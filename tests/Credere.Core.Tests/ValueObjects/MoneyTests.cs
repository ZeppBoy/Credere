using Xunit;

namespace Credere.Core.Tests.ValueObjects;

/// <summary>
/// Reference data for <c>Money</c> (docs/domain/value-objects.md, "Money"). The tests that use it come with the
/// implementation, after the owner has approved these vectors.
/// </summary>
/// <remarks>
/// <para>
/// Rule under test: a <c>Money</c> stores its amount to two decimal places, rounded with
/// <c>MidpointRounding.ToEven</c> every time one is created, including as the result of <c>*</c> and <c>/</c>.
/// Addition and subtraction of two-place amounts are exact. All amounts below are in one currency, "EUR".
/// </para>
/// <para>
/// Sources: (1) Microsoft Learn, "MidpointRounding Enum", Remarks table "Round to nearest" (ToEven column) and the
/// Examples section (3.45 → 3.4), https://learn.microsoft.com/en-us/dotnet/api/system.midpointrounding , read
/// 2026-11-19; (2) IEEE 754-2019 §4.3.1, roundTiesToEven, the same rule. Both are published for 0 and 1 decimal
/// places, so the published rows are scaled by 0.01 (or 0.1) to the two-place case; scaling by a power of ten
/// doesn't change which neighbour is even. The other rows are formula-derived: each value is exactly a tie, or
/// just either side of one, and the neighbouring digit decides. Every expected value was computed independently
/// with exact decimal arithmetic (Python <c>decimal</c>, ROUND_HALF_EVEN), not by the implementation.
/// </para>
/// </remarks>
public sealed class MoneyTests
{
    /// <summary>Gets an amount passed to <c>Money.Create</c> and the amount stored (published rows).</summary>
    public static TheoryData<decimal, decimal> PublishedCreateVectors => new()
    {
        { 0.035m, 0.04m }, // MS table 3.5→4 scaled by 0.01.
        { 0.028m, 0.03m }, // MS table 2.8→3 scaled.
        { 0.025m, 0.02m }, // MS table 2.5→2 scaled.
        { 0.021m, 0.02m }, // MS table 2.1→2 scaled.
        { -0.021m, -0.02m }, // MS table -2.1→-2 scaled.
        { -0.025m, -0.02m }, // MS table -2.5→-2 scaled.
        { -0.028m, -0.03m }, // MS table -2.8→-3 scaled.
        { -0.035m, -0.04m }, // MS table -3.5→-4 scaled.
        { 0.345m, 0.34m }, // MS example 3.45→3.4 (1 dp) scaled by 0.1.
        { -0.345m, -0.34m }, // MS example -3.45→-3.4 (1 dp) scaled by 0.1.
    };

    /// <summary>Gets an amount passed to <c>Money.Create</c> and the amount stored (formula-derived rows).</summary>
    public static TheoryData<decimal, decimal> FormulaDerivedCreateVectors => new()
    {
        { 0.125m, 0.12m }, // tie, previous digit 2 even.
        { 0.135m, 0.14m }, // tie, previous digit 3 odd.
        { 0.145m, 0.14m }, // tie, previous digit 4 even.
        { 0.155m, 0.16m }, // tie, previous digit 5 odd.
        { 1.005m, 1.00m }, // tie, previous digit 0 even.
        { 1.015m, 1.02m }, // tie, previous digit 1 odd.
        { 2.675m, 2.68m }, // tie, previous digit 7 odd.
        { 0.005m, 0.00m }, // tie to 0.00.
        { 0.015m, 0.02m }, // tie to 0.02.
        { 0.0049999m, 0.00m }, // just below a tie.
        { 0.0050001m, 0.01m }, // just above a tie.
        { 12.344m, 12.34m }, // below a tie.
        { 12.346m, 12.35m }, // above a tie.
        { 100m, 100.00m }, // already whole.
        { 0m, 0.00m }, // zero.
        { 1234567890.125m, 1234567890.12m }, // large tie, previous digit 2 even.
        { -0.125m, -0.12m }, // negative tie, previous digit 2 even.
        { -0.135m, -0.14m }, // negative tie, previous digit 3 odd.
        { 19.99m, 19.99m }, // already two places.
    };

