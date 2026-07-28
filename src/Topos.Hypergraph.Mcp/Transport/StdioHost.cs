using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Topos.Hypergraph.Mcp;

// The MCP SDK's canonical stdio host shape (verified against the SDK's own
// QuickstartWeatherServer sample's Program.cs): stdout is the JSON-RPC channel, so all logging
// must go to stderr — an agent spawning this as a subprocess would otherwise see corrupted
// protocol frames on stdout.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ToposMcpServer>();

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

await builder.Build().RunAsync();
