using Credere.Modules.Ai;
using Credere.Modules.Reporting;
using Credere.Modules.Servicing;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddServicingModule(builder.Configuration)
    .AddReportingModule(builder.Configuration)
    .AddAiModule(builder.Configuration);

// M5 will register the MCP server + tool catalog here.

await builder.Build().RunAsync().ConfigureAwait(false);
