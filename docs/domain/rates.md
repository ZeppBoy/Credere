# Interest rates

Interest rates are modelled as a `sealed record` hierarchy rooted at `InterestRate`. Each subtype is a distinct banking concept; **every subtype's interest formula is unique to that type** — the Promissio pattern of "every rate is simple interest in disguise" is not repeated here.

All live in `src/Credere.Core/Rates/`.

> The sketches below use positional-record syntax for brevity. Implementations follow the construction rules in [`value-objects.md`](value-objects.md): get-only properties plus a static `Create(...)` returning `Result<T, DomainError>`. That way a `with` expression can't bypass an invariant such as `NominalRate ≤ 100%`.

## `IInterestCalculator` — the single entry point

**Status:** Implemented (M1-08).

AGENTS.md §7 says that interest goes through `IInterestCalculator`, so every calculation is in one auditable place. Schedules, accrual and payoff all call it, and nothing else computes interest.

```csharp
public interface IInterestCalculator
{
    /// <summary>
    /// Interest on <paramref name="balance"/> for the period from <paramref name="periodStart"/>
    /// (inclusive) to <paramref name="periodEnd"/> (exclusive) under <paramref name="rate"/>.
    /// </summary>
    Money Calculate(InterestRate rate, Money balance, LocalDate periodStart, LocalDate periodEnd);
}
```

