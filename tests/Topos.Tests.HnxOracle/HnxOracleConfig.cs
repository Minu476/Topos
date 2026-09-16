using System.Diagnostics;
using System.Text.Json;

namespace Topos.Tests.HnxOracle;

/// <summary>
/// How this test project reaches HyperNetX and xgi — `docs/ALGORITHM_SPEC.md` §6.1, resolved
/// 2026-09-16 (`docs/DECISIONS.md`): <b>the pinned <c>topos-hnx-oracle</c> container by default,
/// with an environment-variable escape hatch to a local interpreter.</b>
///
/// Mirrors <c>Topos.Tests.GdsOracle</c>'s <c>Neo4jTestConfig</c> deliberately: env-var driven with
/// defaults that point at a disposable container rather than at anything of the developer's,
/// probed for reachability up front, and <b>skipping rather than failing</b> when the oracle is
/// absent — so a machine without Docker still runs the whole suite green, exactly as the GDS
/// parity tests already do (`docs/GDS_ORACLE_SETUP.md`).
///
/// The one structural difference from the GDS oracle: HNX is a library, not a server, so there is
/// no long-running sidecar to connect to. Each request runs the worker once and exits. That makes
/// spec §6.1's two options — container versus local subprocess — the same code path with a
/// different interpreter, which is why both are supported here at no real cost rather than one
/// being chosen and the other written off.
/// </summary>
internal sealed class HnxOracleConfig
{
    /// <summary>Set <c>TOPOS_HNX_ORACLE_PYTHON</c> to a local interpreter to bypass Docker entirely.</summary>
    public static readonly string? LocalPython = Environment.GetEnvironmentVariable("TOPOS_HNX_ORACLE_PYTHON");

    /// <summary>The container image built by <c>tests/Topos.Tests.HnxOracle/Dockerfile</c>.</summary>
    public static readonly string Image =
        Environment.GetEnvironmentVariable("TOPOS_HNX_ORACLE_IMAGE") ?? "topos-hnx-oracle";

    private static readonly string ScriptDirectory = AppContext.BaseDirectory.TrimEnd('/', '\\');
    private static readonly string ScriptName = "hnx_oracle.py";

    /// <summary>
    /// Probed once per test run, not once per test: a cold <c>docker run</c> is slow enough that
    /// paying it for every parity assertion would dominate the suite's runtime.
    /// </summary>
    private static readonly Lazy<Task<bool>> ReachableProbe = new(async () =>
    {
        try
        {
            var response = await QueryAsync(new { op = "ping" }, TimeSpan.FromSeconds(90));
            return response.Result is not null;
        }
        catch
        {
            return false;
        }
    });

    public static Task<bool> IsReachableAsync() => ReachableProbe.Value;

    internal sealed record Response(JsonElement? Result, string? Error)
    {
        /// <summary>The result, or a failed assertion naming the oracle's own error text.</summary>
        public JsonElement Ok()
        {
            Assert.True(Result is not null, $"HNX oracle reported: {Error}");
            return Result!.Value;
        }
    }

    public static async Task<Response> QueryAsync(object request, TimeSpan? timeout = null)
    {
        var start = LocalPython is null
            ? new ProcessStartInfo("docker")
            {
                ArgumentList =
                {
                    "run", "--rm", "-i",
                    // Never reach for a registry. Without this, a machine that simply has not built
                    // the image -- CI, most obviously -- spends the reachability probe waiting on a
                    // Docker Hub pull for an image that was never published there, before failing.
                    // --pull=never turns "not built here" into an immediate local error, which is
                    // what the soft-skip wants.
                    "--pull=never",
                    // Read-only, and only the test output directory -- the worker needs nothing else.
                    "-v", $"{ScriptDirectory}:/oracle:ro",
                    Image, $"/oracle/{ScriptName}",
                },
            }
            : new ProcessStartInfo(LocalPython)
            {
                ArgumentList = { Path.Combine(ScriptDirectory, ScriptName) },
            };

        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.UseShellExecute = false;

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start the HNX oracle worker.");

        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(120));

        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(request));
        process.StandardInput.Close();

        var stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
        await process.WaitForExitAsync(cts.Token);

        if (string.IsNullOrWhiteSpace(stdout))
            return new Response(null, $"worker produced no output (exit {process.ExitCode}).");

        using var document = JsonDocument.Parse(stdout);
        if (document.RootElement.TryGetProperty("error", out var error))
            return new Response(null, error.GetString());

        // Cloned because the JsonDocument is disposed at the end of this scope.
        return new Response(document.RootElement.GetProperty("result").Clone(), null);
    }

    /// <summary>Which oracle actually answered — surfaced so a parity run is attributable.</summary>
    public static async Task<string> DescribeAsync()
    {
        var response = await QueryAsync(new { op = "ping" });
        var result = response.Ok();
        string transport = LocalPython is null ? $"docker:{Image}" : $"local:{LocalPython}";
        return $"{transport} hypernetx={result.GetProperty("hypernetx").GetString()} " +
               $"xgi={result.GetProperty("xgi").GetString()}";
    }
}
