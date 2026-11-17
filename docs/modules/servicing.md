# Module — Servicing

**Project:** `src/Credere.Modules.Servicing/`
**Schema:** `servicing`
**Public contract:** `IServicingModule`

## Bounded context

The Servicing module owns the life of a disbursed loan: disbursement, interest accrual, payments (with real allocation), aging transitions, payoff, and terminal outcomes (write-off, restructure, recover). It is the module that must stay correct even if everything else fails — the aggregate here is the one a banking regulator would scrutinise first.

## Aggregates

### `Loan` (internal)

```csharp
internal sealed class Loan
{
    public LoanId Id { get; }
    public LoanApplicationId SourceApplicationId { get; }
    public int TermsVersion { get; }
    public Money Principal { get; }
    public Money RemainingPrincipal { get; private set; }
    public Money FeesOutstanding { get; private set; }
    public Money PastDueInterest { get; private set; }
    public Money CurrentInterest { get; private set; }
    public InterestRate Rate { get; }
    public LoanTerm Term { get; }
    public LocalDate DisbursementDate { get; }
    public LocalDate FirstPaymentDate { get; }
    public LoanState State { get; private set; }
    public int? DaysPastDue { get; private set; }

    internal Loan(Disburse cmd);

    internal Result<Unit, DomainError> Activate(Activate cmd, HolidayCalendar calendar);
    internal Result<PaymentAllocation, DomainError> RecordPayment(RecordPayment cmd);
    internal Result<Unit, DomainError> ApplyAging(ApplyAging cmd);
    internal Result<PayoffQuote, DomainError> QuotePayoff(QuotePayoff query, LocalDate asOf);
    internal Result<Unit, DomainError> ExecutePayoff(ExecutePayoff cmd, PayoffQuote quote);
    internal Result<Unit, DomainError> WriteOff(WriteOff cmd);
    internal Result<Unit, DomainError> Restructure(Restructure cmd);
    internal Result<Unit, DomainError> Recover(Recover cmd);
}
```

See [`../domain/loan-lifecycle.md`](../domain/loan-lifecycle.md) for the state machine and [`../domain/payment-allocation.md`](../domain/payment-allocation.md) for `RecordPayment` semantics.

## Public interface

```csharp
public interface IServicingModule
{
    Task<Result<LoanId, DomainError>> DisburseAsync(Disburse cmd, CancellationToken ct);
    Task<Result<Unit, DomainError>> ActivateAsync(Activate cmd, CancellationToken ct);
    Task<Result<PaymentResult, DomainError>> RecordPaymentAsync(RecordPayment cmd, CancellationToken ct);
    Task<Result<AgingResult, DomainError>> ApplyAgingAsync(ApplyAging cmd, CancellationToken ct);
    Task<Result<PayoffQuote, DomainError>> QuotePayoffAsync(QuotePayoff query, CancellationToken ct);
    Task<Result<PayoffResult, DomainError>> ExecutePayoffAsync(ExecutePayoff cmd, CancellationToken ct);
    Task<Result<Unit, DomainError>> WriteOffAsync(WriteOff cmd, CancellationToken ct);
    Task<Result<Unit, DomainError>> RestructureAsync(Restructure cmd, CancellationToken ct);
    Task<Result<Unit, DomainError>> RecoverAsync(Recover cmd, CancellationToken ct);

    Task<LoanView?> GetAsync(LoanId id, CancellationToken ct);
    Task<AmortizationSchedule?> GetScheduleAsync(LoanId id, CancellationToken ct);
    Task<IReadOnlyList<LoanView>> ListAsync(LoanListFilters filters, CancellationToken ct);
}
```

All commands carry a `CommandId`.

## Integration events published

| Event | When | Payload (summary) |
|---|---|---|
| `LoanDisbursed` | `DisburseAsync` succeeds | Id, source application id, terms version, principal, rate snapshot, term, dates, correlation id |
| `LoanActivated` | `ActivateAsync` succeeds | Id, effective date, correlation id |
| `PaymentRecorded` | `RecordPaymentAsync` succeeds | Id, allocation breakdown (see `PaymentAllocation`), new balances, correlation id |
| `LoanAged` | `ApplyAgingAsync` transitions state | Id, new state, days past due, correlation id |
| `LoanPaidOff` | Loan reaches zero balance | Id, final payment correlation id |
| `LoanWrittenOff` | `WriteOffAsync` succeeds | Id, reason, written-off balance, correlation id |
| `LoanRestructured` | `RestructureAsync` succeeds | Id, plan, correlation id |
| `LoanRecovered` | `RecoverAsync` succeeds | Id, method, recovered amount, correlation id |

## Integration events consumed

- `ApplicationAccepted` — triggers the handoff. The Servicing module inserts a `DisburseIntent` record and waits for the external disbursement confirmation (operator action or batch). This decouples underwriting acceptance from treasury disbursement.

## Persistence — `servicing` schema

| Table | Purpose |
|---|---|
| `Loans` | Current-state row per loan; RowVersion for optimistic concurrency. |
| `LoanEvents` | Append-only history keyed by `(StreamId, StreamVersion)`. |
| `Outbox` | Integration events pending dispatch. |
| `LoanSchedules` | Row-per-schedule-row amortization schedule (snapshot at origination; regenerated on reset for floating). |
| `AccrualLedger` | Daily accrual rows written by the batch job. |
| `CommandReceipts_Disburse` ... | One receipt table per command handler. |

## Invariants

- Principal, dates, and rate are immutable after disbursement. Changes require restructuring (which creates a new, linked loan — out of M2 scope).
- Every state transition emits exactly one domain event.
- Every allocated payment produces a conservation-balanced `PaymentAllocation`.
- The sum of `AccrualLedger` entries for a loan equals `CurrentInterest + PastDueInterest + (interest already allocated via payments)`.

## Feature slices (M2 initial set)

```
Credere.Modules.Servicing/Application/Commands/
├── Disburse/
├── Activate/
├── RecordPayment/            Internally delegates to PaymentAllocationWaterfall.
├── ApplyAging/
├── QuotePayoff/
├── ExecutePayoff/
├── WriteOff/
├── Restructure/
└── Recover/
```

```
Credere.Modules.Servicing/Application/IntegrationEventHandlers/
└── OnApplicationAccepted/    Creates the DisburseIntent.
```

## Interest accrual

Accrual is driven by the daily batch, not the aggregate. For each loan in a non-terminal state, the batch:

1. Computes `interestForDay = Rate.CalculateInterest(RemainingPrincipal, previousAccrualDate, today)`.
2. Appends a row to `servicing.AccrualLedger`.
3. Dispatches an `InterestAccrued` internal domain event (not published cross-module in M2; it may be in a later milestone if agents need it).
4. Updates the loan's `CurrentInterest` in a single transaction.

Idempotency on accrual is keyed by `(LoanId, BusinessDate)` — re-running the batch for the same date is a no-op.

## Open items

- Floating-rate reset tracking (M3 if time allows; otherwise M4).
- Restructuring that creates a new linked loan (M4+).
- Early-repayment APRC recalculation (M7+).