- `InterestCalculator` is the only implementation: a stateless `sealed` class that delegates the formula to the rate subtype.
- `periodEnd < periodStart` is a programmer error and throws.
- The rate's formula method is `internal`, so code outside `Credere.Core` can't bypass the calculator. Because the member is `internal abstract`, the hierarchy also can't be extended outside the assembly.
- **Rounding point:** each call evaluates the interest exactly and creates exactly one `Money` at the end, which rounds to two places with `MidpointRounding.ToEven` ([value-objects.md](value-objects.md#money)). Per-period interest in a schedule row is that `Money` ([schedules.md](schedules.md)).
- **Exact evaluation (M1-22).** A `decimal` holds 28 digits, so a repeating day-count fraction such as `20/360` is rounded, and an exact half-cent product can land a hair above or below the tie (1,950.00 × 0.06% × 20/360 is exactly 0.065, but `decimal` gives `0.0650000000000000000000000001`). A scan of 13.5 million exact ties under Actual/360 and Actual/365 Fixed rounded about 30% of them the wrong way. The M1-08 fix, a relative 1e-22 tolerance, was withdrawn after the cross-vendor review: it rounded a genuine non-tie as a tie (€100 at 5.00000000000000000000001% for 9 days Actual/360 gave €0.12 instead of €0.13), and its intermediate product overflowed for `decimal.MaxValue`.
  - `Money.Amount` and `Percentage.Value` are finite decimals, so each is a coefficient over a power of ten. The five built-in conventions expose their year fraction as an exact ratio of integers (`internal virtual DayCountConvention.ExactFraction`), sharing the day count with the public `Fraction`. A custom convention that implements only `Fraction` contributes the exact value of the decimal it returns.
  - `internal readonly struct ExactInterest` multiplies the three ratios as integers, with no rounding. `ToMoney` compares the exact cent remainder with half the denominator and uses the parity of the cent only on equality, then splits the rounded cents into units and cents, so a result of `decimal.MaxValue` is never multiplied by 100. The rounded cents are converted to a `decimal` with the fewest decimal places (0, 1 or 2) that hold them, and the conversion throws `OverflowException` when they do not fit the 96-bit coefficient: a rounded amount above `decimal.MaxValue`, or one within the bounds whose cents need more digits than a decimal holds (79,228,162,514,264,337,593,543,950,334.15, for example). `Money` cannot hold such an amount, so it is never silently reduced (owner decision of 2026-11-20, from the Codex review of PR #47). `decimal.MaxValue` itself succeeds.
  - Sums of `ExactInterest` stay exact, so the segments of a floating or tiered rate are rounded once, at the end.
  - The public `DayCountConvention.Fraction` signature and its decimal results are unchanged.
  - Reference vectors: the six rows of `ExactInterestTests` (issue #46, approved on PR #47), plus every earlier fixed-rate and day-count vector.

## `InterestRate` (abstract root)

**Status:** Implemented (M1-08).

```csharp
public abstract record InterestRate
{
    public abstract Percentage NominalRate { get; }
    public abstract DayCountConvention DayCountConvention { get; }

    // Called only by InterestCalculator.
    internal abstract Money CalculateInterest(
        Money balance,
        LocalDate periodStart,
        LocalDate periodEnd);
}
```

`NominalRate` is the "quoted" rate (the headline percent a borrower sees for the product). The specific semantics of each subtype determine how `CalculateInterest` uses it.

A subtype creates its returned `Money` through `ExactInterest.ToMoney` (see Exact evaluation above), never from a `decimal` product. `FloatingRate` and `TieredRate` do the same.

## `FixedRate`

**Status:** Implemented (M1-08). Reference vectors approved by the owner on 2026-11-19 (PR #40).

```csharp
public sealed record FixedRate : InterestRate
{
    public static Result<FixedRate, DomainError> Create(
        Percentage nominalRate, DayCountConvention dayCountConvention);

    internal override Money CalculateInterest(Money balance, LocalDate s, LocalDate e)
    {
        // Exact: the three factors are integer ratios, rounded once at the end.
        var fraction = DayCountConvention.ExactFraction(s, e);
        return ExactInterest.Of(balance, NominalRate, fraction).ToMoney(balance.Currency);
    }
}
```

- **Formula:** `interest = balance × rate × dayCountFraction(s, e)`.
- **Evaluation.** The product is exact, so the order of the factors no longer matters. In `decimal`, `balance * (rate * fraction)` gave `0.01499…96` for 73.00 at 1.5% over 5/365 and rounded to 0.01, where the exact tie 0.015 goes to 0.02 (found in the vector review of PR #40). That case stays in `FixedRateTests`.
- **Use case:** standard installment loans.
- **Invariants:** `NominalRate` ≥ 0, ≤ 100%. `Create` returns a `ValidationError` on field `nominalRate` above 100%, and throws `ArgumentNullException` for a null argument.

## `FloatingRate`

```csharp
public sealed record FloatingRate(
    Percentage BaseRate,
    Percentage Margin,
    DayCountConvention DayCountConvention,
    ResetSchedule ResetSchedule) : InterestRate
{
    public override Percentage NominalRate => BaseRate + Margin;

    internal override Money CalculateInterest(Money balance, LocalDate s, LocalDate e);
}
```

- **Formula:** per sub-period defined by the `ResetSchedule`, use the BaseRate effective on the reset date + Margin. `interest(s, e) = Σᵢ balance × rateᵢ × dayCountFraction(sᵢ, eᵢ)`. The sum stays in `decimal` and becomes `Money` once.
- **Use case:** EURIBOR + margin products.
- **Reset schedule:** out of scope for M1; a `ResetSchedule.Fixed(baseRate)` placeholder is used until M2 introduces reset tracking.
- **Invariants:** `BaseRate`, `Margin` ≥ 0.

## `TieredRate`

```csharp
public sealed record TieredRate(
    IReadOnlyList<Tier> Tiers,
    DayCountConvention DayCountConvention,
    TieredApplicationMode Mode) : InterestRate
{
    public override Percentage NominalRate => Tiers[^1].Rate;

    internal override Money CalculateInterest(Money balance, LocalDate s, LocalDate e);

    // UpperLimit is null only on the last tier, meaning "no upper limit".
    public sealed record Tier(Percentage Rate, Money? UpperLimit);
}

public enum TieredApplicationMode
{
    OnCurrentBalance,   // Look up rate by current balance; one rate for the full period.
    Marginal            // Apply tier rates like income-tax brackets, each tier only to the slice of balance in that bracket.
}
```

- **Formula (OnCurrentBalance):** find the first tier where `UpperLimit` is null or `balance <= UpperLimit`; apply that rate across the period.
- **Formula (Marginal):** for each tier, compute `min(balance, upper) − previousUpper`, treating a null `upper` as `balance`, and apply that tier's rate. The sum stays in `decimal` and becomes `Money` once.
- **Use case:** balance-banded pricing on revolving-flavoured term products.
- **Invariants:**
  - At least one tier.
  - Every tier except the last has a non-null `UpperLimit`, with limits strictly ascending.
  - The last tier's `UpperLimit` is null.
  - All limits are in the currency of the balance.

**The Promissio bug to avoid:** `TieredRate` must be called with the **current balance**, not the original principal, for the `OnCurrentBalance` lookup. That is why the parameter is named `balance` on every rate: the caller passes the outstanding balance for the period.

## `EffectiveRate` (effective annual rate)

```csharp
public sealed record EffectiveRate(
    Percentage EffectiveAnnualRate,
    DayCountConvention DayCountConvention) : InterestRate
{
    public override Percentage NominalRate => EffectiveAnnualRate;

    internal override Money CalculateInterest(Money balance, LocalDate s, LocalDate e);
}
```

- **Formula:** `interest = balance × ((1 + R)^τ − 1)`, where `R` is the effective annual rate and `τ = dayCountFraction(s, e)`. This is **compound** interest derived from an effective annual rate, **not** simple interest.
- **Use case:** products whose rate is quoted as an effective annual rate rather than a nominal one; the schedule is derived from it.
- **Not the APRC.** The APRC is defined in Directive (EU) 2023/2225, Article 3(7), as the total cost of the credit to the consumer, including charges, expressed as an annual percentage of the total amount of credit. `AprcCalculator` computes it from cash flows ([`aprc.md`](aprc.md)). An APRC is an output, never an input rate. The two coincide only when there are no charges and the intervals are whole periods.
- **Invariants:** `EffectiveAnnualRate` ≥ 0, ≤ 100%.
- **Open:** `(1 + R)^τ` with fractional `τ` needs a fractional power, which `decimal` doesn't provide, and AGENTS.md §7 allows `double` only in the APRC solver. `EffectiveRate` is not implemented until [open question #10](../open-questions.md) is decided.

**The Promissio bug to avoid:** applying an effective annual rate as if it were simple interest (`balance × rate × fraction`) is wrong. An effective rate is defined via compounding; using the compound form is the whole point of having a separate subtype.

## Common rules

- All rates are immutable after construction.
- Rates are never "changed" on an existing loan — they are replaced, and the replacement is a modelled business event (not in scope until a restructuring feature lands).
- Day-count conventions belong to the rate (not the loan) because different sub-periods of a floating rate can use different conventions in principle. We don't currently exploit this, but the shape leaves room for it.
- `CalculateInterest` works in absolute, compounding-free terms for Fixed / Floating / Tiered. Compounding happens at the schedule-generator level or, for `EffectiveRate`, inside the rate itself.
- Each call rounds once, when it creates the `Money` it returns. Intermediate products and sums stay in `decimal`.

## Testing

Each rate type has:

1. Unit tests on `IInterestCalculator.Calculate` with explicit input/output pairs.
2. At least five reference cases from a cited source (AGENTS.md §7), named in the test class's XML comment. Use published worked examples where they exist: for example, the four Actual/Actual (ISDA) examples in ISDA's 1998 memo are `FixedRate` cases on £10,000 at 10% ([day-counts.md](day-counts.md#reference-vectors)). Otherwise use formula-derived cases with the arithmetic shown.
3. Property-based invariants (FsCheck.Xunit):
   - Zero balance → zero interest.
   - Zero period (`s == e`) → zero interest.
   - Linearity in balance for Fixed / Floating: `interest(2b) == 2 × interest(b)`, within one cent, because each side rounds once.
   - Rate × fraction symmetry for Fixed: `interest(b, 0, 1yr) == b × rate × 1` within rounding tolerance for 30/360.
4. For `EffectiveRate` (once open question #10 is decided): an annuity generated from `EffectiveRate` `R`, with no charges and whole-month periods, has an APRC equal to `R` within the round-trip tolerance in [`aprc.md`](aprc.md#property-based-invariants).
