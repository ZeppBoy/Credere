# ADR-0005: Transactional outbox for cross-module integration events

**Status:** Accepted 2026-11-17.

## Context

Modules need to communicate without referencing each other. The chosen mechanism is **integration events**: records defined in the publisher's `IntegrationEvents/` folder, consumed by subscribers via handlers.

Delivering an integration event reliably has two subtle requirements:

1. It must not be published if the originating transaction rolls back.
2. It must not be lost if the process crashes between the DB commit and the publish.

A direct in-process dispatch at the end of a successful handler fails both requirements under failure. A message broker (e.g., NATS, RabbitMQ) is overkill for a single-process monolith.

## Decision

Use the **transactional outbox pattern**, implemented per module:

1. Every module has an `<Schema>.Outbox` table with columns:
   - `Id` (Guid, PK, event id)
   - `OccurredAt` (Instant)
   - `Type` (string, event type name)
   - `PayloadJson` (string, serialized event)
   - `DispatchedAt` (Instant?, null until dispatched)
   - `AttemptCount` (int, 0 initially)
   - `LastError` (string?, truncated)
2. When a handler emits an integration event, the event is inserted into the module's `Outbox` in the same `SaveChangesAsync` transaction as the state change and the aggregate history row.
3. A single hosted service — `OutboxDispatcher` in `Credere.Batch` — polls every module's outbox at a configurable interval (default 2 seconds) and dispatches events to in-process subscriber handlers via DI.
4. On successful delivery, `DispatchedAt` is set. On failure, `AttemptCount` increments and `LastError` is populated; retries continue with exponential backoff capped at 5 minutes.
5. Subscribers are idempotent: each subscriber has a receipt table `ProjectionReceipts_<EventType>` keyed on `EventId`. Duplicate delivery is a no-op.

### Subscriber discovery

Subscribers register themselves via DI:

```csharp
services.AddIntegrationEventHandler<LoanDisbursed, GenerateLoanAgreementHandler>();
```

The outbox dispatcher resolves handlers for each event type and invokes them in registration order. Handler failures do not cascade: each handler succeeds or fails independently; its own receipt table decides whether the delivery is complete.

### Why in-process dispatch?

For a monolith, in-process dispatch is straightforward, fast, and testable. The outbox table is the durable boundary — swapping the dispatcher for a bridge that publishes to a message broker is a local change that doesn't touch module code.

## Consequences

- **Pros**
  - No lost events on process crash between commit and publish.
  - No events published on rolled-back transactions.
  - Clear path to a real message broker later (replace the dispatcher implementation).
  - Idempotent subscribers by design.

- **Costs**
  - A small background service must run for events to be delivered.
  - Dispatch latency is bounded by poll interval (default 2 seconds — fast enough for the demo, deliberately slow enough to catch design assumptions about synchrony).
  - Outbox table grows; a retention policy (delete dispatched rows older than N days) is a future addition.

- **Rejected alternatives**
  - **In-process dispatch at end of handler without outbox:** loses the crash-safety guarantee.
  - **Change Data Capture (CDC):** heavy; requires SQL Server CDC enablement and a separate pipeline.
  - **Direct publish to a message broker:** warranted only when the monolith splits.
