# Architecture

Credere is a **modular monolith**. One deployable artifact. Strict bounded-context modules inside. Module boundaries are enforced at build time by `tests/Credere.ArchitectureTests/` using `NetArchTest`. A failing architecture test is a failing build.

## 1. System context (C4 — Level 1)

```
┌──────────────┐           ┌──────────────┐           ┌──────────────┐
│   Banker     │   HTTPS   │              │   stdio   │   Claude     │
│  (browser)   ├──────────▶│   Credere    │◀──────────┤   Desktop    │
└──────────────┘           │              │           └──────────────┘
                           │  monolith    │
┌──────────────┐           │              │           ┌──────────────┐
│   Scheduler  │  trigger  │              │   OTLP    │   Langfuse   │
│   (cron)     ├──────────▶│              ├──────────▶│   + Aspire   │
└──────────────┘           └──────┬───────┘           └──────────────┘
                                  │
                      ┌───────────┴───────────┐
                      ▼                       ▼
              ┌──────────────┐        ┌──────────────┐
              │ SQL Server   │        │  MongoDB     │
              │  (aggregates)│        │  (AI + blobs)│
              └──────────────┘        └──────────────┘
```

Three hosts share the monolith's binaries but run separately:

- **`Credere.Api`** — ASP.NET Core minimal-API host. Serves the banker UI and external callers.
- **`Credere.Batch`** — worker host. Runs the daily accrual + aging job and the outbox dispatcher.
- **`Credere.Mcp`** — MCP server (stdio in M5, HTTP in M6). Exposes read tools to Claude Desktop.

## 2. Module map (C4 — Level 2)

```
                     ┌───────────────────────────────────┐
                     │           Hosts (composition)     │
                     │  Credere.Api  Credere.Batch  Credere.Mcp
                     └──────────────────┬────────────────┘
                                        │ wires via IModule interfaces
                                        ▼
       ┌──────────────┬──────────────┬──────────────┬──────────────┬──────────────┐
       │ Origination  │   Servicing  │   Documents  │   Reporting  │      Ai      │
       │   module     │    module    │    module    │    module    │    module    │
       └──────┬───────┴──────┬───────┴──────┬───────┴──────┬───────┴──────┬───────┘
              │              │              │              │              │
              └──────────────┴──────┬───────┴──────────────┴──────────────┘
                                    │
                            ┌───────┴───────┐
                            │ Credere.Core  │   NodaTime-only shared kernel
                            │ Credere.Shared│   Result, outbox, idempotency
                            └───────────────┘
```

### Modules

| Module | Bounded context | SQL schema | Public interface |
|---|---|---|---|
| **Origination** | LoanApplication aggregate; underwriting decision | `origination` | `IOriginationModule` |
| **Servicing** | Loan aggregate; payments with allocation; aging; payoff; schedule + APRC | `servicing` | `IServicingModule` |
| **Documents** | PDF generation; blob storage in Mongo; metadata in SQL | `documents` | `IDocumentsModule` |
| **Reporting** | Dashboard read models (pure event consumer) | `reporting` | `IReportingModule` |
| **Ai** | Agents, MCP tools, deterministic guardrails; Mongo artifact stores | `ai` | `IAiModule` |

## 3. Dependency rules

The following rules are tested in `tests/Credere.ArchitectureTests/`:

1. `Credere.Core` depends only on NodaTime.
2. `Credere.Shared` depends only on BCL.
3. A `Credere.Modules.*` project references at most `Credere.Core` + `Credere.Shared`.
4. A `Credere.Modules.X` project MUST NOT reference `Credere.Modules.Y` for any Y ≠ X.
5. Host projects (`Credere.Api`, `Credere.Batch`, `Credere.Mcp`) may reference module projects for DI composition but must consume them through `I<Module>Module`.
6. Tests may reference anything they test.

Breaking any of these fails `dotnet build`. The fix is never to disable the test; it is to redesign the dependency (usually via an integration event).

## 4. Internal layout of a module

Each module project is self-contained and organized by feature slice internally:

```
Credere.Modules.Servicing/
├── Application/
│   ├── IServicingModule.cs                 Public contract (the only exposed type).
│   ├── Commands/
│   │   ├── DisburseLoan/
│   │   │   ├── DisburseLoanCommand.cs
│   │   │   ├── DisburseLoanHandler.cs
│   │   │   └── DisburseLoanValidator.cs
│   │   ├── RecordPayment/                  Payment allocation lives here, not in the aggregate.
│   │   ├── ApplyAging/
│   │   └── Payoff/
│   ├── Queries/
│   └── IntegrationEventHandlers/
├── Domain/
│   ├── Loan.cs                             Aggregate (internal).
│   ├── LoanId.cs
│   ├── LoanState.cs
│   ├── Events/                             Domain events (internal).
│   └── PaymentAllocationWaterfall.cs
├── Infrastructure/
│   ├── ServicingDbContext.cs               schema="servicing"
│   ├── Migrations/
│   ├── LoanRepository.cs
│   └── OutboxBridge.cs
├── IntegrationEvents/                      Published to other modules (public contracts).
│   ├── LoanDisbursed.cs
│   ├── PaymentRecorded.cs
│   └── LoanAged.cs
└── ServicingModuleExtensions.cs            services.AddServicingModule(config);
```

Feature folders are vertical slices *inside* the module: command + handler + validator + tests live together. Shared helpers emerge only after the third repetition.

## 5. Persistence

### SQL Server (primary — one schema per module)

