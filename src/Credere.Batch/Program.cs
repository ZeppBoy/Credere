using Credere.Modules.Ai;
using Credere.Modules.Documents;
using Credere.Modules.Origination;
using Credere.Modules.Reporting;
using Credere.Modules.Servicing;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOriginationModule(builder.Configuration)
    .AddServicingModule(builder.Configuration)
    .AddDocumentsModule(builder.Configuration)
    .AddReportingModule(builder.Configuration)
    .AddAiModule(builder.Configuration);

// M4 will register the OutboxDispatcher and the daily accrual + aging jobs here.

await builder.Build().RunAsync().ConfigureAwait(false);
