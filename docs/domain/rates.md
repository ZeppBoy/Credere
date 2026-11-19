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
- **Rounding point:** each call computes in `decimal` and creates exactly one `Money` at the end, which rounds to two places with `MidpointRounding.ToEven` ([value-objects.md](value-objects.md#money)). Per-period interest in a schedule row is that `Money` ([schedules.md](schedules.md)).
- **Ties.** A repeating day-count fraction such as `20/360` is stored in 28 digits, rounded, so an exact half-cent product can land a hair above or below the tie (1,950.00 × 0.06% × 20/360 is exactly 0.065, but `decimal` gives `0.06500000000000000000000000052`). A scan of 13.5 million exact ties under Actual/360 and Actual/365 Fixed rounded about 30% of them the wrong way. Every subtype therefore creates its `Money` through `InterestRate.ToMoney`, an `internal static` helper that treats a value within 1e-22 of the interest's magnitude of a half-cent as that half-cent, then rounds half-even once. A value that differs from a tie by more than that, for example by 2.5e-14, is rounded as it is. The same scan, repeated with the helper, found no wrong tie in 6.7 million.

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

The root also has `internal static Money ToMoney(decimal interest, string currency)`, the one place a subtype turns its unrounded `decimal` into the returned `Money` (see Ties above). `FloatingRate` and `TieredRate` use it too.

## `FixedRate`

**Status:** Implemented (M1-08). Reference vectors approved by the owner on 2026-11-19 (PR #40).

```csharp
public sealed record FixedRate : InterestRate
{
    public static Result<FixedRate, DomainError> Create(
        Percentage nominalRate, DayCountConvention dayCountConvention);

    internal override Money CalculateInterest(Money balance, LocalDate s, LocalDate e)
    {
        // Left to right: principal times rate first, then the day-count fraction.
        var interest = balance.Amount * NominalRate.Fraction * DayCountConvention.Fraction(s, e);
        return ToMoney(interest, balance.Currency);
    }
}
```

- **Formula:** `interest = balance × rate × dayCountFraction(s, e)`.
- **Order of evaluation.** The product is evaluated left to right in `decimal`, principal times rate first, and rounded once through `ToMoney`. `balance * (rate * fraction)` is not equivalent: for 73.00 at 1.5% over 5/365 it gives `0.01499…96` and rounds to 0.01, where the exact tie 0.015 goes to 0.02 (found in the vector review of PR #40).
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
