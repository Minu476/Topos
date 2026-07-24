using Neo4j.Driver;

namespace Topos.Tests.GdsOracle;

/// <summary>
/// GDS-oracle connection config (spec §5, §5.1). Test-project-only by construction — this whole
/// project is the GPLv3 isolation boundary: <c>Neo4j.Driver</c> and every GDS-touching type live
/// here, never in <c>Topos.Hypergraph</c>, which has zero reference to this project or to Neo4j
/// at all.
///
/// Mirrors RLB's own pattern (`RichLearning.V2.Tests/EdgeThetaPersistenceTests.cs`
/// <c>Neo4jTestConfig</c>) — env-var driven, connectivity-checked, and skips gracefully rather
/// than failing the whole suite when no oracle is reachable. Defaults point at the disposable
/// Docker container this harness expects (`docker run ... -p 17687:7687 ... neo4j:2026.06-community`
/// with the GDS plugin, isolated from any other local Neo4j instance — see
/// `docs/GDS_ORACLE_SETUP.md`), not at a developer's real Neo4j data.
/// </summary>
internal sealed record Neo4jTestConfig(string Uri, string User, string Password, string Database)
{
    public static Neo4jTestConfig Default { get; } = new(
        Environment.GetEnvironmentVariable("TOPOS_GDS_ORACLE_URI") ?? "bolt://localhost:17687",
        Environment.GetEnvironmentVariable("TOPOS_GDS_ORACLE_USER") ?? "neo4j",
        Environment.GetEnvironmentVariable("TOPOS_GDS_ORACLE_PASSWORD") ?? "toposdev123",
        Environment.GetEnvironmentVariable("TOPOS_GDS_ORACLE_DATABASE") ?? "neo4j");

    /// <summary>
    /// Connectivity + GDS-availability probe, short-timeout by design (this must fail fast in
    /// CI/dev environments without the oracle running, not hang the whole suite).
    /// </summary>
    public async Task<bool> IsReachableAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var driver = GraphDatabase.Driver(Uri, AuthTokens.Basic(User, Password));
            await using var session = driver.AsyncSession(o => o.WithDatabase(Database));
            var result = await session.RunAsync("RETURN gds.version() AS version");
            await result.ConsumeAsync();
            await driver.DisposeAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public IDriver CreateDriver() => GraphDatabase.Driver(Uri, AuthTokens.Basic(User, Password));
}
