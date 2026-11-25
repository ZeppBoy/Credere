# Module — Origination

**Project:** `src/Credere.Modules.Origination/`
**Schema:** `origination`
**Public contract:** `IOriginationModule`
**Status:** The contract sections below are Planned (M2). They were written in M2-C1 ([ADR-0011](../adr/0011-m2-module-contracts.md)) and land as code in M2-C2 and later. Today `IOriginationModule` is an empty interface.

## Bounded context

The Origination module owns the pre-disbursement life of a loan application. An application represents the borrower's request plus the lender's response (approve / decline / request more info). The module is complete the moment an application is either declined or accepted by the borrower and handed off; it does **not** own the servicing loan.

## Aggregates

### `LoanApplication` (internal)

```csharp
internal sealed class LoanApplication
{
    public LoanApplicationId Id { get; }
    public ApplicantSnapshot Applicant { get; }
    public ProductCode Product { get; }
    public RequestedTerms Terms { get; }
    public int TermsVersion { get; private set; }            // 0 until the first approval; +1 for every approval.
    public DecidedTerms? DecidedTerms { get; private set; }  // Set by an approval.
    public LoanApplicationState State { get; private set; }
    public UnderwritingDecision? Decision { get; private set; }
    public IReadOnlyList<LoanApplicationEvent> UncommittedEvents { get; }

    internal static Result<LoanApplication, DomainError> Submit(SubmitApplication cmd, LoanApplicationId id, Instant now);
    internal Result<Unit, DomainError> Decide(DecideApplication cmd, Instant now);
    internal Result<Unit, DomainError> AcceptTerms(AcceptTerms cmd, Instant now);
    internal Result<Unit, DomainError> Withdraw(WithdrawApplication cmd, Instant now);
}
```

The aggregate never reads the clock: the handler takes `IClock` from DI and passes the `Instant`.

### States and transitions

- `Submitted` — applicant has submitted; underwriting not yet completed.
- `UnderReview` — a human review was requested; underwriting continues.
- `Approved` — underwriting approved; awaiting borrower acceptance of the decided terms.
- `AcceptedByBorrower` — decided terms accepted; the handoff event has been published. Terminal for this module.
- `Declined` — terminal.
- `Withdrawn` — terminal.

| From | Command | Guard | To | Domain event | Integration event |
|---|---|---|---|---|---|
| (none) | Submit | the command validates | Submitted | `ApplicationSubmitted` | `ApplicationSubmitted` |
| Submitted / UnderReview | Decide | `Outcome` is `Approve`; `FinalTerms` present | Approved (`TermsVersion` + 1) | `ApplicationApproved` | `ApplicationDecided` |
| Submitted / UnderReview | Decide | `Outcome` is `Decline`; `FinalTerms` absent | Declined | `ApplicationDeclined` | `ApplicationDecided` |
| Submitted | Decide | `Outcome` is `HumanReview`; `FinalTerms` absent | UnderReview | `ApplicationSentToReview` | `ApplicationDecided` |
| Approved | AcceptTerms | `TermsVersion` equals the current version | AcceptedByBorrower | `ApplicationAccepted` | `ApplicationAccepted` |
| Submitted / UnderReview / Approved | Withdraw | reason non-empty | Withdrawn | `ApplicationWithdrawn` | `ApplicationWithdrawn` |

Any other `(state, command)` pair returns `InvalidStateTransition`, with the state and command names as strings. A declined or withdrawn application rejects every command. An `AcceptTerms` carrying a version other than the current one returns `StaleTermsVersion`, so a borrower never accepts terms an underwriter has since replaced. An accepted application cannot be withdrawn: the handoff is already under way.

## Public contract

All public types live in the `Credere.Modules.Origination` assembly. Commands implement `Credere.Shared.Commands.ICommand`; events implement `IIntegrationEvent` and carry `[Version(1)]`.

