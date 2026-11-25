# Payment allocation

**Credere allocates every payment from day one.** This is not a stub, not deferred, not a "future phase." The absence of payment allocation was the single largest gap in Promissio; Credere closes it by making allocation part of the first slice that touches payments.

Allocation lives in `src/Credere.Modules.Servicing/Domain/PaymentAllocationWaterfall.cs`. The Loan aggregate's `RecordPayment` command delegates to it; the aggregate itself does not contain allocation logic.

## Source for the allocation rule

The [owner's D2 decision on M2-02](https://github.com/ZeppBoy/Credere/issues/69#issuecomment-7167387870) establishes the formal rules below as Credere's product rule. The reference cases are derived from those rules.

[Directive (EU) 2023/2225, Article 21(1)(h)](https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX%3A32023L2225) requires consumer credit agreements to disclose the order of allocation to balances charged at different borrowing rates where appropriate. The priority and examples on this page come from the product rule.

## Waterfall (order of allocation)

For an incoming payment of amount `P` in currency `C`, the waterfall allocates in this order until `P` is exhausted or the loan balance is zero:

1. **Outstanding fees** (late fees, NSF fees, other contractual fees). Reduce `FeesOutstanding` first.
2. **Past-due interest** — interest accrued in a prior scheduled period that remains unpaid. Reduce `PastDueInterest`.
3. **Current period interest** — interest accrued in the current period (since the last scheduled payment date, up to the effective date of this payment). Reduce `CurrentInterest`.
4. **Principal** — the remainder reduces `RemainingPrincipal`.

Any residual after all four balances reach zero is `Excess`. The aggregate records it for return in `PaymentRecorded` and emits the domain and integration events `PaymentExcessRecorded`. The loan keeps a zero balance. Treasury handles the return outside M2. See [overpayments in the loan lifecycle](loan-lifecycle.md#payments-and-accrual).

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

The waterfall is responsible for these invariants:

- All amounts are in the same currency as the payment.
- Sum of allocated amounts + excess = payment amount.
- Each allocated amount is non-negative.
- Each remaining balance is non-negative.
- For each balance, the allocated amount plus the remaining balance equals the starting balance.

## Business rules on top of the waterfall

- `RecordPayment` is available in `Active`, `InGrace` and `PastDue`. Other states return `InvalidStateTransition`.
- A payment must be positive, match the loan's currency and have an effective date on or after `AccruedThroughDate`.
- The aggregate accrues interest to the effective date before allocating the payment.
- A payment that leaves all four balances at zero moves the loan to `PaidOff` and emits `PaymentReceived` and `LoanPaidOff`. A positive excess also emits `PaymentExcessRecorded`.

## Reference examples

These cases apply the [formal rules](#formal-rules). PA-01 through PA-09 share the starting balances of the original example, PA-05, whose payment was 40.00. For those balances the four stages are fully covered at cumulative payments of 10, 35, 50 and 1050. PA-09 adds one cent, the smallest unit of [Money](value-objects.md). All amounts are in EUR. Balance and allocation tuples are ordered as fees, past-due interest, current interest, principal.

| Case | Payment | Starting balances | Expected allocation | Expected excess | Expected remaining balances |
|---|---:|---|---|---:|---|
| PA-01 · Partial fees | 5.00 | (10.00, 25.00, 15.00, 1000.00) | (5.00, 0.00, 0.00, 0.00) | 0.00 | (5.00, 25.00, 15.00, 1000.00) |
| PA-02 · Exact fees | 10.00 | (10.00, 25.00, 15.00, 1000.00) | (10.00, 0.00, 0.00, 0.00) | 0.00 | (0.00, 25.00, 15.00, 1000.00) |
| PA-03 · Partial past-due interest | 20.00 | (10.00, 25.00, 15.00, 1000.00) | (10.00, 10.00, 0.00, 0.00) | 0.00 | (0.00, 15.00, 15.00, 1000.00) |
| PA-04 · Exact fees and past-due interest | 35.00 | (10.00, 25.00, 15.00, 1000.00) | (10.00, 25.00, 0.00, 0.00) | 0.00 | (0.00, 0.00, 15.00, 1000.00) |
| PA-05 · Partial current interest | 40.00 | (10.00, 25.00, 15.00, 1000.00) | (10.00, 25.00, 5.00, 0.00) | 0.00 | (0.00, 0.00, 10.00, 1000.00) |
| PA-06 · Exact fees and interest | 50.00 | (10.00, 25.00, 15.00, 1000.00) | (10.00, 25.00, 15.00, 0.00) | 0.00 | (0.00, 0.00, 0.00, 1000.00) |
| PA-07 · Partial principal | 100.00 | (10.00, 25.00, 15.00, 1000.00) | (10.00, 25.00, 15.00, 50.00) | 0.00 | (0.00, 0.00, 0.00, 950.00) |
| PA-08 · Exact payoff | 1050.00 | (10.00, 25.00, 15.00, 1000.00) | (10.00, 25.00, 15.00, 1000.00) | 0.00 | (0.00, 0.00, 0.00, 0.00) |
| PA-09 · One cent of excess | 1050.01 | (10.00, 25.00, 15.00, 1000.00) | (10.00, 25.00, 15.00, 1000.00) | 0.01 | (0.00, 0.00, 0.00, 0.00) |
| PA-10 · Empty priority buckets, using the balance after PA-06 | 0.01 | (0.00, 0.00, 0.00, 1000.00) | (0.00, 0.00, 0.00, 0.01) | 0.00 | (0.00, 0.00, 0.00, 999.99) |
| PA-11 · Empty past-due interest between two non-zero balances | 20.00 | (10.00, 0.00, 15.00, 1000.00) | (10.00, 0.00, 10.00, 0.00) | 0.00 | (0.00, 0.00, 5.00, 1000.00) |
| PA-12 · Uneven cents split between current interest and principal | 0.50 | (0.00, 0.00, 0.37, 100.00) | (0.00, 0.00, 0.37, 0.13) | 0.00 | (0.00, 0.00, 0.00, 99.87) |

## Property-based invariants (FsCheck.Xunit)

- **Conservation.** `fee + pastInt + currentInt + principal + excess == payment` exactly. Inputs are already expressed in cents.
- **Priority.** If any balance remains positive after allocation, all lower-priority allocations are zero.
- **Excess only when fully paid.** `excess > 0 ⇒ all four new-balances == 0`.
- **No excess when under-paying.** `payment < sum(fees, pastInt, currentInt, principal) ⇒ excess == 0`.

## Why this lives in the module, not the aggregate

The Loan aggregate's responsibility is state and events. Allocation is a pure function of inputs — perfect to isolate for testing, mutation testing, and readability. The aggregate calls the waterfall, applies the result to its state fields, and emits events. A corrupt waterfall is a single file to fix; the aggregate code reads like English.