    /// <summary>Gets an amount, a factor, and the product as one rounded <c>Money</c>.</summary>
    // Formula-derived: the exact product is shown in each comment; it is rounded once, at the end.
    public static TheoryData<decimal, decimal, decimal> MultiplicationVectors => new()
    {
        { 10.00m, 0.125m, 1.25m }, // exact product.
        { 100.01m, 0.5m, 50.00m }, // 50.005 tie, 0 even.
        { 100.03m, 0.5m, 50.02m }, // 50.015 tie, 1 odd.
        { 0.05m, 0.5m, 0.02m }, // 0.025 tie.
        { 0.07m, 0.5m, 0.04m }, // 0.035 tie.
        { 19.99m, 3m, 59.97m }, // exact product.
        { 99.99m, 0.0525m, 5.25m }, // 5.249475.
        { 333.33m, 0.15m, 50.00m }, // 49.9995.
        { 2500.00m, 0.035m, 87.50m }, // exact product.
        { -10.00m, 0.125m, -1.25m }, // negative amount.
        { 10.00m, -0.125m, -1.25m }, // negative factor.
        { 0.01m, 0.5m, 0.00m }, // 0.005 tie, 0 even.
        { 0.03m, 0.5m, 0.02m }, // 0.015 tie, 1 odd.
        { 1234.56m, 1.07m, 1320.98m }, // 1320.9792.
        { 12345.67m, 0m, 0.00m }, // zero factor.
        { 12345.67m, 1m, 12345.67m }, // identity factor.
    };

    /// <summary>Gets an amount, a divisor, and the quotient as one rounded <c>Money</c>.</summary>
    // Formula-derived: the exact quotient is shown in each comment; it is rounded once, at the end.
    public static TheoryData<decimal, decimal, decimal> DivisionVectors => new()
    {
        { 10.00m, 3m, 3.33m }, // 3.3333….
        { 10.00m, 8m, 1.25m }, // 1.25 exact.
        { 100.00m, 6m, 16.67m }, // 16.6666….
        { 0.05m, 2m, 0.02m }, // 0.025 tie.
        { 0.15m, 2m, 0.08m }, // 0.075 tie, 7 odd.
        { 0.01m, 3m, 0.00m }, // 0.0033….
        { 1.00m, 8m, 0.12m }, // 0.125 tie, 2 even.
        { 1.00m, 16m, 0.06m }, // 0.0625.
        { 5.00m, -2m, -2.50m }, // -2.50 exact.
        { 1000.00m, 7m, 142.86m }, // 142.857142….
        { -10.00m, 3m, -3.33m }, // -3.3333….
        { 0.00m, 5m, 0.00m }, // zero dividend.
    };

    /// <summary>Gets two amounts, their exact sum, and their exact difference (left minus right).</summary>
    // Formula-derived: two-place amounts add and subtract exactly, so no rounding applies.
    public static TheoryData<decimal, decimal, decimal, decimal> AdditionVectors => new()
    {
        { 0.10m, 0.20m, 0.30m, -0.10m },
        { 19.99m, 0.01m, 20.00m, 19.98m },
        { 1000000.00m, 0.01m, 1000000.01m, 999999.99m },
        { -5.50m, 5.50m, 0.00m, -11.00m },
        { 0.01m, -0.02m, -0.01m, 0.03m },
    };

    // Failure cases the tests must cover (docs/domain/value-objects.md, "Money", Invariants):
    //   Money.Create(1m, null / "" ) returns a ValidationError whose Field is "currency".
    //   Adding, subtracting or comparing two amounts in different currencies throws InvalidOperationException.
    //   Dividing by zero throws DivideByZeroException.
    // The doc doesn't say whether a whitespace-only currency is invalid. The implementation will treat it as
    // empty; the owner is asked to confirm in the pull request.
}
