# Module — Servicing

**Project:** `src/Credere.Modules.Servicing/`
**Schema:** `servicing`
**Public contract:** `IServicingModule`
**Status:** The contract sections below are Planned (M2). They were written in M2-C1 from owner decisions D3, D4 and D5 ([ADR-0011](../adr/0011-m2-module-contracts.md)) and land as code in M2-C2 and later. Today `IServicingModule` is an empty interface.

## Bounded context

The Servicing module owns the life of a disbursed loan: disbursement, interest accrual, payments (with real allocation), aging transitions, payoff, and terminal outcomes (write-off, restructure, recover). It is the module that must stay correct even if everything else fails — the aggregate here is the one a banking regulator would scrutinise first.

Servicing does not reference Origination. The handoff from an accepted application is a Servicing-owned input, built by the host ([Handoff from Origination](#handoff-from-origination)).

## Aggregates

### `Loan` (internal)

```csharp
internal sealed class Loan
{
    public LoanId Id { get; }
    public Guid SourceReference { get; }          // Opaque: the originating application's id. Servicing never interprets it.
    public int TermsVersion { get; }
    public string Product { get; }                // Key into ServicingOptions.Products.
    public Money Principal { get; }
    public Money RemainingPrincipal { get; private set; }
    public Money FeesOutstanding { get; private set; }     // Always zero in M2: no fee is posted.
    public Money PastDueInterest { get; private set; }
    public Money CurrentInterest { get; private set; }
    public FixedRate Rate { get; }                // Snapshot: nominal rate from the accepted terms, day count from the product.
    public LoanTerm Term { get; }
    public PaymentFrequency Frequency { get; }
    public RepaymentType Repayment { get; }
    public LocalDate DisbursementDate { get; }
    public LocalDate FirstPaymentDate { get; }
    public int PastDueThreshold { get; }
    public int DefaultThreshold { get; }
    public LocalDate AccruedThroughDate { get; private set; }   // Interest is accrued up to, excluding, this date.
    public LoanState State { get; private set; }
    public int? DaysPastDue { get; private set; }

    internal Loan(Disburse cmd, DisbursementIntent intent, ProductTerms product, AmortizationSchedule schedule);

    internal Result<Unit, DomainError> Activate(Activate cmd, IBusinessCalendar calendar);
    internal Result<AccrualResult, DomainError> AccrueInterest(AccrueInterest cmd, IInterestCalculator calculator);
    internal Result<PaymentResult, DomainError> RecordPayment(RecordPayment cmd, PaymentId paymentId, IInterestCalculator calculator);
    internal Result<AgingResult, DomainError> ApplyAging(ApplyAging cmd);
    internal Result<PayoffQuote, DomainError> QuotePayoff(QuotePayoff query, IInterestCalculator calculator);
    internal Result<PayoffResult, DomainError> ExecutePayoff(ExecutePayoff cmd, PaymentId paymentId, IInterestCalculator calculator);
    internal Result<Unit, DomainError> WriteOff(WriteOff cmd, IInterestCalculator calculator);
    internal Result<Unit, DomainError> Restructure(Restructure cmd);
    internal Result<Unit, DomainError> Recover(Recover cmd);
}
```

Domain code never reads the clock or the calendar's configuration: every date arrives in the command, and `IBusinessCalendar` and `IInterestCalculator` arrive as parameters. The aggregate uses the loan's own `Rate` and the schedule's payment dates. See [`../domain/loan-lifecycle.md`](../domain/loan-lifecycle.md) for the state machine and [`../domain/payment-allocation.md`](../domain/payment-allocation.md) for the waterfall.

### `DisbursementIntent` (internal)

The persisted handoff. One row per `(SourceReference, AcceptedTermsVersion)`; it holds the accepted terms and the product key until treasury confirms the disbursement.

```csharp
internal sealed class DisbursementIntent
{
    public DisbursementIntentId Id { get; }
    public Guid SourceReference { get; }
    public int AcceptedTermsVersion { get; }
    public string Product { get; }
    public AcceptedLoanTerms Terms { get; }
    public DisbursementIntentState State { get; private set; }   // Pending, Disbursed
    public LoanId? LoanId { get; private set; }                  // Set when State is Disbursed.
}
```

## Public contract

All public types below live in the `Credere.Modules.Servicing` assembly. Commands implement `Credere.Shared.Commands.ICommand`; events implement `IIntegrationEvent` and carry `[Version(1)]`. Ids are `readonly record struct`s. Dates are NodaTime `LocalDate`; the host supplies every date explicitly.

```csharp
public readonly record struct LoanId(Guid Value);
public readonly record struct PaymentId(Guid Value);
public readonly record struct DisbursementIntentId(Guid Value);

public enum LoanState { Disbursed, Active, InGrace, PastDue, Defaulted, WrittenOff, Restructured, Recovered, PaidOff }
public enum RepaymentType { Annuity, Differentiated, InterestOnlyBullet }
public enum DisbursementIntentState { Pending, Disbursed }
```

### Facade

```csharp
public interface IServicingModule
{
    Task<Result<DisbursementIntentView, DomainError>> RegisterDisbursementIntentAsync(
        RegisterDisbursementIntent command, CancellationToken cancellationToken);
    Task<Result<LoanId, DomainError>> DisburseAsync(Disburse command, CancellationToken cancellationToken);
    Task<Result<Unit, DomainError>> ActivateAsync(Activate command, CancellationToken cancellationToken);
    Task<Result<AccrualResult, DomainError>> AccrueInterestAsync(AccrueInterest command, CancellationToken cancellationToken);
    Task<Result<PaymentResult, DomainError>> RecordPaymentAsync(RecordPayment command, CancellationToken cancellationToken);
    Task<Result<AgingResult, DomainError>> ApplyAgingAsync(ApplyAging command, CancellationToken cancellationToken);
    Task<Result<PayoffQuote, DomainError>> QuotePayoffAsync(QuotePayoff query, CancellationToken cancellationToken);
    Task<Result<PayoffResult, DomainError>> ExecutePayoffAsync(ExecutePayoff command, CancellationToken cancellationToken);
    Task<Result<Unit, DomainError>> WriteOffAsync(WriteOff command, CancellationToken cancellationToken);
    Task<Result<Unit, DomainError>> RestructureAsync(Restructure command, CancellationToken cancellationToken);
    Task<Result<Unit, DomainError>> RecoverAsync(Recover command, CancellationToken cancellationToken);

    Task<LoanView?> GetAsync(LoanId id, CancellationToken cancellationToken);
    Task<AmortizationSchedule?> GetScheduleAsync(LoanId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<LoanView>> ListAsync(LoanListFilters filters, CancellationToken cancellationToken);
    Task<DisbursementIntentView?> GetDisbursementIntentAsync(DisbursementIntentId id, CancellationToken cancellationToken);
}
```

`AmortizationSchedule`, `Money`, `Percentage`, `LoanTerm` and `PaymentFrequency` are `Credere.Core` types. A missing loan returns `NotFound` from a `Result` method and `null` from a query.

### Commands and queries

```csharp
public sealed record RegisterDisbursementIntent(
    Guid CommandId,
    Guid SourceReference,
    int AcceptedTermsVersion,
    string Product,
    AcceptedLoanTerms Terms,
    Guid CorrelationId) : ICommand;

public sealed record AcceptedLoanTerms(Money Principal, Percentage NominalRate, LoanTerm Term);

public sealed record Disburse(
    Guid CommandId,
    DisbursementIntentId IntentId,
    LocalDate DisbursementDate,
    LocalDate FirstPaymentDate,
    Guid CorrelationId) : ICommand;

public sealed record Activate(Guid CommandId, LoanId Id, LocalDate EffectiveDate, Guid CorrelationId) : ICommand;

public sealed record AccrueInterest(Guid CommandId, LoanId Id, LocalDate BusinessDate, Guid CorrelationId) : ICommand;

public sealed record RecordPayment(
    Guid CommandId, LoanId Id, Money Amount, LocalDate EffectiveDate, Guid CorrelationId) : ICommand;

public sealed record ApplyAging(
    Guid CommandId, LoanId Id, LocalDate BusinessDate, int DaysPastDue, Guid CorrelationId) : ICommand;

public sealed record QuotePayoff(LoanId Id, LocalDate PayoffDate);   // A query: no CommandId, no receipt.

public sealed record ExecutePayoff(
    Guid CommandId, LoanId Id, LocalDate PayoffDate, Money QuotedTotal, Guid CorrelationId) : ICommand;

public sealed record WriteOff(
    Guid CommandId, LoanId Id, LocalDate BusinessDate, string Reason, Guid CorrelationId) : ICommand;

public sealed record Restructure(
    Guid CommandId, LoanId Id, LocalDate BusinessDate, string Plan, Guid CorrelationId) : ICommand;

public sealed record Recover(
    Guid CommandId, LoanId Id, LocalDate BusinessDate, string Method, Money RecoveredAmount, Guid CorrelationId) : ICommand;

public sealed record LoanListFilters(
    IReadOnlyList<LoanState>? States, string? Product, LocalDate? DisbursedFrom, LocalDate? DisbursedTo);
```

A handler validates its command and returns a `ValidationError` for a bad field (an empty `Reason`, a non-positive `Amount`, a `FirstPaymentDate` that `ScheduleRequest.Create` rejects). The command records are plain DTOs; they have no invariants of their own.

### Results and views

```csharp
public sealed record DisbursementIntentView(
    DisbursementIntentId Id,
    Guid SourceReference,
    int AcceptedTermsVersion,
    string Product,
    AcceptedLoanTerms Terms,
    DisbursementIntentState State,
    LoanId? LoanId);

public sealed record AccrualResult(LocalDate AccruedThroughDate, Money Accrued, bool Changed);

public sealed record PaymentResult(PaymentId PaymentId, PaymentAllocation Allocation, LoanState State);

public sealed record AgingResult(LoanState State, int? DaysPastDue, bool Changed);

public sealed record PayoffQuote(
    LoanId Id,
    LocalDate PayoffDate,
    Money Principal,
    Money PastDueInterest,
    Money CurrentInterest,
    Money Fees,
    Money Total);

public sealed record PayoffResult(PaymentId PaymentId, PaymentAllocation Allocation);

public sealed record LoanView(
    LoanId Id,
    Guid SourceReference,
    int TermsVersion,
    string Product,
    Money Principal,
    Money RemainingPrincipal,
    Money FeesOutstanding,
    Money PastDueInterest,
    Money CurrentInterest,
    Percentage NominalRate,
    string DayCountConvention,
    LoanTerm Term,
    PaymentFrequency Frequency,
    RepaymentType Repayment,
    LocalDate DisbursementDate,
    LocalDate FirstPaymentDate,
    LocalDate AccruedThroughDate,
    int PastDueThreshold,
    int DefaultThreshold,
    LoanState State,
    int? DaysPastDue);
```

`PaymentAllocation` is the public record in [payment-allocation.md](../domain/payment-allocation.md); its `Excess` field is the overpayment. `AccrualResult.Changed` and `AgingResult.Changed` are `false` for a no-op ([No-op or transition](../domain/loan-lifecycle.md#no-op-or-transition)). `DayCountConvention` is the convention's `Name`.

### Errors

Servicing returns the common errors of [ADR-0006](../adr/0006-error-semantics.md): `ValidationError`, `NotFound`, `InvalidStateTransition`, `ConcurrencyConflict` and `ConflictingTerms`. It adds one module error, public because the facade returns it:

```csharp
public sealed record PayoffQuoteStale(Money QuotedTotal, Money CurrentTotal)
    : DomainError("payoff-quote-stale", "The payoff quote no longer matches the loan.");
```

## Integration events published

Events live in `src/Credere.Modules.Servicing/IntegrationEvents/`. Every event has `Guid EventId` first (the outbox row id) and `Guid CorrelationId` last.

```csharp
[Version(1)] public sealed record LoanDisbursed(
    Guid EventId, LoanId LoanId, Guid SourceReference, int AcceptedTermsVersion, string Product,
    Money Principal, Percentage NominalRate, string DayCountConvention, LoanTerm Term,
    PaymentFrequency Frequency, RepaymentType Repayment,
    LocalDate DisbursementDate, LocalDate FirstPaymentDate, Guid CorrelationId) : IIntegrationEvent;

[Version(1)] public sealed record LoanActivated(
    Guid EventId, LoanId LoanId, LocalDate EffectiveDate, Guid CorrelationId) : IIntegrationEvent;

[Version(1)] public sealed record PaymentRecorded(
    Guid EventId, LoanId LoanId, PaymentId PaymentId, LocalDate EffectiveDate, Money Amount,
    PaymentAllocation Allocation, LoanState State, Guid CorrelationId) : IIntegrationEvent;

[Version(1)] public sealed record PaymentExcessRecorded(
    Guid EventId, LoanId LoanId, PaymentId PaymentId, LocalDate EffectiveDate, Money Excess,
    Guid CorrelationId) : IIntegrationEvent;

[Version(1)] public sealed record LoanAged(
    Guid EventId, LoanId LoanId, LocalDate BusinessDate, LoanState PreviousState, LoanState NewState,
    int? DaysPastDue, Guid CorrelationId) : IIntegrationEvent;

[Version(1)] public sealed record LoanPaidOff(
    Guid EventId, LoanId LoanId, PaymentId FinalPaymentId, LocalDate PaidOffOn, Guid CorrelationId) : IIntegrationEvent;

[Version(1)] public sealed record LoanWrittenOff(
    Guid EventId, LoanId LoanId, LocalDate BusinessDate, string Reason, Money WrittenOffBalance,
    Guid CorrelationId) : IIntegrationEvent;

[Version(1)] public sealed record LoanRestructured(
    Guid EventId, LoanId LoanId, LocalDate BusinessDate, string Plan, Guid CorrelationId) : IIntegrationEvent;

[Version(1)] public sealed record LoanRecovered(
    Guid EventId, LoanId LoanId, LocalDate BusinessDate, string Method, Money RecoveredAmount,
    Guid CorrelationId) : IIntegrationEvent;
```

| Event | When | Intended subscribers |
|---|---|---|
| `LoanDisbursed` | `DisburseAsync` succeeds | Documents (agreement PDF), Reporting |
| `LoanActivated` | `ActivateAsync` succeeds | Reporting |
| `PaymentRecorded` | `RecordPaymentAsync` or `ExecutePayoffAsync` succeeds | Documents (receipt), Reporting, Ai (early-warning signal) |
| `PaymentExcessRecorded` | A payment's `Excess` is positive | Reporting (the list of amounts to return) |
| `LoanAged` | `ApplyAgingAsync` changes the state or the days past due. Not published for a no-op. | Reporting, Ai (early warning, collections) |
| `LoanPaidOff` | The loan reaches zero balance through `RecordPaymentAsync` or `ExecutePayoffAsync` | Reporting |
| `LoanWrittenOff` | `WriteOffAsync` succeeds | Reporting |
| `LoanRestructured` | `RestructureAsync` succeeds | Reporting |
| `LoanRecovered` | `RecoverAsync` succeeds | Reporting |

Domain-only events (`LoanEnteredGracePeriod`, `LoanBecamePastDue`, `LoanDefaulted`, `LoanBecameActive`, `LoanPastDueDaysUpdated`, `PaymentReceived`, `PaymentExcessRecorded` as a domain event, `InterestAccrued`) stay `internal` and are listed in [the transition table](../domain/loan-lifecycle.md#transition-table). `InterestAccrued` is not published across modules in M2; it may be in a later milestone if agents need it.

## Integration events consumed

None. Servicing consumes no integration event and references no other module. The handoff arrives as the `RegisterDisbursementIntent` command, which the host builds from `ApplicationAccepted`.

## Handoff from Origination

Owner decision D4 ([ADR-0011](../adr/0011-m2-module-contracts.md)).

1. Origination publishes `ApplicationAccepted` after the borrower accepts the final terms ([origination.md](origination.md#integration-events-published)). The earlier catalog entry `ApplicationDecided` is not the handoff.
2. The **host** translates the event. A host-owned handler implements `IIntegrationEventHandler<ApplicationAccepted>` and calls `IServicingModule.RegisterDisbursementIntentAsync`. Hosts may reference both modules (`docs/architecture.md` §3, rule 5); no module references the other.
3. The mapping:

   | `RegisterDisbursementIntent` field | Source |
   |---|---|
   | `CommandId` | `ApplicationAccepted.EventId` |
   | `SourceReference` | `ApplicationAccepted.ApplicationId.Value` |
   | `AcceptedTermsVersion` | `ApplicationAccepted.TermsVersion` |
   | `Product` | `ApplicationAccepted.Product.Value` |
   | `Terms` | `ApplicationAccepted.AcceptedTerms` (principal, nominal rate, term) |
   | `CorrelationId` | `ApplicationAccepted.CorrelationId` |

4. The handler persists a `DisbursementIntent` in one transaction with its receipt. The intent is unique on `(SourceReference, AcceptedTermsVersion)`:
   - The same event delivered twice carries the same `CommandId`, so the receipt returns the stored outcome.
   - A second event for the same key with identical terms returns the existing intent.
   - A second event for the same key with different terms returns `ConflictingTerms`.
5. `DisburseAsync` takes the intent id, not the terms. It is the operator's or treasury's confirmation. It reads the terms and `AcceptedTermsVersion` from the intent, creates the loan, and marks the intent `Disbursed`. A second `Disburse` for an intent that already has a loan returns `InvalidStateTransition`.
6. In M2 the translating handler lives in the end-to-end test (`tests/Credere.Integration.Tests/M2/`, task M2-09), which runs it against the real outbox payload and the subscriber receipt. The production dispatcher, polling and retries are [M4](../roadmap.md#m4--reporting-module--batch--outbox); the handler moves into `Credere.Batch` then.

## Product configuration

A loan's product is a string key into `ServicingOptions.Products`. The product fixes what the accepted terms do not carry. At disbursement the values are copied onto the loan and never change.

| Field | Meaning | M2 supported values |
|---|---|---|
| `Repayment` | Which generator builds the schedule | `Annuity` (`AnnuityScheduleGenerator`), `Differentiated` (`DifferentiatedScheduleGenerator`), `InterestOnlyBullet` (`BulletScheduleGenerator` with `capitaliseInterest: false`) |
| `Frequency` | Payment frequency | `Monthly`, `Quarterly` (M1 `PaymentFrequency`) |
| `DayCountConvention` | Convention of the loan's `FixedRate` | One of the five built-in M1 conventions, by `Name` |
| `PastDueThreshold`, `DefaultThreshold` | Aging thresholds | Defaults 1 and 90 days |
| `Calendar` | Name of a business calendar | See [Business dates and calendars](#business-dates-and-calendars) |

**Supported in M2.** `FixedRate` only. Floating and tiered rates need reset tracking and a rate history, which are later work (Open items). Zero-coupon bullet (`capitaliseInterest: true`) is not supported, because capitalising interest changes the accrual and posting rules below. Amortising schedules come from the M1 generators; Servicing writes no schedule maths of its own.

**Pending owner decision:** which day-count convention, payment frequency and repayment type the M2 demo product uses. The contract does not depend on the answer. M2-03's accrual vectors need it, and the choice is the owner's under AGENTS.md §7.

## Business dates and calendars

Owner decision D5 ([ADR-0011](../adr/0011-m2-module-contracts.md)).

- **The host supplies every date.** `Activate.EffectiveDate`, `AccrueInterest.BusinessDate`, `RecordPayment.EffectiveDate`, `ApplyAging.BusinessDate`, `QuotePayoff.PayoffDate` and the others are explicit command or query fields. No handler or aggregate reads the clock. Replaying a batch for a past date is therefore the same call with that date.
- **Calendars are configuration.** `ServicingOptions.Calendars` maps a name to a set of non-business weekdays and a list of holiday `LocalDate`s. The module ships no jurisdiction data: a host configures its own, and a calendar with weekends only is valid.
- **`IBusinessCalendar`** is an `internal` interface in Servicing with one member, `bool IsBusinessDay(LocalDate date)`. The options-bound implementation is registered by `AddServicingModule`. `Activate` receives it as a parameter, so the aggregate stays pure.
- **Activation.** `Activate` succeeds only when `EffectiveDate` is the first business day after `DisbursementDate`. Any other date returns a `ValidationError` on `effectiveDate`.
- **Accrual is by calendar day.** Interest accrues on non-business days too. A batch that skips weekends loses nothing, because accrual is cumulative ([below](#interest-accrual-and-posting)).
- **Days past due** use the same calendar in the aging batch, which passes the resulting integer in ([loan-lifecycle.md](../domain/loan-lifecycle.md#days-past-due-semantics)).

## Persistence — `servicing` schema

| Table | Purpose |
|---|---|
| `Loans` | Current-state row per loan; RowVersion for optimistic concurrency. |
| `LoanEvents` | Append-only history keyed by `(StreamId, StreamVersion)`. |
| `DisbursementIntents` | The persisted handoff; unique on `(SourceReference, AcceptedTermsVersion)`. |
| `Outbox` | Integration events pending dispatch. |
| `LoanSchedules` | Row-per-schedule-row amortization schedule, a snapshot taken at disbursement. M2 does not regenerate it after a prepayment. |
| `AccrualLedger` | One row per accrual that changes the balance: `(LoanId, BusinessDate)` unique, period start, amount. Written by `AccrueInterest`. |
| `PaymentLedger` | One row per recorded payment or payoff: `PaymentId`, effective date, amount, the allocation fields and the excess. |
| `CommandReceipts_<Handler>` | One receipt table per command handler: `RegisterDisbursementIntent`, `Disburse`, `Activate`, `AccrueInterest`, `RecordPayment`, `ApplyAging`, `ExecutePayoff`, `WriteOff`, `Restructure`, `Recover`. |

Converters for `Money`, `Percentage`, `LoanTerm`, ids and `LocalDate` live in this module's `Infrastructure/` ([open question #8](../open-questions.md), plan decision D1).

## Invariants

- Principal, dates, rate, thresholds and product are immutable after disbursement. Changes require restructuring (which creates a new, linked loan — out of M2 scope).
- Every state transition emits exactly one domain event. A no-op is not a transition and emits none.
- Every allocated payment produces a conservation-balanced `PaymentAllocation`.
- The sum of `AccrualLedger` entries for a loan equals `CurrentInterest + PastDueInterest + (interest already allocated via payments)`.
- A loan is created from exactly one `DisbursementIntent`, and an intent produces at most one loan.
- `AccruedThroughDate` never decreases.

## Feature slices (M2 initial set)

```
Credere.Modules.Servicing/Application/Commands/
├── RegisterDisbursementIntent/
├── Disburse/
├── Activate/
├── AccrueInterest/
├── RecordPayment/            Internally delegates to PaymentAllocationWaterfall.
├── ApplyAging/
├── ExecutePayoff/
├── WriteOff/
├── Restructure/
└── Recover/

Credere.Modules.Servicing/Application/Queries/
├── GetLoan/
├── GetSchedule/
├── ListLoans/
├── GetDisbursementIntent/
└── QuotePayoff/
```

Each command folder holds the command record, handler, validator and result type. There is no `IntegrationEventHandlers/` folder in M2: the handoff handler belongs to the host.

## Interest accrual and posting

Owner decision D5 ([ADR-0011](../adr/0011-m2-module-contracts.md)). This is a Credere design decision. No regulatory source prescribes it, so M2-03 writes its reference vectors first and the owner approves them before any code.

Accrual is a command, `AccrueInterest`, which the daily batch (M4) and the end-to-end test call. It is not part of the aggregate's constructor and not part of `RecordPayment`, but `RecordPayment`, `ExecutePayoff` and `WriteOff` first accrue to their own date by the same rule.

**Segments.** An interest *segment* is an interval `[s, e)` over which the principal balance `B` is constant. The loan starts one segment at its disbursement date. A segment ends, and the next begins, at:

- every scheduled payment date of the schedule, and
- the effective date of a payment whose principal allocation is positive.

A payment that allocates nothing to principal does not end a segment. Interest accrues from the disbursement date, not the activation date.

**Rules.**

1. **Cumulative accrual.** For business date `D`, `Accrued(D) = Σ Calculate(Rate, Bᵢ, sᵢ, min(eᵢ, D))` over the segments with `sᵢ < D`, where `Calculate` is `IInterestCalculator.Calculate` and the open segment's end is `D`. The ledger row posted by `AccrueInterest(D)` is `Accrued(D) − Accrued(AccruedThroughDate)`, and `AccruedThroughDate` becomes `D`.
2. **One rounding per segment.** The only rounding is inside `IInterestCalculator.Calculate`: each call creates one `Money`, half to even ([rates.md](../domain/rates.md#iinterestcalculator--the-single-entry-point)). Ledger rows are differences of `Money` values, so they are exact. No daily rounding accumulates.
3. **Independent of the run dates.** `Accrued(D)` depends only on `D` and the segments, not on how often accrual ran. A batch that runs daily, weekly or only on `D` posts the same total. This is an acceptance property for M2-03.
4. **Idempotent by date.** Running `AccrueInterest` for a `BusinessDate` on or before `AccruedThroughDate` is a no-op (`Changed = false`, no ledger row, no event), also under a different `CommandId`. Re-running the batch for the same date is therefore safe.
5. **Past-due interest.** A scheduled payment date `P` has passed when the business date is after `P`; a payment on `P` is on time. When accrual crosses a passed `P`, the interest accrued through `P` that is still unpaid moves from `CurrentInterest` to `PastDueInterest`, and later interest starts a fresh `CurrentInterest`. Payments reduce `PastDueInterest` before `CurrentInterest` (the waterfall).
6. **Agreement with the schedule.** For a loan paid on schedule, the interest accrued over period `k` equals the schedule row's `InterestDue`, to the cent, because the schedule generator makes the same single `Calculate` call over the same period and balance. After a prepayment or a late payment the ledger governs and the schedule is not regenerated.
7. **No default interest and no compounding.** M2 posts no penalty interest and no interest on interest. `FeesOutstanding` stays zero.
8. **Eligible states.** Accrual runs in `Active`, `InGrace`, `PastDue` and `Defaulted`. In `Disbursed` it returns `InvalidStateTransition`; the first accrual after activation catches up from the disbursement date.

`InterestAccrued` is the internal domain event for a ledger row, and `AccrualLedger` is the ledger.

## Payoff quotes

Owner decision D5 ([ADR-0011](../adr/0011-m2-module-contracts.md)).

- `QuotePayoff(LoanId, PayoffDate)` returns the amount that closes the loan if paid on `PayoffDate`: principal, past-due interest, current interest accrued to `PayoffDate` by [the accrual rules](#interest-accrual-and-posting) assuming no other payment first, fees, and their `Total`. It writes nothing, and a quote is not stored.
- A quote is valid for `PayoffDate` only. `PayoffDate` before `AccruedThroughDate` is a `ValidationError` on `payoffDate`.
- `ExecutePayoff` **revalidates**: it recomputes the total for the same `PayoffDate` from the loan's current state and compares it with `QuotedTotal`. A difference, such as a payment recorded between quote and execution, returns `PayoffQuoteStale(QuotedTotal, CurrentTotal)` and changes nothing. The caller quotes again.
- On a match, `ExecutePayoff` records the payoff as a payment of `Total` through the waterfall. The excess is zero, all four balances reach zero, and the loan moves to `PaidOff`.
- No prepayment fee exists in M2. Calculations use `IInterestCalculator` only; no payoff-specific interest formula exists.

## Open items

Not in M2:

- Floating-rate reset tracking, tiered rates (M3 if time allows; otherwise M4).
- Zero-coupon bullet, which capitalises interest.
- Restructuring that creates a new linked loan (M4+).
- Early-repayment APRC recalculation (M7+).
- Fees, default interest, backdated payments, and the treasury process that returns an excess.

Open decisions for the owner are listed on the M2-C1 pull request: payments on `Defaulted` loans and cure from `Defaulted` ([loan-lifecycle.md](../domain/loan-lifecycle.md#not-decided)), and the demo product's day count, frequency and repayment type.
