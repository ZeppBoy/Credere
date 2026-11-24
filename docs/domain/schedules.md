# Amortization schedules

Credere generates three kinds of amortization schedule from the shared kernel. All live in `src/Credere.Core/Schedules/`. All produce a canonical `AmortizationSchedule` record validated by the aggregate before it can be stored on a Loan.

## Public contract

**Status:** Implemented (M1-13 and M1-23). Reference dates approved by the owner on 2026-11-20 (PR #43).

```csharp
public interface IScheduleGenerator
{
    Result<AmortizationSchedule, DomainError> Generate(ScheduleRequest request);
}

public enum PaymentFrequency
{
    Monthly = 12,      // Value = periods per year. The primary case in M1.
    Quarterly = 4,     // Allowed as a non-primary case; Term.Months must be a multiple of 3.
}

public sealed record ScheduleRequest
{
    public Money Principal { get; }
    public InterestRate Rate { get; }
    public LoanTerm Term { get; }
    public LocalDate DisbursementDate { get; }
    public LocalDate FirstPaymentDate { get; }
    public PaymentFrequency PaymentFrequency { get; }

    public static Result<ScheduleRequest, DomainError> Create(
        Money principal,
        InterestRate rate,
        LoanTerm term,
        LocalDate disbursementDate,
        LocalDate firstPaymentDate,
        PaymentFrequency paymentFrequency);
}

public sealed record AmortizationSchedule
{
    public IReadOnlyList<ScheduleRow> Rows { get; }
    public Money Principal { get; }          // Rows[0].OpeningBalance

    public static Result<AmortizationSchedule, DomainError> Create(
        Money principal, IReadOnlyList<ScheduleRow> rows);
}

public sealed record ScheduleRow(
    int Index,
    LocalDate PaymentDate,
    Money OpeningBalance,
    Money CapitalisedInterest,
    Money InterestDue,
    Money PrincipalDue,
    Money TotalPayment,
    Money ClosingBalance);
```

`ScheduleRequest` and `AmortizationSchedule` have invariants, so they use a get-only shape and `Create(...)` ([value-objects.md](value-objects.md)); a `with` expression can't bypass them. `Create` throws `ArgumentNullException` for a null argument and returns a `ValidationError` otherwise.

`ScheduleRequest.Create` rejects, in this order:

- `paymentFrequency` that is not `Monthly` or `Quarterly`;
- `firstPaymentDate` that is not strictly after `disbursementDate` (field `firstPaymentDate`). There is no maximum length of the first period in M1;
- a `term` that is not a whole number of payment periods, for example 10 months with quarterly payments (field `term`).
- a `term` whose last payment date falls outside the calendar's supported range (field `term`).

`Generate` returns a `Result` so a validated request that cannot produce a valid schedule can return a domain error. `AmortizationSchedule.Create` reports an early principal repayment on field `principal` and other invariant violations on field `rows`. It checks these invariants:

- `Rows` is non-empty, and every amount is in the principal's currency.
- `Rows[0].OpeningBalance == Principal`.
- `Rows[^1].ClosingBalance == Money.Zero(Principal.Currency)`, exactly, because the last row absorbs the residual (see [Rounding and residuals](#rounding-and-residuals)).
- On each row before maturity, `PrincipalDue < OpeningBalance`.
- For each row, `OpeningBalance + CapitalisedInterest - PrincipalDue == ClosingBalance` and `InterestDue + PrincipalDue == TotalPayment`.
- `CapitalisedInterest` records the period interest added to the balance. When no interest is capitalised, the field holds zero in the row's currency.
- For each adjacent pair: `Rows[i].ClosingBalance == Rows[i+1].OpeningBalance`.
- Dates strictly increase.

The row equations use exact integer cents. A row with a representable balance and payment can pass even if an intermediate sum exceeds `decimal.MaxValue`. A difference of one cent still gives a `ValidationError`. `Create` copies the rows, and `Rows` is read-only. The schedule holds a collection, so it implements equality itself: two schedules are equal when their rows are equal one by one. `ScheduleRow.Index` is not checked against the row's position, because this contract does not list it as an invariant.

## Periods and interest (all generators)

**Status:** Implemented (M1-13), as the internal `SchedulePeriods` (`PaymentDate`, `Periods`, `RowCount`). Reference dates: 41 payment dates and 3 interest-period lists in `SchedulePeriodsTests`, recomputed by Codex and approved by the owner (PR #43). The interest bullet below is implemented by the generators (M1-14 to M1-16).

- **Payment dates.** Row `i` (1-based) falls on `FirstPaymentDate.PlusMonths((i − 1) × 12 / periodsPerYear)`. Each date is computed from `FirstPaymentDate` rather than from the previous row, so a month-end start doesn't drift: 31 January gives 28 February, then 31 March, not 28 March.
- **Interest period.** Row `i` accrues from `periodStartᵢ` (inclusive) to `PaymentDateᵢ` (exclusive). `periodStart₁ = DisbursementDate` and `periodStartᵢ = PaymentDateᵢ₋₁`.
- **Interest.** Every generator computes the period accrual with `IInterestCalculator.Calculate(Rate, openingBalanceᵢ, periodStartᵢ, PaymentDateᵢ)`. `InterestDue` records the cash interest due, and `CapitalisedInterest` records the interest added to the balance. Their sum is the same amount Servicing accrues under the rate's day-count convention. [Interest calculator](rates.md#iinterestcalculator--the-single-entry-point)

## 1. Annuity

**Status:** Implemented (M1-14): `AnnuityScheduleGenerator`, constructed with an `IInterestCalculator`, whose `Generate` returns `Result<AmortizationSchedule, DomainError>`. Reference schedules: 16 complete schedules in `AnnuityScheduleGeneratorVectors`, recomputed independently by Codex and approved by the owner (PR #51). Planned (M1-14, after owner approval of the failure vectors on PR #51): rejecting a request whose rounded instalment would repay the balance before the last row. It does not implement `IScheduleGenerator` until that interface returns a `Result` too (owner decision on PR #51).

`AnnuityScheduleGenerator`. Equal `TotalPayment` on every row except the last, which absorbs the residual.

**Instalment.** For principal `P`, nominal periodic rate `r = NominalRate.Fraction / periodsPerYear`, and `n` payments:

```
payment = P × r × (1 + r)^n / ((1 + r)^n − 1)
```

`n` is an integer, so the power stays in `decimal`. The payment becomes a `Money` once, so it is rounded once. The code evaluates the equivalent `P × r / (1 − (1+r)^−n)`, because `(1+r)^n` overflows a `decimal` for a long term at a high rate (600 months at 100%), while `(1+r)^−n` only shrinks.

For each row `i = 1..n−1`:

```
interestDueᵢ   = IInterestCalculator.Calculate(Rate, openingBalanceᵢ, periodStartᵢ, PaymentDateᵢ)
principalDueᵢ  = payment − interestDueᵢ
closingBalanceᵢ = openingBalanceᵢ − principalDueᵢ
```

The last row pays the remaining balance: `principalDueₙ = openingBalanceₙ` and `totalPaymentₙ = interestDueₙ + openingBalanceₙ`.

**Why the instalment and the interest use different rates.** The instalment uses the nominal periodic rate, which is the usual way to quote an annuity instalment. The interest uses the day-count convention, because AGENTS.md §7 sends all interest through `IInterestCalculator`. The two agree when every period's day-count fraction is exactly `1 / periodsPerYear`. That happens under 30/360 ISDA or 30E/360 when:

- the first period is one full period; and
- the payment day of the month is ≤ 28.

In every other case the difference builds up in the last row. That includes the Actual conventions, a broken first period, and payment days 29–31.

**Edge cases.**
- Zero rate: `payment = P / n`, interest is zero, and the last row absorbs the rounding.
- Long first period: if the first period's interest exceeds the instalment, that row's `PrincipalDue` is negative and the balance grows. The schedule contract allows it, and the generator does not reject it.
- Tiny principal: the rounded instalment can repay the balance before the last row (0.03 over 5 rows at 0%: instalment 0.006 becomes 0.01, so the balance would be −0.01 after row 4). Under 1.00 at 6% it happens too. Planned (M1-14): a `ValidationError` on `principal`.
- An amount so large that the instalment, a balance or an interest overflows a `decimal` returns a `ValidationError` on `principal`; one too large to keep cents is rejected by the schedule's exact row check.
- Floating rate: the schedule is generated at origination against the rate active then, and re-generated on reset dates (M2+, once reset tracking lands).
- `EffectiveRate`: blocked on [open question #10](../open-questions.md). Its periodic rate `(1 + R)^(1/periodsPerYear) − 1` needs a fractional power.

## 2. Differentiated (Decreasing)

`DifferentiatedScheduleGenerator`. Equal `PrincipalDue` across all rows; `InterestDue` decreases as the balance drops.

**Formula.** For each row `i = 1..n`:

```
principalDueᵢ  = Money(P / n)                (rounded once; the last row takes openingBalanceₙ instead)
interestDueᵢ   = IInterestCalculator.Calculate(Rate, openingBalanceᵢ, periodStartᵢ, PaymentDateᵢ)
totalPaymentᵢ  = principalDueᵢ + interestDueᵢ
closingBalanceᵢ = openingBalanceᵢ − principalDueᵢ
```

**Use case:** "straight-line" repayment schedules common in commercial loans.

## 3. Bullet

`BulletScheduleGenerator`. Principal is paid in a single final row; interest is paid periodically or capitalised.

Two modes:

- **Interest-only until bullet.** Each row: `interestDue = IInterestCalculator.Calculate(...)` on the opening balance, `principalDue = 0`, `totalPayment = interestDue`. Final row: `principalDue = P` and `totalPayment = interestDue + P`.
- **Zero-coupon (capitalised interest).** In every row except the last, `IInterestCalculator.Calculate(...)` is computed on the opening balance and added to it, so the balance compounds. Those rows show `InterestDue = 0`, `PrincipalDue = 0` and `TotalPayment = 0`. The final row pays the whole accumulated balance.

**Use case:** bridge loans; some corporate structures.

## Rounding and residuals

- Every monetary field in `ScheduleRow` is a `Money` and is therefore stored at 2 decimal places with banker's rounding.
- The **residual** goes into the last row's `PrincipalDue`, so `Rows[^1].ClosingBalance` is exactly zero.
- If rounded principal due reaches or exceeds the opening balance before maturity, the request returns a `ValidationError` on `principal`. A negative principal due after a long first period remains valid. Bullet rows with a positive opening balance can have zero cash dues before maturity.
- The residual has no small fixed bound. The annuity instalment is rounded to the cent once, and that rounding error compounds over the term at the loan's rate. Example: 250,000 at 12% nominal, 360 monthly rows, 30E/360, payment day ≤ 28. The regular payment is 2,571.53 and the last is 2,576.59, a 5.06 difference against 360 rows. Mismatches between day count and nominal rate (see [Annuity](#1-annuity)) add to it. Reference cases assert the exact last row.
- Putting the residual on the first row instead is **not** used, because the final payment is the one that closes out the loan.

This placement is a Credere design decision. No regulatory source prescribes it.

## Verification

- **Reference tests.** Each generator has at least ten canonical cases where the full schedule is specified (every row's amounts) and tested row-by-row. The cases are formula-derived, with the arithmetic for at least the first two rows and the last row shown in the test comment.
- **Property-based (FsCheck.Xunit).**
  - Sum of `PrincipalDue` across rows equals `Principal`.
  - Each row's `InterestDue` equals `IInterestCalculator.Calculate(Rate, OpeningBalance, periodStart, PaymentDate)` for that row.
  - `Rows[^1].ClosingBalance == Money.Zero(Principal.Currency)`.
  - Row count equals `Term.Months` (for monthly) or `Term.Months / 3` (for quarterly).
  - Annuity under 30E/360 with the payment day ≤ 28 and a full first period: every row except the last has the same `TotalPayment`.
- **Verify.Xunit snapshots.** A fixed set of canonical loans (€10,000 × 24 months × 5% fixed × 30E/360; etc.) have full-schedule snapshots locked in. Any change to the generator that affects these snapshots requires an updated snapshot in the same commit, with the reason in the commit message.
