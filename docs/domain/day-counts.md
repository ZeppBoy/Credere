# Day-count conventions

Day-count conventions govern how a year fraction is computed between two dates. Credere implements five product conventions. Each lives in `src/Credere.Core/DayCounts/`, and each has at least twenty reference vectors; [Reference vectors](#reference-vectors) says what counts as a source.

The APRC does not use any of these conventions. It measures time under Directive (EU) 2023/2225, Annex III, Part I, remark (c), implemented as `EuCcdAnnexIII`; see [`aprc.md`](aprc.md#time-intervals).

## Authoritative sources

- **ISDA 2006 Definitions**, Section 4.16 ("Day Count Fraction"). The paragraph letters below are from the 2006 text, as shown in ISDA's 2000-vs-2006 blackline.
- **ISDA, "EMU and Market Conventions: Recent Developments"** (memorandum ISDA-BS:9951.1, November 1998). Its worked examples of the ISDA actual/actual method are the published vectors for Actual/Actual (ISDA).

Cite these in the XML comment of every implementation file, with the specific paragraph or example.

## Public contract

```csharp
public abstract record DayCountConvention
{
    public abstract string Name { get; }           // "ACT/360", "ACT/365 Fixed", "30/360 ISDA", etc.

    /// <summary>
    /// Returns the year fraction between <paramref name="start"/> (inclusive) and
    /// <paramref name="end"/> (exclusive).
    /// </summary>
    public abstract decimal Fraction(LocalDate start, LocalDate end);
}
```

All implementations:

- Return 0 for `start == end`.
- Return a non-negative fraction for `end > start`. It can be 0: under both 30/360 conventions, 30 January → 31 January counts 0 days.
- Throw `ArgumentOutOfRangeException` if `end < start`. (Reverse-direction accrual is not modelled.)
- Compute in `decimal` only, with no `double` or `float` anywhere (AGENTS.md §7). The one `double` exception in Credere is the APRC solver's fractional exponent ([`aprc.md`](aprc.md#numerical-notes)).

## 1. Actual/Actual (ISDA)

`ActActIsda`. Splits the interval at year boundaries. The fraction is `days_in_non_leap_portion / 365 + days_in_leap_portion / 366`.

**Use case:** sovereign bond markets, some corporate loans.

**Reference:** ISDA 2006 Definitions §4.16(b).

## 2. Actual/360

**Status:** Implemented (M1-04).

`Act360` (`ACT/360`) divides the actual number of calendar days from the inclusive start to the exclusive end by 360. Leap days count as ordinary days. The reference tests compare fractions rounded to 12 decimal places with `MidpointRounding.ToEven`.

**Use case:** money markets (USD LIBOR convention historically, SOFR today).

**Reference:** ISDA §4.16(e).

## 3. Actual/365 Fixed

**Status:** Implemented (M1-04).

`Act365Fixed` (`ACT/365 Fixed`) divides the actual number of calendar days from the inclusive start to the exclusive end by 365, including in leap years. The reference tests compare fractions rounded to 12 decimal places with `MidpointRounding.ToEven`.

**Use case:** GBP money markets, many consumer loans.

**Reference:** ISDA §4.16(d).

## 4. 30/360 ISDA (Bond Basis)

**Status:** Implemented (M1-06).

`Thirty360Isda` (`30/360 ISDA`) weights year differences by 360 days and month differences by 30 days. The start day determines whether an end day of 31 is adjusted. February dates retain their day of month.

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

**Status:** Implemented (M1-06).

`ThirtyE360` (`30E/360`) uses the same formula for the year and month differences. Each endpoint's day of month is adjusted independently. February dates retain their day of month.

```
D1 = min(start.Day, 30)
D2 = min(end.Day, 30)
```

**Use case:** Euro-area retail loans; many EU consumer credit products.

**Reference:** ISDA §4.16(g).

## Reference vectors

Each implementation has a test class `<Name>Tests` with at least twenty reference cases. Each case gives `start`, `end`, the expected `fraction` to at least six decimal places, and its source. The ISDA 2006 Definitions give formulas but no numeric examples, so vectors come from two kinds of source, and each test case says which:

1. **Published.** A worked example from an ISDA publication. The 1998 memo has four for Actual/Actual (ISDA), each computed on £10,000 at 10%:

   | Start | End | Split | Fraction | Interest (memo) |
   |---|---|---|---|---|
   | 2003-11-01 | 2004-05-01 | 61/365 + 121/366 | 0.4977243806 | £497.72 |
   | 1999-02-01 | 1999-07-01 | 150/365 | 0.4109589041 | £410.96 |
   | 1999-07-01 | 2000-07-01 | 184/365 + 182/366 | 1.0013773486 | £1,001.38 |
   | 2002-08-15 | 2003-07-15 | 334/365 | 0.9150684932 | £915.07 |

2. **Formula-derived.** Worked by hand from the text of the §4.16 paragraph, with the arithmetic in the test comment and the case marked `formula-derived`. Together the cases must exercise every clause of the paragraph:
   - 30/360 (f): `D1 = 31`; `D2 = 31` with `D1 > 29`; `D2 = 31` with `D1 < 30`; an end of February (no adjustment); multi-year periods.
   - 30E/360 (g): `D1 = 31`; `D2 = 31`; an end of February.
   - Actual conventions: periods crossing a year end, periods containing 29 February, and multi-year periods.

   The owner reviews formula-derived vectors before the implementation is written, as `.claude/rules/financial-math.md` requires.

Example:

```csharp
// ISDA, "EMU and Market Conventions: Recent Developments" (ISDA-BS:9951.1, Nov 1998),
// short first calculation period example: 1 Nov 2003 – 1 May 2004, ISDA method,
// 61/365 + 121/366 (memo: £10,000 × 10% × fraction = £497.72).
[Theory]
[InlineData("2003-11-01", "2004-05-01", "0.4977243806")]
public void ActActIsda_MatchesIsdaReference(string s, string e, string expected) { ... }
```

## Property-based invariants (FsCheck.Xunit)

For every convention:

- `Fraction(s, s) == 0m`.
- **Non-decreasing:** `e1 ≤ e2 ⇒ Fraction(s, e1) ≤ Fraction(s, e2)`. The order is strict only for the Actual conventions. The 30/360 conventions can return the same fraction for different end dates. For example, end dates of 30 and 31 March tie under 30E/360 for any start, and under 30/360 ISDA when the start day is 30 or 31.
- **No reversal:** `Fraction(e, s)` with `e > s` throws by design.

For Actual/Actual (ISDA), Actual/360, Actual/365 Fixed and 30E/360 only:

- **Additivity:** `Fraction(s, e1) + Fraction(e1, e2) == Fraction(s, e2)` for `s ≤ e1 ≤ e2`. 30E/360 qualifies because its numerator telescopes.

30/360 ISDA is **not** additive, because its `D2` rule depends on `D1`. For example, 2024-01-15 → 2024-01-31 → 2024-02-01 sums to 16 + 1 = 17 days, while the direct fraction counts 16. Its tests assert this counterexample, so nobody "fixes" the property later.

## Why not just use Noda `Period.DaysBetween`?

NodaTime gives us calendar arithmetic (leap years, month lengths) but is intentionally **not** a day-count library. The semantics above are not "days between" — they are purely conventional, defined by market practice. Mixing these responsibilities leads to subtle off-by-one bugs. Credere keeps the arithmetic trivially auditable in one file per convention.

## Choosing a convention for a product

The product definition (loan terms) names the convention; it is part of the approved terms and is immutable after disbursement. The APRC calculator ignores the product's convention: it measures time per Directive (EU) 2023/2225, Annex III, Part I, remark (c) ([`aprc.md`](aprc.md#time-intervals)).
