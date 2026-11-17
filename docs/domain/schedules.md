# Amortization schedules

Credere generates three kinds of amortization schedule from the shared kernel. All live in `src/Credere.Core/Schedules/`. All produce a canonical `AmortizationSchedule` record validated by the aggregate before it can be stored on a Loan.

## Public contract

```csharp
public interface IScheduleGenerator
{
    AmortizationSchedule Generate(ScheduleRequest request);
}

public sealed record ScheduleRequest(
    Money Principal,
    InterestRate Rate,
    LoanTerm Term,
    LocalDate DisbursementDate,
    LocalDate FirstPaymentDate,
    Frequency PaymentFrequency);     // Monthly in M1; quarterly allowed as a non-primary case.

public sealed record AmortizationSchedule(IReadOnlyList<ScheduleRow> Rows);

public sealed record ScheduleRow(
    int Index,
    LocalDate PaymentDate,
    Money OpeningBalance,
    Money InterestDue,
    Money PrincipalDue,
    Money TotalPayment,
    Money ClosingBalance);
```

Invariants (enforced by `AmortizationSchedule`'s constructor):

- `Rows` is non-empty.
- `Rows[0].OpeningBalance == Principal`.
- `Rows[^1].ClosingBalance == Money.Zero(Principal.Currency)` within rounding tolerance.
- For each row: `OpeningBalance - PrincipalDue == ClosingBalance` and `InterestDue + PrincipalDue == TotalPayment`.
- For each adjacent pair: `Rows[i].ClosingBalance == Rows[i+1].OpeningBalance`.
- Dates are monotonically increasing.

## 1. Annuity

`AnnuityScheduleGenerator`. Equal `TotalPayment` across all rows except (optionally) the last, which absorbs the rounding residual.

**Formula.** For a loan with principal `P`, periodic rate `r = rate / periodsPerYear`, and `n` payments:

```
payment = P × r × (1 + r)^n / ((1 + r)^n − 1)
```

For each period `i = 1..n`:

```
interestDueᵢ = openingBalanceᵢ × r
principalDueᵢ = payment − interestDueᵢ
closingBalanceᵢ = openingBalanceᵢ − principalDueᵢ
```

**Edge cases.**
- Zero rate: `payment = P / n`, interest is zero, principal is equal across periods.
- Floating rate: generated at origination against the rate active at origination; re-generated on reset dates (M2+ once reset tracking lands).

**Rounding policy.** `Money` rounds each amount to two decimals. The last row absorbs the residual — its `TotalPayment` can differ from the others by up to `0.01 × (n − 1)` in theory, in practice less. The residual direction is deterministic (banker's rounding applied consistently), so the schedule is stable under re-generation.

## 2. Differentiated (Decreasing)

`DifferentiatedScheduleGenerator`. Equal `PrincipalDue` across all rows; `InterestDue` decreases as the balance drops.

**Formula.** For each period `i = 1..n`:

```
principalDueᵢ = P / n          (same for every row; last row absorbs residual)
openingBalanceᵢ = P − (i − 1) × principalDueᵢ
interestDueᵢ = openingBalanceᵢ × r
totalPaymentᵢ = principalDueᵢ + interestDueᵢ
closingBalanceᵢ = openingBalanceᵢ − principalDueᵢ
```

**Use case:** "straight-line" repayment schedules common in commercial loans.

## 3. Bullet

`BulletScheduleGenerator`. Principal is paid in a single final row; interest is paid periodically (or capitalized).

Two modes:

- **Interest-only until bullet.** Each period: `interestDue = openingBalance × r`, `principalDue = 0`, `totalPayment = interestDue`. Final row: `principalDue = P`, `totalPayment = interestDue + P`.
- **Zero-coupon (capitalized interest).** All periods: `interestDue = 0`, `principalDue = 0`, `openingBalance` grows by compounded interest. Final row: `principalDue = openingBalance_final`, representing accumulated principal + interest.

**Use case:** bridge loans; some corporate structures.

## Rounding and residuals

- Every monetary field in `ScheduleRow` is a `Money` and is therefore stored at 2 decimal places with banker's rounding.
- The rounding **residual** (accumulated sub-cent drift) is assigned to the last row's `PrincipalDue`. This guarantees `Rows[^1].ClosingBalance == 0`.
- The residual's documented upper bound is `0.01 × n` cents in the worst case; in practice it is far smaller.
- An alternative — residual on the first row — is **not** used. Reason: borrowers expect the final payment to be the one that "closes out" the loan; adjusting the first payment confuses regulatory disclosures.

Reference: ECB Statistical Classification §6.4.2 ("rounding of scheduled payments").

## Verification

- **Reference tests.** Each generator has at least ten canonical cases where the full schedule is specified (every row's amounts) and tested row-by-row.
- **Property-based (FsCheck.Xunit).**
  - Sum of `PrincipalDue` across rows equals `Principal`.
  - Sum of `InterestDue` across rows equals total interest computed independently via `IInterestCalculator`.
  - `Rows[^1].ClosingBalance == Money.Zero(Principal.Currency)`.
  - Row count equals `Term.Months` (for monthly) or `Term.Months / 3` (for quarterly).
- **Verify.Xunit snapshots.** A fixed set of canonical loans (€10,000 × 24 months × 5% fixed × 30E/360; etc.) have full-schedule snapshots locked in. Any change to the generator that affects these snapshots requires an updated snapshot in the same commit, with the reason in the commit message.
