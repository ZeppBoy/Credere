# Module — Reporting

**Project:** `src/Credere.Modules.Reporting/`
**Schema:** `reporting`
**Public contract:** `IReportingModule`

## Bounded context

Reporting is the read side. It projects data from every other module's integration events into denormalized tables tuned for the queries that power the Banker UI and (optionally) the Ai module. It is a **pure consumer** — it publishes nothing, owns no aggregates, enforces no business rules beyond projection-level invariants.

## Why have a dedicated read module?

Without Reporting, every query-time join would need to cross module boundaries (Origination → Servicing → Documents). That would either:

- Require modules to expose everything via their public interfaces (contract bloat), or
- Force callers to compose results in the UI layer (duplication).

A dedicated projection module is the canonical answer in a modular monolith. See [`../adr/0001-modular-monolith.md`](../adr/0001-modular-monolith.md).

## Public interface

```csharp
public interface IReportingModule
{
    Task<DashboardView> GetDashboardAsync(CancellationToken ct);

    Task<IReadOnlyList<ApplicationRow>> ListApplicationsAsync(
        ApplicationListQuery query, CancellationToken ct);

    Task<ApplicationDetail?> GetApplicationDetailAsync(
        LoanApplicationId id, CancellationToken ct);

    Task<IReadOnlyList<LoanRow>> ListLoansAsync(
        LoanListQuery query, CancellationToken ct);

    Task<LoanDetail?> GetLoanDetailAsync(
        LoanId id, CancellationToken ct);

    Task<IReadOnlyList<WatchlistRow>> GetWatchlistAsync(
        CancellationToken ct);

    Task<PortfolioMetrics> GetPortfolioMetricsAsync(
        LocalDate asOf, CancellationToken ct);
}
```

## Integration events consumed

Everything in the catalog:

- `ApplicationSubmitted`, `ApplicationDecided`, `ApplicationAccepted`, `ApplicationWithdrawn`
- `LoanDisbursed`, `LoanActivated`, `PaymentRecorded`, `LoanAged`, `LoanPaidOff`, `LoanWrittenOff`, `LoanRestructured`, `LoanRecovered`
- `DocumentGenerated`
- `AgentRecommendationProduced`

Each handler is a simple upsert into one or more projection tables.

## Integration events published

**None.** Reporting is a pure consumer. If it ever needs to publish, that's a design conversation — it means a cross-cutting concern has been slipped into the wrong module.

## Persistence — `reporting` schema

Projection tables, denormalized for query, no FKs across modules:

| Table | Purpose |
|---|---|
| `Applications` | Flat view of application + decision + accepted terms. |
| `Loans` | Flat view of loan + current state + current balances + rate snapshot + accrual-to-date. |
| `LoanTimeline` | One row per lifecycle event per loan (used for the UI timeline). |
| `LoanStaging` | IFRS 9 stage per loan + inputs used (updated daily by the batch). |
| `LoanDocuments` | One row per generated document; joins to Documents for blob retrieval. |
| `Watchlist` | Early-warning watchlist rows produced by the Ai module. |
| `PortfolioDaily` | Daily snapshot of portfolio totals (count, outstanding principal, total past-due). |
| `ProjectionReceipts_<Event>` | One table per subscribed event, `(EventId)` primary key. Idempotency. |

## Projection idempotency

Every handler's first action is to insert a row into `ProjectionReceipts_<Event>`. The receipt table has a unique index on `EventId`. A duplicate delivery hits a constraint violation and the handler no-ops. This is the module's only defense against the outbox dispatcher re-delivering an event.

## Feature slices (M4 initial set)

```
Credere.Modules.Reporting/Application/IntegrationEventHandlers/
├── OnApplicationSubmitted/
├── OnApplicationDecided/
├── OnApplicationAccepted/
├── OnApplicationWithdrawn/
├── OnLoanDisbursed/
├── OnLoanActivated/
├── OnPaymentRecorded/
├── OnLoanAged/
├── OnLoanPaidOff/
├── OnLoanWrittenOff/
├── OnLoanRestructured/
├── OnLoanRecovered/
├── OnDocumentGenerated/
└── OnAgentRecommendationProduced/

Credere.Modules.Reporting/Application/Queries/
├── GetDashboard/
├── ListApplications/
├── GetApplicationDetail/
├── ListLoans/
├── GetLoanDetail/
├── GetWatchlist/
└── GetPortfolioMetrics/
```

## Rebuild policy

- Projections are **rebuildable from scratch** by replaying integration events from the outbox history.
- A `reporting-rebuild` CLI (introduced in M4) truncates the projection tables and replays every event in chronological order.
- Tested in `Credere.Integration.Tests` — the rebuild must produce bit-identical projection content.

## Query endpoints

Reporting's public interface is consumed by `Credere.Api` route groups:

```
/api/v1/dashboard
/api/v1/applications
/api/v1/applications/{id}
/api/v1/loans
/api/v1/loans/{id}
/api/v1/loans/{id}/schedule
/api/v1/watchlist
/api/v1/portfolio/metrics
```

## Invariants

- No projection row survives without the receipt row that proves the event was applied.
- Every projection table has an index covering the UI's primary filter (status, submittedAt, etc.).
- Projections are eventually consistent with the publisher — lag is reported via an OTEL gauge (`projection.lag.seconds`) and surfaced on the Aspire dashboard.

## Testing

- **Handler unit tests** per event: given a seeded projection state and an incoming event, assert the projection state afterward.
- **Rebuild test** in `Credere.Integration.Tests`: generate a realistic sequence of events, apply them live, snapshot projections; then rebuild from empty and assert bit-equality.
- **Idempotency test**: each event is delivered twice; the second application has no effect.
