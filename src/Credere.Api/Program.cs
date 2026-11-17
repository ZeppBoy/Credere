using Credere.Modules.Ai;
using Credere.Modules.Documents;
using Credere.Modules.Origination;
using Credere.Modules.Reporting;
using Credere.Modules.Servicing;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOriginationModule(builder.Configuration)
    .AddServicingModule(builder.Configuration)
    .AddDocumentsModule(builder.Configuration)
    .AddReportingModule(builder.Configuration)
    .AddAiModule(builder.Configuration);

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapOpenApi();

await app.RunAsync().ConfigureAwait(false);
