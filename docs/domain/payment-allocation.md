# Payment allocation

**Credere allocates every payment from day one.** This is not a stub, not deferred, not a "future phase." The absence of payment allocation was the single largest gap in Promissio; Credere closes it by making allocation part of the first slice that touches payments.

Allocation lives in `src/Credere.Modules.Servicing/Domain/PaymentAllocationWaterfall.cs`. The Loan aggregate's `RecordPayment` command delegates to it; the aggregate itself does not contain allocation logic.

## Authoritative sources

- **ECB Statistical Classification of interest rates**, Annex II (payment allocation for consumer credit).
- **EU Consumer Credit Directive 2008/48/EC**, Article 10 (information in credit agreement, including order of allocation).
- **Industry practice** (retail banking): fees → past-due interest → current interest → principal.

Cite these in the XML comment on `PaymentAllocationWaterfall.cs` with the specific article / section references.

## Waterfall (order of allocation)

For an incoming payment of amount `P` in currency `C`, the waterfall allocates in this order until `P` is exhausted or the loan balance is zero:

1. **Outstanding fees** (late fees, NSF fees, other contractual fees). Reduce `FeesOutstanding` first.
2. **Past-due interest** — interest accrued in a prior scheduled period that remains unpaid. Reduce `PastDueInterest`.
3. **Current period interest** — interest accrued in the current period (since the last scheduled payment date, up to the effective date of this payment). Reduce `CurrentInterest`.
4. **Principal** — the remainder reduces `RemainingPrincipal`.

Any residual after principal is zero is **returned to the payer** via a `PaymentExcess` domain event (and corresponding integration event). The aggregate does not keep overpayments on the loan.

## Public contract

```csharp
public sealed record PaymentAllocation(
    Money FeesAllocated,
    Money PastDueInterestAllocated,
    Money CurrentInterestAllocated,
    Money PrincipalAllocated,
    Money Excess,
    Money NewFeesOutstanding,
    Money NewPastDueInterest,
    Money NewCurrentInterest,
    Money NewRemainingPrincipal);

internal static class PaymentAllocationWaterfall
{
    public static PaymentAllocation Allocate(
        Money payment,
        Money feesOutstanding,
        Money pastDueInterest,
        Money currentInterest,
        Money remainingPrincipal);
}
```

**Visibility.** `PaymentAllocationWaterfall` is `internal` to `Credere.Modules.Servicing`. The result type `PaymentAllocation` is `public` because it is part of the `PaymentRecorded` integration event payload.

## Formal rules

Let `p = payment.Amount`. In order:

```
fee_allocated         = min(p, fees_outstanding)
p                    −= fee_allocated

past_interest_allocated = min(p, past_due_interest)
p                      −= past_interest_allocated

current_interest_allocated = min(p, current_interest)
p                         −= current_interest_allocated

principal_allocated = min(p, remaining_principal)
p                  −= principal_allocated

excess = p
```

Resulting new balances:

```
new_fees_outstanding   = fees_outstanding - fee_allocated
new_past_due_interest  = past_due_interest - past_interest_allocated
new_current_interest   = current_interest - current_interest_allocated
new_remaining_principal = remaining_principal - principal_allocated
```

## Invariants

Enforced by `PaymentAllocation`'s constructor and verified by unit + property tests:

- All amounts are in the same currency as the payment.
- Sum of allocated amounts + excess = payment amount.
- Each allocated amount is non-negative.
- Each new-balance is non-negative.
- Sum of new balances + sum of allocated interest + principal = starting balances - interest accrued paid off. (See test matrix for exact algebraic form.)

## Business rules on top of the waterfall

- A payment currency mismatch returns `Result<...>.Failure("Currency mismatch")`. No partial allocation attempted.
- A zero or negative payment amount returns `Result<...>.Failure("Payment must be positive")`.
- A payment to a loan in a terminal state (`WrittenOff`, `Restructured`, `Recovered`, `PaidOff`) returns `Result<...>.Failure("Loan is in terminal state X")`. The aggregate does not throw.
- A payment that would exactly close the balance (principal = 0 after allocation, no other outstanding items) triggers a `LoanPaidOff` domain event in addition to `PaymentReceived`, and transitions the loan to `PaidOff`.

## Reference examples

The test class carries at least five reference cases from the ECB Annex II illustrative examples, with explicit input balances, payment amount, and expected allocation. Example:

```
Starting: fees=10, pastInt=25, currentInt=15, principal=1000
Payment:  40

Expected:
  feeAlloc=10, pastIntAlloc=25, currentIntAlloc=5, principalAlloc=0, excess=0
  new: fees=0, pastInt=0, currentInt=10, principal=1000
```

## Property-based invariants (FsCheck.Xunit)

- **Conservation.** `fee + pastInt + currentInt + principal + excess == payment` exactly (banker's rounding is applied per field; sum is identical to the input).
- **Priority.** If any of `fees`, `pastInt`, `currentInt` remains positive after allocation, then principal must be unchanged. (Can't skip the waterfall.)
- **Excess only when fully paid.** `excess > 0 ⇒ all four new-balances == 0`.
- **No excess when under-paying.** `payment < sum(fees, pastInt, currentInt, principal) ⇒ excess == 0`.

## Why this lives in the module, not the aggregate

The Loan aggregate's responsibility is state and events. Allocation is a pure function of inputs — perfect to isolate for testing, mutation testing, and readability. The aggregate calls the waterfall, applies the result to its state fields, and emits events. A corrupt waterfall is a single file to fix; the aggregate code reads like English.
