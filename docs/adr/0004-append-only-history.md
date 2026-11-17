# ADR-0004: Per-aggregate append-only history, transactional with state writes

**Status:** Accepted 2026-11-17.

## Context

We want auditability — every state change on every aggregate must be reconstructable from history — without paying the full cost of event sourcing (versioning, upcasters, projection rebuild policies, snapshot management) that Promissio deferred into permanent debt.

Full event sourcing stores events as the source of truth and reconstructs state via replay. That's powerful but expensive for a single-author portfolio: schema evolution becomes a first-class problem, projections must be rebuildable, and snapshotting for performance becomes a design question.

The alternative is **current-state + append-only history** in the same transaction:

- Normal relational writes update the aggregate's current state (with optimistic concurrency).
- The handler also appends a history row per domain event in the same `SaveChangesAsync` transaction.
- Queries read the current-state row directly. History is for audit / replay / regulatory inspection.

This gives us the audit trail without the event-sourcing tax.

## Decision

Each aggregate in each module has:

1. A **current-state table** (`<Schema>.<AggregateNamePlural>`) with `[Timestamp] byte[] RowVersion` for optimistic concurrency.
2. An **append-only history table** (`<Schema>.<AggregateName>Events`) with columns:
   - `Id` (Guid, PK, generated per event)
   - `StreamId` (Guid, the aggregate's id)
   - `StreamVersion` (long, monotonic within a stream)
   - `Type` (string, the fully-qualified short event type name)
   - `PayloadJson` (string, the serialized event body)
   - `OccurredAt` (business-effective date, `LocalDate` or `Instant` depending on event)
   - `RecordedAt` (`Instant`, when the row was written)
   - `CorrelationId` (Guid)
   - `CommandId` (Guid, for traceback to the command that produced the event)
3. A unique index `(StreamId, StreamVersion)` to prevent duplicate stream versions.

In handlers:

- Load the aggregate's current-state row with its `RowVersion`.
- Apply the command; collect the domain events into an uncommitted list.
- In a single `SaveChangesAsync` transaction: update the state row (RowVersion increments) + insert the history rows.
- On `DbUpdateConcurrencyException`, return `Result<T, DomainError>.Failure(new ConcurrencyConflict(...))`. The caller retries.

Replay is supported for audit queries and reporting rebuilds:

- `Rehydrate(IEnumerable<DomainEvent>)` on the aggregate reconstructs state from history.
- Replay is used by `Credere.Integration.Tests` to verify aggregate-level invariants are preserved under event replay.

## Event schema evolution

- Every event record carries a `[EventVersion(n)]` attribute (declared on the record class). Default is 1.
- Breaking changes increment the version. Each breaking change ships with an **upcaster** — a small transformation function from version `n` to version `n+1`, located in the module's `Infrastructure/Upcasters/`.
- Upcasters are tested with a locked JSON fixture per prior version.
- Removing an event type is forbidden; events are append-only in schema as well as in data.

## Consequences

- **Pros**
  - Audit trail for free; auditors can reconstruct any aggregate at any point.
  - No projection-rebuild policy needed (reporting rebuilds use the event history as the source).
  - No "I deferred event versioning" tech debt — the versioning rule is written into every event from day one.
  - Standard relational writes for the hot path — no in-memory replay on every command.

- **Costs**
  - Each handler must remember to emit domain events into the uncommitted list, and the module's `SaveChangesAsync` must know how to drain that list. An `AggregateSavingInterceptor` handles this uniformly.
  - History tables grow forever. A retention policy (by business date) could be added in a later ADR; initial assumption is unbounded retention.
  - Slightly more storage than pure current-state.

- **Rejected alternatives**
  - **Full event sourcing (Marten):** carries versioning/upcaster/projection-rebuild tax disproportionate to a single-author demo.
  - **Trigger-based audit (SQL Server Temporal Tables):** opaque to the application layer; cannot carry correlation/command ids cleanly.
  - **Audit log table without stream versioning:** loses the ability to detect gaps or out-of-order replays.
