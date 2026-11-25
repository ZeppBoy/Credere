# APRC — Annual Percentage Rate of Charge

**Status:** Planned (M1).

Credere computes the APRC (the "APR" in the directive's wording) for new consumer credit agreements under Directive (EU) 2023/2225. The result is shown to the borrower on the loan agreement and stored on the Loan aggregate as part of the approved terms.

## Authoritative sources

| Source | Used for |
|---|---|
| **Directive (EU) 2023/2225** (revised Consumer Credit Directive), OJ L, 30.10.2023: Article 3(5) and 3(7) (definitions), Article 30 (calculation), **Annex III** Parts I and II | The equation, time intervals, rounding, and additional assumptions. This is the regime Credere implements. |
| **Directive 98/7/EC**, OJ L 101, 1.4.1998, pp. 20–23: Annex II, which replaced Annex III ("Examples of calculation") of Directive 87/102/EEC | Reference vectors for the solver. These are the only worked APRC examples published in EU legislation. |

Why this pairing (owner decision, 2026-11-18):

- 2023/2225 repeals Directive 2008/48/EC with effect from 20 November 2026 (Article 47), and Member States apply their transposing measures from that date (Article 48(1)). 2008/48/EC keeps applying to agreements that existed on 20 November 2026 until they end. Credere does not model those legacy agreements.
- Neither 2008/48/EC (Annex I) nor 2023/2225 (Annex III) contains worked examples. The equation in 98/7/EC is the same one, so its examples remain a valid test of the solver. Its 365.25-day variants were dropped from the later directives and are not used.

The XML comment on `AprcCalculator.cs` and each test cite the article, Annex part and remark, or example number.

## Definition

Annex III, Part I, of 2023/2225 defines the APR `X` by:

```
Σₖ Cₖ (1 + X)^(−tₖ) = Σₗ Dₗ (1 + X)^(−sₗ)
```

- `Cₖ` is drawdown `k`. `tₖ` is the interval in years from the first drawdown to drawdown `k`, so `t₁ = 0`.
- `Dₗ` is a repayment or a payment of charges. `sₗ` is the interval in years from the first drawdown to that payment.
- The starting date is the date of the first drawdown (remark (b)).

The APR expresses the total cost of the credit to the consumer (Article 3(7)). Which charges belong in that cost is set by Article 3(5) and Article 30(2). The caller decides which charges to include when it builds the cash flows; the calculator does not.

## Time intervals

**Status:** Implemented (M1-17).

Annex III, Part I, remark (c):

- A year is presumed to have 365 days (366 in a leap year), 52 weeks, or 12 equal months. A month is presumed to have 30.41666 days (365/12).
- An interval that is not a whole number of weeks, months or years is expressed as a whole number of one of those periods plus a number of days:
  - (i) every day is counted, including weekends and holidays;
  - (ii) the equal periods are counted first, then the days, both backwards to the date of the initial drawdown;
  - (iii) the days exclude the first day and include the last one. They are converted to years by dividing by 365 or 366: the number of days in the complete year counted backwards from the last day to the same day of the previous year.

`EuCcdAnnexIII` implements the APRC time basis as an internal `DayCountConvention`. It measures Gregorian calendar months and counts any remaining days using the length of the year ending at the anchor date. Its `Fraction(firstDrawdown, date)` works as follows.

1. If both dates are the last day of their month, return the difference in calendar months divided by 12. [Credere’s D4 month-end convention](https://github.com/ZeppBoy/Credere/issues/23#issuecomment-7020226307)
2. For other pairs, find `m`, the largest number of months such that `date.PlusMonths(−m) ≥ firstDrawdown`. When the target month lacks the original day, use its last day. Months are the whole-period unit for M1’s monthly and quarterly schedules.
3. Let `anchor = date.PlusMonths(−m)` and `n` be the number of days from `firstDrawdown` excluded to `anchor` included. If `n = 0`, return `m / 12`.
4. Otherwise return `m / 12 + n / Y`. `Y` is the number of days from `anchor.PlusYears(−1)` to `anchor` (365 or 366), using the same last-day clamp if the prior year lacks 29 February.

Whole months contribute one twelfth of a year each. The Commission’s earlier guidance illustrates backward clamping and the look-back year through worked examples. [SWD(2012) 128 final, §4.1.1, pp. 26–27](https://mf.gov.cz/assets/attachments/EU-MFCR_Metodika_2012_128-Guidelines-consumer-credit-directive-swd-en.pdf#page=26)

The table applies remark (c) and the D4 month-end rule.

| First drawdown | Cash flow | `m` | `anchor` | `n` | `Y` | Fraction |
|---|---|---|---|---|---|---|
| 2026-01-15 | 2027-07-15 | 18 | 2026-01-15 | 0 | — | 18/12 = 1.5 |
| 2026-01-15 | 2026-03-01 | 1 | 2026-02-01 | 17 | 365 | 1/12 + 17/365 = 0.1299086758 |
| 2027-12-20 | 2028-03-10 | 2 | 2028-01-10 | 21 | 365 | 2/12 + 21/365 = 0.2242009132 |
| 2028-02-20 | 2028-04-10 | 1 | 2028-03-10 | 19 | 366 | 1/12 + 19/366 = 0.1352459016 |
| 2026-01-31 | 2026-03-31 | 2 | 2026-01-31 | 0 | — | 2/12 = 0.1666666667 |
| 2026-01-31 | 2026-02-28 | 1 | 2026-01-31 | 0 | — | 1/12 = 0.0833333333 |

`EuCcdAnnexIIITests` checks 27 reference vectors at 12 decimal places with half-even rounding.

## Rounding

Annex III, Part I, remark (d) requires the result to an accuracy of at least one decimal place, rounded half up: "if the figure at the following decimal place is greater than or equal to 5, the figure at that particular decimal place shall be increased by one".

- `AprcResult.Rate` is the solver’s best estimate as a `decimal` fraction, for example `0.1296203771`, before disclosure rounding. Tests and the audit trail use it.
- `AprcResult.Disclosed` is a `Percentage` rounded to **two** decimal places with `MidpointRounding.AwayFromZero`. For the non-negative rates Credere computes, that equals the directive's half-up rule. Two places exceed the one-place minimum and match the two-place figures in the 98/7/EC examples. The loan agreement shows `Disclosed`.
- When the final bisection bracket spans a disclosure cutoff, the solver compares present values at that cutoff to select the disclosed percentage. An exact tie rounds up.

## Scope in M1

The M1 calculator handles:

- One drawdown at the disbursement date. Charges the creditor keeps at drawdown reduce the drawdown amount, as in 98/7/EC example A2 (1,000 lent, 50 retained, 950 drawn).
- A fixed schedule of borrower payments, produced by the selected `IScheduleGenerator`.
- Optional per-period charges, added to each borrower payment as part of `Dₗ`.

Out of M1 scope:

- Multiple drawdowns.
- Variable or contingent charges, and the assumptions for them in Annex III Part II (f) and (g).
- Overdraft facilities and open-end credit (Annex III Part II (d) and (e)).
- APRC after early repayment.

## Implementation

**Status:** Implemented (M1-19).

```csharp
public interface IAprcCalculator
{
    Result<AprcResult, DomainError> Calculate(AprcRequest request);
}

public sealed record AprcRequest(
    IReadOnlyList<CashFlow> Drawdowns,
    IReadOnlyList<CashFlow> BorrowerPayments);   // Start date = earliest drawdown (remark (b)).

public sealed record CashFlow(Money Amount, LocalDate Date);

public sealed record AprcResult(
    decimal Rate,             // Unrounded fraction.
    Percentage Disclosed,     // Two decimal places, half up.
    int Iterations,
    decimal FinalResidual);
```

`Calculate` returns a `ValidationError` ([ADR-0006](../adr/0006-error-semantics.md)) when:

- there is anything other than one drawdown or there are no payments;
- the drawdown amount is zero or negative, or a payment amount is negative;
- the currencies are mixed;
- a payment is dated before the first drawdown;
- the payments total less than the drawdowns (a negative APRC is out of scope);
- no root is found with `X ≤ 100` (10,000 %).

### Algorithm

1. Let `d₀` be the date of the drawdown. For each payment compute its year fraction from `d₀` using `EuCcdAnnexIII`.
2. Define `f(X) = Σₖ Cₖ (1 + X)^(−tₖ) − Σₗ Dₗ (1 + X)^(−sₗ)`.
3. Start with the bracket `X ∈ [0, 1]`. If `f(0) × f(1) > 0`, double the upper bound up to `X = 100`.
4. Bisect until any of these holds:
   - `|f(X)| < 1e-12 × max(Σ|Cₖ|, Σ|Dₗ|)`;
   - the bracket width is `< 1e-10`;
   - 200 iterations have run.
5. Return the midpoint as `Rate` and its residual as `FinalResidual`. If both bracket endpoints produce the same two-place percentage, use it as `Disclosed`. Otherwise, test the half-up cutoffs in the bracket. At a cutoff, a negative or zero residual places the rate at or above that cutoff.

### Numerical notes

- Bisection, rates, cash flows and residuals use `decimal`. The discount power `(1 + X)^(−t)` converts its arguments to `double` for `Math.Pow` and converts the discount factor back to `decimal` before applying it to a cash flow.
- At a disclosure cutoff, cash flows at whole-year offsets permit an exact present-value comparison. The solver clears denominators and compares integers, preserving exact ties and distinguishing nearby amounts. Fractional-year cutoffs use the solver’s discount-factor precision.

### Reference examples

`AprcSolverTests` checks all eight worked examples in 98/7/EC Annex II (amending 87/102/EEC Annex III). `AprcCalculatorTests` also checks Part B through the public calculator. The fixtures cite the part and example number. The "Exact" column was computed by bisection to 10 decimal places (2026-11-18).

| Ex. | Cash flows (amount @ years from drawdown) | Printed `i` | Printed % | Exact `i` |
|---|---|---|---|---|
| A1 | +1,000 @ 0; −1,200 @ 546/365 | 0.1296204 | 13 (12.96) | 0.1296203771 |
| A2 | +950 @ 0; −1,200 @ 546/365 | 0.169026 | 16.9 | 0.1690262065 |
| A3 | +1,000 @ 0; −600 @ 1; −600 @ 730/365 | 0.1306623 | 13.1 (13.07) | 0.1306623863 |
| A4 | +1,000 @ 0; −272 @ 90/365; −272 @ 181/365; −544 @ 1 | 0.13226 | 13.2 (13.23) | 0.1322624554 |
| B1 | +1,000 @ 0; −1,200 @ 1.5 | 0.129243 | 12.9 (12.92) | 0.1292432347 |
| B2 | +950 @ 0; −1,200 @ 1.5 | 0.168526 | 16.9 (16.85) | 0.1685261269 |
| B3 | +1,000 @ 0; −600 @ 1; −600 @ 2 | 0.13066 | 13.1 (13.07) | 0.1306623863 |
| B4 | +1,000 @ 0; −272 @ 0.25; −272 @ 0.5; −544 @ 1 | 0.13185 | 13.2 (13.19) | 0.1318549545 |

How the tests compare:

- **Solver (all eight).** The tests feed the year fractions from the table straight to the internal solver, which `Credere.Core.Tests` reaches through `InternalsVisibleTo`. The A examples use calendar days over 365. Annex III would measure their dates as whole months, so the A examples can only test the solver.
- **Precision.** `Rate` must lie within one unit of the last printed digit of `i`. The OJ rounds some values (A1) and truncates others (A3), so "round to N places" would be the wrong check.
- **Disclosure.** `Disclosed` must equal the printed two-place percentage, where one is printed, and its one-place rounding must equal the printed one-place figure.
- **End to end (B1–B4).** The B examples are also run through `Calculate` with dates a whole number of months apart, for example drawdown 2026-01-15 and payments on 2026-04-15, 2026-07-15 and 2027-01-15 for B4. This checks that `EuCcdAnnexIII` produces the B examples' fractions.

### Property-based invariants

- **Round-trip stability.** Take a `FixedRate` annuity under 30E/360 with:
  - nominal rate `r`;
  - no charges;
  - payments on the disbursement's day of the month, with that day ≤ 28;
  - principal ≥ 1,000.

  With unrounded cash flows, the annual rate is `(1 + r/12)^12 − 1`. Cent rounding changes the result. For example, a EUR 1,000.00 loan at 99.99% for one month pays EUR 1,083.32 after rounding ties to even. Its cash-flow APRC differs from the unrounded rate by about 1.447 basis points.

  For each successful annuity schedule, the property uses the rounded payments and independently solves the Annex III equation in monthly units, using decimal division to discount one month at a time. It raises the resulting monthly factor to the twelfth power and checks that `Rate` matches within `1e-9` in fractional-rate units. A failing FsCheck seed becomes a pinned regression case (AGENTS.md §8).
- **Monotonic in fees.** Adding an upfront charge strictly increases the APRC.
- **Monotonic in rate.** Increasing the nominal rate strictly increases the APRC.
- **Zero-cost degeneracy.** With one drawdown and payments that total exactly the drawdown, the APRC is zero.

### Mutation testing

Stryker.NET runs on `AprcCalculator.cs` and `EuCcdAnnexIII.cs` with an 85% mutation-score threshold. The bisection body, the convergence checks, bracket widening, and the month-counting loop are the regions most likely to let mutants survive.

## Future work (post-M1)

- Multiple drawdowns.
- Overdraft and open-end credit assumptions (Annex III Part II (d) and (e)).
- APRC after early repayment.
- Representative examples with assumed fees for advertising.

Each arrives in its own PR, with reference cases cited from Annex III or another primary source.
