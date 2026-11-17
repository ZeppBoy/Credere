# Day-count conventions

Day-count conventions govern how a year fraction is computed between two dates. Credere implements five conventions. Each lives in `src/Credere.Core/DayCounts/`. Each has at least twenty reference test vectors sourced from authoritative material.

## Authoritative sources

- **ISDA 2006 Definitions**, Section 4.16 ("Day Count Fraction").
- **ECB Statistical Classification** of interest rates, ANNEX II illustrative examples.
- **EU Consumer Credit Directive 2008/48/EC**, Annex I (APRC), uses Actual/365 for illustrative examples; see [`aprc.md`](aprc.md).

Cite these in the XML comment of every implementation file, with the specific section / example number.

## Public contract

```csharp
public abstract record DayCountConvention
{
    public abstract string Name { get; }           // "ACT/360", "30/360 ISDA", etc.

    /// <summary>
    /// Returns the year fraction between <paramref name="start"/> (inclusive) and
    /// <paramref name="end"/> (exclusive).
    /// </summary>
    public abstract decimal Fraction(LocalDate start, LocalDate end);
}
```

All implementations:

- Return 0 for `start == end`.
- Return a positive fraction for `end > start`.
- Throw `ArgumentOutOfRangeException` if `end < start`. (Reverse-direction accrual is not modelled.)
- Produce `decimal` with no intermediate `double` conversion for the counting; `double` is permitted only inside internal helpers where overflow / loss of precision is not a concern (see `AprcCalculator` for the compounding case, which is bounded and tested).

## 1. Actual/Actual (ISDA)

`ActActIsda`. Splits the interval at year boundaries. The fraction is `days_in_non_leap_portion / 365 + days_in_leap_portion / 366`.

**Use case:** sovereign bond markets, some corporate loans.

**Reference:** ISDA 2006 Definitions §4.16(b).

## 2. Actual/360

`Act360`. `fraction = (end - start) / 360`.

**Use case:** money markets (USD LIBOR convention historically, SOFR today).

**Reference:** ISDA §4.16(c).

## 3. Actual/365 Fixed

`Act365Fixed`. `fraction = (end - start) / 365`.

**Use case:** GBP money markets, many consumer loans. The EU Consumer Credit Directive uses this in its illustrative APRC examples.

**Reference:** ISDA §4.16(d); EU 2008/48/EC Annex I.

## 4. 30/360 ISDA (Bond Basis)

`Thirty360Isda`. All months treated as 30 days, all years as 360 days.

Day-count numerator per ISDA:

```
D1 = min(start.Day, 30)
D2 = (start.Day == 30 or 31) ? min(end.Day, 30) : end.Day
numerator = 360 × (end.Year - start.Year) + 30 × (end.Month - start.Month) + (D2 - D1)
fraction = numerator / 360
```

**Use case:** US corporate / Eurobond markets.

**Reference:** ISDA §4.16(f).

## 5. 30E/360 (Eurobond)

`ThirtyE360`. Similar to ISDA Bond Basis but with a simpler end-of-month treatment:

```
D1 = min(start.Day, 30)
D2 = min(end.Day, 30)
```

**Use case:** Euro-area retail loans; many EU consumer credit products.

**Reference:** ISDA §4.16(g).

## Reference vectors

Each implementation has a test class `<Name>Tests` with at least twenty reference cases. Each case specifies `start`, `end`, expected `fraction` to at least six decimal places, and the source citation.

Example:

```csharp
// ISDA 2006 Definitions, §4.16(b), illustrative example:
// Start: 2003-10-30, End: 2004-11-30. Expected: 1.0869565217 (actual/actual (ISDA)).
[Theory]
[InlineData("2003-10-30", "2004-11-30", 1.0869565217)]
public void ActActIsda_MatchesIsdaReference(string s, string e, decimal expected) { ... }
```

## Property-based invariants (FsCheck.Xunit)

Common to every convention:

- `Fraction(s, s) == 0m`.
- `Fraction(s, e1) + Fraction(e1, e2) == Fraction(s, e2)` when `s <= e1 <= e2` (**additivity**).
- Monotonic: `e1 < e2 ⇒ Fraction(s, e1) < Fraction(s, e2)`.
- Symmetric to range-reversal: `Fraction(s, e) + Fraction(e, s)` is defined only when `e >= s` — the reverse-direction call throws by design.

## Why not just use Noda `Period.DaysBetween`?

NodaTime gives us calendar arithmetic (leap years, month lengths) but is intentionally **not** a day-count library. The semantics above are not "days between" — they are purely conventional, defined by market practice. Mixing these responsibilities leads to subtle off-by-one bugs. Credere keeps the arithmetic trivially auditable in one file per convention.

## Choosing a convention for a product

The product definition (loan terms) names the convention; it is part of the approved terms and is immutable after disbursement. The APRC calculator overrides this to Actual/365 Fixed per EU Directive, independent of the product's accrual convention.
