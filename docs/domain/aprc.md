# APRC — Annual Percentage Rate of Charge

Credere computes an APRC per the EU Consumer Credit Directive. The result is surfaced to the borrower on the loan agreement and stored on the Loan aggregate as part of the approved terms.

## Authoritative sources

- **Directive 2008/48/EC** (Consumer Credit Directive), Annex I.
- **Directive 2023/2225** (revised Consumer Credit Directive), Annex III — for products falling under the revised scope. Where M1 implements the 2008 formulation; 2023 cases are additive.
- **Commission Staff Working Document SWD(2012) 128 final**, "Guidelines on the application of Directive 2008/48/EC in relation to costs and the Annual Percentage Rate of Charge".

Citations appear in the XML comment on `AprcCalculator.cs` with Annex / section references.

## Definition

The APRC `X` satisfies:

```
Σₖ Cₖ / (1 + X)^tₖ = Σₗ Dₗ / (1 + X)^sₗ
```

where:

- `Cₖ` is a drawdown (loan amount paid to the borrower) occurring at time `tₖ` years from the reference date.
- `Dₗ` is a borrower payment (principal + interest + fees) occurring at time `sₗ` years from the reference date.
- Time is measured in years using **Actual/365 Fixed** convention (per Annex I §II of the 2008 Directive).
- `X` is the annualized rate at which the two sides equate.

The equation is solved by an iterative numerical method; Credere uses **bisection** with convergence criteria described below.

## Scope in M1

The M1 calculator handles:

- One drawdown at a single date (the disbursement date), with amount equal to the loan principal less any upfront fees financed by the lender.
- A fixed schedule of borrower payments (produced by the module's selected `IScheduleGenerator`).
- Optional per-period charges added to each borrower payment (treated as part of `Dₗ`).

Explicitly out of M1 scope:

- Multiple drawdowns.
- Variable / contingent charges.
- Early-repayment APRC.
- Credit lines / overdrafts (Directive 2023/2225 §4 scope).

## Implementation

```csharp
public interface IAprcCalculator
{
    AprcResult Calculate(AprcRequest request);
}

public sealed record AprcRequest(
    IReadOnlyList<CashFlow> Drawdowns,
    IReadOnlyList<CashFlow> BorrowerPayments,
    LocalDate ReferenceDate);

public sealed record CashFlow(Money Amount, LocalDate Date);

public sealed record AprcResult(
    Percentage Aprc,
    int Iterations,
    decimal FinalResidual);
```

### Algorithm

1. Compute `τₖ = Act365Fixed.Fraction(ReferenceDate, tₖ)` for every cash flow.
2. Define `f(X) = Σₖ Cₖ (1 + X)^(-τₖ) − Σₗ Dₗ (1 + X)^(-sₗ)`.
3. Bracket `X ∈ [0, 1]`. If `f(0) × f(1) > 0`, widen the upper bound (iterate doubling up to `X = 100`).
4. Bisect until:
   - `|f(X)| < 1e-12 × max(|Drawdowns|, |Borrower payments|)`, OR
   - the bracket width is `< 1e-10`, OR
   - 200 iterations.
5. Return `X` as a `Percentage` (converted from fraction, four-decimal precision guaranteed).

### Numerical notes

- Inside the bisection, powers `(1 + X)^(-τ)` are computed in `double`, not `decimal`, because `decimal` has no fractional exponentiation. The error introduced by `double` is bounded (τ ≤ 50 years, X ≤ 100%, double has ~15 decimal digits of precision). The final `Aprc` is converted back to `decimal` with four-decimal rounding.
- The reference APRC values in the EU Directive examples are given to four decimal places. Credere matches them exactly at that precision in all reference tests.

### Reference examples

Annex I of Directive 2008/48/EC contains six worked examples. All six are implemented as `AprcCalculatorTests`:

| Example | Scenario | Expected APRC |
|---|---|---|
| 1 | €6,000 one-off, 12 monthly payments of €528.65 | 9.6 % |
| 2 | €6,000 with €60 upfront charge, 12 monthly payments of €528.65 | 11.9 % |
| 3 | €6,000 one-off, 48 monthly payments | 9.6 % |
| 4 | Credit granted for 3 months (demo of the time scale) | 6.0 % |
| 5 | Bullet repayment, 12-month term | 6.0 % |
| 6 | Deferred repayment, 24-month term | 7.9 % |

For each, the test XML comment cites the Annex example number, the inputs in the directive's wording, and the expected value.

### Property-based invariants

- **Round-trip stability.** For an annuity schedule with rate `r` and fee = 0, the computed APRC equals `r` within four decimal places. (Reason: the annuity formula and the APRC equation agree when there are no charges.)
- **Monotonic in fees.** Adding an upfront charge strictly increases APRC.
- **Monotonic in rate.** Increasing the nominal rate strictly increases APRC.
- **Zero-sum degeneracy.** If total drawdowns equal total payments (zero interest, zero fees, trivial case), APRC is zero.

### Mutation testing

Stryker.NET is run on `AprcCalculator.cs` with a mutation score threshold of 85%. The bisection body, convergence checks, and bracket widening are the most mutation-prone regions.

## Future work (post-M1)

- Multiple drawdowns.
- Early-repayment APRC per Directive 2023/2225 Annex III.
- Credit-line / overdraft APRC.
- Disclosure extensions (e.g., representative APRC with assumed fees).

Each of these will arrive in its own PR with new reference cases cited from the appropriate Annex.
