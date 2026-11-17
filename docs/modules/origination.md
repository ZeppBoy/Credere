# Module — Origination

**Project:** `src/Credere.Modules.Origination/`
**Schema:** `origination`
**Public contract:** `IOriginationModule`

## Bounded context

The Origination module owns the pre-disbursement life of a loan application. An application represents the borrower's request plus the lender's response (approve / decline / request more info). The module is complete the moment an application is either declined or approved-and-handed-off; it does **not** own the servicing loan.

## Aggregates

### `LoanApplication` (internal)

```csharp
internal sealed class LoanApplication
{
    public LoanApplicationId Id { get; }
    public ApplicantSnapshot Applicant { get; }
    public ProductCode Product { get; }
    public RequestedTerms Terms { get; private set; }
    public int TermsVersion { get; private set; }
    public LoanApplicationState State { get; private set; }
    public UnderwritingDecision? Decision { get; private set; }
    public IReadOnlyList<LoanApplicationEvent> UncommittedEvents { get; }

    internal LoanApplication(SubmitApplication cmd);
    internal Result<Unit, DomainError> Decide(DecideApplication cmd);
    internal Result<Unit, DomainError> AcceptTerms(AcceptTerms cmd);
    internal Result<Unit, DomainError> Withdraw(WithdrawApplication cmd);
}
```

### States

- `Submitted` — applicant has submitted; underwriting not yet completed.
- `UnderReview` — being evaluated (used for long-running flows with partial info).
- `Approved` — underwriting approved; awaiting borrower acceptance of final terms.
- `AcceptedByBorrower` — final terms accepted; ready for disbursement handoff.
- `Declined` — terminal.
- `Withdrawn` — terminal.

## Public interface

```csharp
public interface IOriginationModule
{
    Task<Result<LoanApplicationId, DomainError>> SubmitAsync(
        SubmitApplication cmd, CancellationToken ct);

    Task<Result<DecisionView, DomainError>> DecideAsync(
        DecideApplication cmd, CancellationToken ct);

    Task<Result<Unit, DomainError>> AcceptTermsAsync(
        AcceptTerms cmd, CancellationToken ct);

    Task<Result<Unit, DomainError>> WithdrawAsync(
        WithdrawApplication cmd, CancellationToken ct);

    Task<ApplicationView?> GetAsync(LoanApplicationId id, CancellationToken ct);
    Task<IReadOnlyList<ApplicationView>> ListAsync(
        ApplicationListFilters filters, CancellationToken ct);
}
```

All commands carry a `CommandId` for idempotency (handled by the module's receipt table).

## Integration events published

| Event | When | Payload (summary) |
|---|---|---|
| `ApplicationSubmitted` | `SubmitAsync` succeeds | Id, applicant snapshot, product, requested terms, correlation id |
| `ApplicationDecided` | `DecideAsync` succeeds | Id, decision (Approve/Decline/HumanReview), rationale, terms (if approved) |
| `ApplicationAccepted` | `AcceptTermsAsync` succeeds | Id, accepted terms, acceptance timestamp |
| `ApplicationWithdrawn` | `WithdrawAsync` succeeds | Id, reason |

Events live in `src/Credere.Modules.Origination/IntegrationEvents/`.

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

## DTOs (public)

- `SubmitApplication { Guid CommandId, ApplicantSnapshot Applicant, ProductCode Product, RequestedTerms Terms, Guid CorrelationId }`
- `DecideApplication { Guid CommandId, LoanApplicationId Id, DecisionOutcome Outcome, string Rationale, DecidedTerms? FinalTerms, Guid CorrelationId }`
- `AcceptTerms { Guid CommandId, LoanApplicationId Id, int TermsVersion, Guid CorrelationId }`
- `WithdrawApplication { Guid CommandId, LoanApplicationId Id, string Reason, Guid CorrelationId }`
- `ApplicationView` — read-only projection.
- `ApplicationListFilters` — status, product, submitted-after, submitted-before.

## Invariants

- An approved application has a final `DecidedTerms`.
- Terms version is monotonically increasing; a previously-accepted terms version cannot be modified.
- Only an application in `AcceptedByBorrower` can be used for disbursement (the Servicing module enforces this via the handoff's idempotency key).
- A declined or withdrawn application is terminal and rejects every subsequent command.

## Feature slices (M2 initial set)

```
Credere.Modules.Origination/Application/Commands/
├── SubmitApplication/
├── DecideApplication/
├── AcceptTerms/
└── WithdrawApplication/
```

Each folder: command record, handler, validator, test.

## Open items

See [`../open-questions.md`](../open-questions.md) #1 for how Ai reads this module's data.
