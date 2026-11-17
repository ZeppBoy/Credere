# ADR-0007: Command idempotency via `CommandId` + per-handler receipt tables

**Status:** Accepted 2026-11-17.

## Context

Any command in Credere can be retried: an HTTP client retries after a network hiccup; the batch job retries a failed run; an MCP tool call is replayed by an agent after a tool-level error. Without idempotency, the second attempt produces duplicate state changes, duplicate events, duplicate payments.

Promissio handled idempotency only for the application-to-loan handoff (`LoanCreationReceipt` keyed on `LoanApplicationId`). Every other command — Activate, ApplyAging, future RecordPayment — was wide open to duplicate submission.

## Decision

Every command record carries a `Guid CommandId`:

```csharp
public sealed record RecordPayment(
    Guid CommandId,
    LoanId Id,
    Money Amount,
    LocalDate EffectiveDate,
    Guid CorrelationId);
```

Each handler maintains a per-handler receipt table:

```
<Schema>.CommandReceipts_<HandlerName>
    CommandId          (Guid, PK)
    ReceivedAt         (Instant)
    OutcomeJson        (string, serialized Result or key id)
```

Handler flow:

1. On entry: `SELECT OutcomeJson FROM <ReceiptTable> WHERE CommandId = @id`.
2. If found, deserialize and return — no state change.
3. Otherwise, execute the handler normally. In the same transaction as the state change and the integration-event outbox write, **insert the receipt row** with the computed outcome.
4. Commit.

A duplicate command from a retry produces the same returned result as the original, with no side effects.

### Scope

- Every public command handler has a receipt table.
- Query handlers do not (queries are naturally idempotent).
- Integration-event handlers have their own receipt tables (`ProjectionReceipts_<EventType>`) — see [`0005-transactional-outbox.md`](0005-transactional-outbox.md).
- Internal, module-private commands (helpers, batch sub-steps) do not require receipts; idempotency at the public boundary is sufficient.

### Receipt retention

- Receipts are retained forever by default.
- A retention policy (e.g., delete receipts older than 90 days) could be added in a future ADR. For a portfolio-scale demo, perpetual retention is fine.

## Consequences

- **Pros**
  - Retry-safe at every command boundary.
  - Transparent to the caller — the client doesn't need to know anything special.
  - Integrates naturally with the outbox and append-only history (all three write in one transaction).

- **Costs**
  - One extra table per handler. Many modules → many tables. Acceptable for the modularity benefit.
  - Clients must generate the `CommandId` before sending — a trivial burden (`Guid.NewGuid()`), but the convention must be enforced in the API host.
  - Receipt tables grow unless pruned.

- **Rejected alternatives**
  - **Single global `CommandReceipts` table:** cross-module coupling and a hot spot.
  - **Idempotency via content-hash of request payload:** fragile (any formatting change invalidates the hash), and content-hashing a Money + dates is fiddly.
  - **No idempotency, "retries are the caller's problem":** trivially wrong for money-moving operations.