```csharp
public readonly record struct LoanApplicationId(Guid Value);

public enum LoanApplicationState { Submitted, UnderReview, Approved, AcceptedByBorrower, Declined, Withdrawn }
public enum DecisionOutcome { Approve, Decline, HumanReview }

public sealed record ProductCode
{
    public string Value { get; }
    public static Result<ProductCode, DomainError> Create(string value);   // 1-32 characters of A-Z, 0-9, '-' and '_'.
}

public sealed record ApplicantSnapshot(
    Guid ApplicantId,
    string FullName,
    LocalDate DateOfBirth,
    string CountryCode,            // ISO 3166-1 alpha-2.
    Money MonthlyIncome,
    Money MonthlyDebtPayments);

public sealed record RequestedTerms(Money Principal, LoanTerm Term);

public sealed record DecidedTerms(Money Principal, Percentage NominalRate, LoanTerm Term);
```

`ApplicantSnapshot` carries personal data. It stays out of logs and source (AGENTS.md §10). The fields are the ones the credit-decisioning rules read: country, income and debt payments for the DTI rule ([agents.md](../ai/agents.md)). The repayment type, payment frequency, day count and aging thresholds are product attributes configured in Servicing ([servicing.md](servicing.md#product-configuration)), so the application's terms hold only the amount, rate and term.

### Facade

```csharp
public interface IOriginationModule
{
    Task<Result<LoanApplicationId, DomainError>> SubmitAsync(
        SubmitApplication command, CancellationToken cancellationToken);
    Task<Result<DecisionView, DomainError>> DecideAsync(
        DecideApplication command, CancellationToken cancellationToken);
    Task<Result<Unit, DomainError>> AcceptTermsAsync(
        AcceptTerms command, CancellationToken cancellationToken);
    Task<Result<Unit, DomainError>> WithdrawAsync(
        WithdrawApplication command, CancellationToken cancellationToken);

    Task<ApplicationView?> GetAsync(LoanApplicationId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApplicationView>> ListAsync(
        ApplicationListFilters filters, CancellationToken cancellationToken);
}
```

All commands carry a `CommandId` for idempotency (handled by the module's receipt table, [ADR-0007](../adr/0007-command-idempotency.md)). A missing application returns `NotFound` from a `Result` method and `null` from a query.

### Commands, queries and views

```csharp
public sealed record SubmitApplication(
    Guid CommandId,
    ApplicantSnapshot Applicant,
    ProductCode Product,
    RequestedTerms Terms,
    Guid CorrelationId) : ICommand;

public sealed record DecideApplication(
    Guid CommandId,
    LoanApplicationId Id,
    DecisionOutcome Outcome,
    string Rationale,
    DecidedTerms? FinalTerms,        // Required for Approve; absent for Decline and HumanReview.
    Guid CorrelationId) : ICommand;

public sealed record AcceptTerms(
    Guid CommandId, LoanApplicationId Id, int TermsVersion, Guid CorrelationId) : ICommand;

public sealed record WithdrawApplication(
    Guid CommandId, LoanApplicationId Id, string Reason, Guid CorrelationId) : ICommand;

public sealed record ApplicationListFilters(
    IReadOnlyList<LoanApplicationState>? States,
    ProductCode? Product,
    Instant? SubmittedAfter,
    Instant? SubmittedBefore);

public sealed record DecisionView(
    LoanApplicationId Id,
    DecisionOutcome Outcome,
    string Rationale,
    DecidedTerms? FinalTerms,
    int TermsVersion,
    LoanApplicationState State);

public sealed record ApplicationView(
    LoanApplicationId Id,
    ApplicantSnapshot Applicant,
    ProductCode Product,
    RequestedTerms RequestedTerms,
    DecidedTerms? DecidedTerms,
    int TermsVersion,
    LoanApplicationState State,
    DecisionOutcome? LastOutcome,
    string? LastRationale,
    Instant SubmittedAt);
```

The handler validates a command and returns a `ValidationError` for a bad field: an `Approve` without `FinalTerms`, a `Decline` with them, an empty `Rationale` or `Reason`, a non-positive principal, a `NominalRate` above 100%, or an unknown country code. The command records have no invariants of their own.

### Errors

Origination returns the common errors of [ADR-0006](../adr/0006-error-semantics.md) (`ValidationError`, `NotFound`, `InvalidStateTransition`, `ConcurrencyConflict`, `ConflictingTerms`) and one module error, public because the facade returns it:

```csharp
public sealed record StaleTermsVersion(int Expected, int Actual)
    : DomainError("stale-terms-version", "The terms were replaced after the version the borrower saw.");
```

## Integration events published

Events live in `src/Credere.Modules.Origination/IntegrationEvents/`. Every event has `Guid EventId` first (the outbox row id) and `Guid CorrelationId` last. Event timestamps come from the handler's `IClock`.

```csharp
[Version(1)] public sealed record ApplicationSubmitted(
    Guid EventId, LoanApplicationId ApplicationId, ApplicantSnapshot Applicant, ProductCode Product,
    RequestedTerms Terms, Instant SubmittedAt, Guid CorrelationId) : IIntegrationEvent;

[Version(1)] public sealed record ApplicationDecided(
    Guid EventId, LoanApplicationId ApplicationId, DecisionOutcome Outcome, string Rationale,
    DecidedTerms? FinalTerms, int? TermsVersion, Instant DecidedAt, Guid CorrelationId) : IIntegrationEvent;

[Version(1)] public sealed record ApplicationAccepted(
    Guid EventId, LoanApplicationId ApplicationId, ProductCode Product, int TermsVersion,
    DecidedTerms AcceptedTerms, Instant AcceptedAt, Guid CorrelationId) : IIntegrationEvent;

[Version(1)] public sealed record ApplicationWithdrawn(
    Guid EventId, LoanApplicationId ApplicationId, string Reason, Instant WithdrawnAt,
    Guid CorrelationId) : IIntegrationEvent;
```

| Event | When | Intended subscribers |
|---|---|---|
| `ApplicationSubmitted` | `SubmitAsync` succeeds | Ai (credit decisioning, optional), Reporting |
| `ApplicationDecided` | `DecideAsync` succeeds, for any outcome | Reporting |
| `ApplicationAccepted` | `AcceptTermsAsync` succeeds | Servicing (the handoff, through the host: [servicing.md](servicing.md#handoff-from-origination)), Reporting |
| `ApplicationWithdrawn` | `WithdrawAsync` succeeds | Reporting |

`ApplicationDecided` is no longer the handoff event. Servicing starts from `ApplicationAccepted`, so a disbursement never begins from an approval the borrower has not accepted.

## Integration events consumed

None in M2. The module may subscribe to `AgentRecommendationProduced` in M6 to attach AI credit recommendations to an application (handled by a cross-cutting observer rather than inside Origination's domain).

## Persistence — `origination` schema

| Table | Purpose |
|---|---|
| `LoanApplications` | Current-state row per application; RowVersion for optimistic concurrency. |
| `LoanApplicationEvents` | Append-only history keyed by `(StreamId, StreamVersion)`. |
| `Outbox` | Integration events pending dispatch. |
| `CommandReceipts_SubmitApplication` | Idempotency receipts per handler (one table per handler). |
| `CommandReceipts_DecideApplication` | " |
| `CommandReceipts_AcceptTerms` | " |
| `CommandReceipts_WithdrawApplication` | " |

Converters for `Money`, `Percentage`, `LoanTerm`, ids and NodaTime types live in this module's `Infrastructure/` ([open question #8](../open-questions.md), plan decision D1).

## Invariants

- An approved application has final `DecidedTerms`, and `TermsVersion` is at least 1.
- Terms version is monotonically increasing; a previously-accepted terms version cannot be modified, because an accepted application accepts no further command.
- `ApplicationAccepted` is published exactly once per application, and only from `Approved`. Servicing therefore sees an application only after the borrower has accepted it.
- A declined or withdrawn application is terminal and rejects every subsequent command.

## Feature slices (M2 initial set)

```
Credere.Modules.Origination/Application/Commands/
├── SubmitApplication/
├── DecideApplication/
├── AcceptTerms/
└── WithdrawApplication/

Credere.Modules.Origination/Application/Queries/
├── GetApplication/
└── ListApplications/
```

Each folder: command or query record, handler, validator, result or view type, test.

## Open items

See [`../open-questions.md`](../open-questions.md) #1 for how Ai reads this module's data.