- Each module has its own `DbContext`, pointed at the same SQL Server instance but a distinct schema (`origination`, `servicing`, `documents`, `reporting`, `ai`).
- Each aggregate has a **current-state table** with `[Timestamp] byte[] RowVersion` for optimistic concurrency.
- Each aggregate has an **append-only history table** (`<Entity>Events`) written in the same transaction as the state write via `SaveChangesAsync`.
- Each module has its own **outbox table** written in the same transaction.
- Command idempotency: per-handler **receipt tables** keyed on `CommandId`.

See [`adr/0003-per-module-dbcontext-and-schema.md`](adr/0003-per-module-dbcontext-and-schema.md) and [`adr/0004-append-only-history.md`](adr/0004-append-only-history.md).

### MongoDB (secondary — AI artifacts + document blobs)

Collections:

| Collection | Purpose |
|---|---|
| `ai.agent_sessions` | One doc per agent invocation. Inputs, tool calls, final output, guardrail decisions, timings, cost, model version. |
| `ai.llm_calls` | Raw prompt / response pairs for post-hoc analysis. |
| `ai.eval_results` | Golden-dataset run outputs with judge scores, versioned by judge-prompt version. |
| `ai.eval_golden` | Golden datasets (mirrored as JSON lines under `docs/ai/golden/`). |
| `ai.agent_memory` | Optional per-agent scratchpad. |
| `documents.blobs` | Generated PDFs as binary with metadata (loan id, doc type, hash). |

The Documents module stores PDF metadata as a row in `documents.Documents` (SQL) and the blob bytes in `documents.blobs` (Mongo). Queries use SQL; blob retrieval uses Mongo. The transactional guarantee is: the SQL row is only committed if the Mongo write succeeds (two-phase pattern with compensation on failure — detailed in the Documents module doc).

## 6. Integration events — initial catalog

Events are `record` types defined in the publisher module's `IntegrationEvents/` folder. Subscribers reference only those records (no cross-module domain-model leakage).

| Event | Publisher | Subscribers |
|---|---|---|
| `ApplicationSubmitted` | Origination | Ai (credit decisioning), Reporting |
| `ApplicationDecided` | Origination | Servicing (await disbursement), Reporting |
| `LoanDisbursed` | Servicing | Documents (generate agreement PDF), Reporting |
| `PaymentRecorded` | Servicing | Reporting, Ai (early-warning signal source) |
| `LoanAged` | Servicing | Reporting, Ai (early-warning, collections) |
| `DocumentGenerated` | Documents | Reporting |
| `AgentRecommendationProduced` | Ai | Reporting |

## 7. Outbox and dispatch

Every module writes integration events into its own `<schema>.Outbox` table inside the SQL transaction that commits the state change. A single hosted service — `OutboxDispatcher` in `Credere.Batch` — polls every module's outbox at a configurable interval (default 2s), dispatches events to subscriber handlers via DI, and marks the row dispatched on success.

Idempotency on the subscriber side is enforced by a per-subscriber `(SubscriberKey, EventId)` receipt row.

The dispatcher is deliberately in-process. Swapping to a message broker later means replacing the dispatcher with a bridge that publishes to the broker. Module code does not change.

See [`adr/0005-transactional-outbox.md`](adr/0005-transactional-outbox.md).

## 8. Module public contracts

Each module exposes exactly one interface:

```csharp
public interface IOriginationModule
{
    Task<Result<ApplicationId, Error>> SubmitAsync(SubmitApplication cmd, CancellationToken ct);
    Task<Result<DecisionView, Error>> DecideAsync(DecideApplication cmd, CancellationToken ct);
    Task<ApplicationView?> GetAsync(ApplicationId id, CancellationToken ct);
}
```

- The interface is the only type exposed outside the module.
- DTOs used by the interface (`SubmitApplication`, `DecisionView`, `ApplicationView`, `Error`, strongly-typed id) are also public.
- Everything else inside the module (aggregates, repositories, DbContext, domain events, feature handlers) is `internal`.

Hosts wire modules via extension methods:

```csharp
builder.Services
    .AddCredereCore(builder.Configuration)
    .AddOriginationModule(builder.Configuration)
    .AddServicingModule(builder.Configuration)
    .AddDocumentsModule(builder.Configuration)
    .AddReportingModule(builder.Configuration)
    .AddAiModule(builder.Configuration);
```

## 9. Cross-cutting concerns

- **Observability:** OpenTelemetry is configured in the host. Every module's `DbContext` is instrumented; every agent / MCP call produces a span.
- **Auth:** JWT bearer with scopes (`banker:read`, `banker:simulate`, `banker:write`). Validated in the API host and in the MCP host.
- **Config:** typed options per module (`OriginationOptions`, `ServicingOptions`, etc.). Bound from `appsettings.json` + `user-secrets`.

## 10. Testing strategy

| Scope | Project | Tooling |
|---|---|---|
| Unit | `Credere.Core.Tests`, `Credere.Shared.Tests` | xUnit, FluentAssertions, FsCheck.Xunit |
| Mutation | `Credere.Core.Tests` | Stryker.NET ≥80% |
| Per-module | `Credere.Modules.*.Tests` | xUnit, Testcontainers (SQL Server + Mongo as needed) |
| End-to-end composition | `Credere.Integration.Tests` | WebApplicationFactory + Testcontainers |
| Architecture | `Credere.ArchitectureTests` | NetArchTest — enforces every rule in §3 |
| AI evaluation | `Credere.Ai.Evals` (console) + `Credere.Modules.Ai.Tests` | Hand-rolled harness, LLM-as-judge |

Running `dotnet test` from the repo root runs every scope except AI evaluations (which cost money and run via a separate `credere-evals` entry point).
