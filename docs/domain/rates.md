# Interest rates

Interest rates are modelled as a `sealed record` hierarchy rooted at `InterestRate`. Each subtype is a distinct banking concept; **every subtype's `CalculateInterest` method is unique to that type** — the Promissio pattern of "every rate is simple interest in disguise" is not repeated here.

All live in `src/Credere.Core/Rates/`.

## `InterestRate` (abstract root)

```csharp
public abstract record InterestRate
{
    public abstract Percentage NominalRate { get; }
    public abstract DayCountConvention DayCountConvention { get; }

    public abstract Money CalculateInterest(
        Money principal,
        LocalDate periodStart,
        LocalDate periodEnd);
}
```

`NominalRate` is the "quoted" rate (the headline percent a borrower sees for the product). The specific semantics of each subtype determine how `CalculateInterest` uses it.

## `FixedRate`

```csharp
public sealed record FixedRate(
    Percentage NominalRate,
    DayCountConvention DayCountConvention) : InterestRate
{
    public override Money CalculateInterest(Money p, LocalDate s, LocalDate e)
        => p * (NominalRate.Fraction * DayCountConvention.Fraction(s, e));
}
```

- **Formula:** `interest = principal × rate × dayCountFraction(s, e)`.
- **Use case:** standard installment loans.
- **Invariants:** `NominalRate` ≥ 0, ≤ 100%.

## `FloatingRate`

```csharp
public sealed record FloatingRate(
    Percentage BaseRate,
    Percentage Margin,
    DayCountConvention DayCountConvention,
    ResetSchedule ResetSchedule) : InterestRate
{
    public override Percentage NominalRate => BaseRate + Margin;

    public override Money CalculateInterest(Money p, LocalDate s, LocalDate e);
}
```

- **Formula:** per sub-period defined by the `ResetSchedule`, use the BaseRate effective on the reset date + Margin. `interest(s, e) = Σᵢ principal × rateᵢ × dayCountFraction(sᵢ, eᵢ)`.
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

    public override Money CalculateInterest(Money p, LocalDate s, LocalDate e);

    public sealed record Tier(Percentage Rate, Money UpperLimit);
}

public enum TieredApplicationMode
{
    OnCurrentBalance,   // Look up rate by current balance; one rate for the full period.
    Marginal            // Apply tier rates like income-tax brackets, each tier only to the slice of balance in that bracket.
}
```

- **Formula (OnCurrentBalance):** find the first tier where `currentBalance <= UpperLimit`; apply that rate across the period.
- **Formula (Marginal):** for each tier, compute `min(currentBalance, upper) - previousUpper` and apply that tier's rate; sum.
- **Use case:** balance-banded pricing on revolving-flavoured term products.
- **Invariants:** at least one tier; tiers sorted ascending by `UpperLimit`; the last tier's `UpperLimit` is effectively ∞ in practice (represented as `Money.MaxValue` or a conventional cap).

**The Promissio bug to avoid:** `TieredRate` must be called with **current balance**, not the original principal, for `OnCurrentBalance` lookup. The signature `CalculateInterest(Money p, ...)` takes "principal for this period" — the caller's responsibility to pass the current remaining balance. The documentation and the method name inside the module (`CalculateInterestOnBalance`) make this explicit.

## `EffectiveRate` (APRC)

```csharp
public sealed record EffectiveRate(
    Percentage AnnualPercentageRate,
    DayCountConvention DayCountConvention) : InterestRate
{
    public override Percentage NominalRate => AnnualPercentageRate;

    public override Money CalculateInterest(Money p, LocalDate s, LocalDate e);
}
```

- **Formula:** `interest = principal × ((1 + APR)^τ − 1)` where `τ = dayCountFraction(s, e)`. This is **compound** interest derived from the effective annual rate, **not** simple interest. See [`aprc.md`](aprc.md) for the full APRC definition.
- **Use case:** regulatory disclosure rate (per EU Consumer Credit Directive 2008/48/EC and 2023/2225). Used when the "annual effective rate" is what has been quoted to the borrower; the schedule is derived from it.
- **Invariants:** `AnnualPercentageRate` ≥ 0, ≤ 100%.

**The Promissio bug to avoid:** applying APRC as if it were simple interest (`principal × rate × fraction`) is wrong. APRC is defined via compounding; using the compound form here is the whole point of having a separate subtype.

## Common rules

- All rates are immutable after construction.
- Rates are never "changed" on an existing loan — they are replaced, and the replacement is a modelled business event (not in scope until a restructuring feature lands).
- Day-count conventions belong to the rate (not the loan) because different sub-periods of a floating rate can use different conventions in principle. We don't currently exploit this, but the shape leaves room for it.
- `CalculateInterest` works in absolute, compounding-free terms for Fixed / Floating / Tiered. Compounding happens at the schedule-generator level or, for `EffectiveRate`, inside the rate itself.

## Testing

Each rate type has:

1. Unit tests on `CalculateInterest` with explicit input/output pairs.
2. At least three independent reference cases taken from ISDA 2006 Definitions or ECB illustrative material, cited in the test class's XML comment.
3. Property-based invariants (FsCheck.Xunit):
   - Zero principal → zero interest.
   - Zero period (`s == e`) → zero interest.
   - Linearity in principal for Fixed / Floating: `interest(2p) == 2 × interest(p)`.
   - Rate × fraction symmetry for Fixed: `interest(p, 0, 1yr) == p × rate × 1` within rounding tolerance for 30/360.
4. For `EffectiveRate`: round-trip property — recovering APR from a generated schedule matches within four decimal places.
