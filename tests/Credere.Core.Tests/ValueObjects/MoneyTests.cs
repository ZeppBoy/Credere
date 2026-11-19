using Credere.Core.Results;
using Credere.Core.ValueObjects;
using Xunit;

namespace Credere.Core.Tests.ValueObjects;

/// <summary>
/// Tests <see cref="Money"/> against reference vectors that the owner approved before the implementation was
/// written (PR #36, 2026-11-19).
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

    private const string Eur = "EUR";

    private static Money Eu(decimal amount) => Money.Create(amount, Eur).Value;

    [Theory]
    [MemberData(nameof(PublishedCreateVectors))]
    [MemberData(nameof(FormulaDerivedCreateVectors))]
    public void Create_roundsToTwoPlacesHalfEven(decimal amount, decimal expected)
    {
        var result = Money.Create(amount, Eur);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Amount);
        Assert.Equal(Eur, result.Value.Currency);
    }

    [Theory]
    [MemberData(nameof(MultiplicationVectors))]
    public void Multiply_roundsTheProductOnce(decimal amount, decimal factor, decimal expected)
    {
        Assert.Equal(expected, (Eu(amount) * factor).Amount);
        Assert.Equal(expected, (factor * Eu(amount)).Amount);
    }

    [Theory]
    [MemberData(nameof(DivisionVectors))]
    public void Divide_roundsTheQuotientOnce(decimal amount, decimal divisor, decimal expected)
    {
        Assert.Equal(expected, (Eu(amount) / divisor).Amount);
    }

    [Theory]
    [MemberData(nameof(AdditionVectors))]
    public void AddAndSubtract_areExact(decimal left, decimal right, decimal sum, decimal difference)
    {
        Assert.Equal(sum, (Eu(left) + Eu(right)).Amount);
        Assert.Equal(difference, (Eu(left) - Eu(right)).Amount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("eur")]
    [InlineData("Eur")]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    [InlineData("E R")]
    [InlineData(" EUR")]
    [InlineData("EUR ")]
    [InlineData("EUR\n")]
    [InlineData("\u0415UR")] // Cyrillic capital Ie, which looks like E.
    [InlineData("\u00c9UR")] // E with an acute accent.
    public void Create_invalidCurrency_returnsAValidationErrorOnTheCurrencyField(string? currency)
    {
        var result = Money.Create(1m, currency!);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal("currency", error.Field);
        Assert.Contains("three uppercase letters", error.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData("AAA")]
    [InlineData("ZZZ")]
    public void Create_threeUppercaseLetters_isAccepted(string currency)
    {
        Assert.Equal(currency, Money.Create(1m, currency).Value.Currency);
    }

    [Fact]
    public void Zero_isZeroInTheCurrency_andEqualsCreatedZero()
    {
        var zero = Money.Zero(Eur);

        Assert.Equal(0m, zero.Amount);
        Assert.Equal(Eur, zero.Currency);
        Assert.Equal(Eu(0m), zero);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("eur")]
    [InlineData("EURO")]
    public void Zero_invalidCurrency_throws(string? currency)
    {
        var exception = Assert.Throws<ArgumentException>(() => Money.Zero(currency!));

        Assert.Equal("currency", exception.ParamName);
        Assert.Contains("three uppercase letters", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Equality_comparesNumericValueAndCurrency_notScale()
    {
        Assert.Equal(Eu(1.5m), Eu(1.50m));
        Assert.Equal(Eu(1.5m).GetHashCode(), Eu(1.50m).GetHashCode());
        Assert.NotEqual(Eu(1.5m), Money.Create(1.5m, "USD").Value);
        Assert.NotEqual(Eu(1.5m), Eu(1.51m));
    }

    [Theory]
    [InlineData(100, "100.00 EUR")]
    [InlineData(1234.5, "1234.50 EUR")]
    [InlineData(-0.125, "-0.12 EUR")]
    [InlineData(0.004, "0.00 EUR")]
    [InlineData(-0.004, "0.00 EUR")]
    [InlineData(1234567.891, "1234567.89 EUR")]
    public void ToString_showsTwoPlacesWithInvariantCulture_andNeverANegativeZero(double amount, string expected)
    {
        Assert.Equal(expected, Eu((decimal)amount).ToString());
    }

    [Fact]
    public void Amount_isKeptAtItsNaturalScale_notForcedToTwoPlaces()
    {
        // The owner chose rounding without forcing the stored scale (PR #36), so decimal.MaxValue stays creatable.
        Assert.Equal(decimal.MaxValue, Eu(decimal.MaxValue).Amount);
    }

    [Fact]
    public void Comparison_orderAmountsInOneCurrency()
    {
        var small = Eu(1.00m);
        var large = Eu(1.01m);

        Assert.True(small < large);
        Assert.True(large > small);
        Assert.True(small <= large);
        Assert.True(large >= small);
        Assert.True(small <= Eu(1.0m));
        Assert.True(small >= Eu(1.0m));
        Assert.False(small < Eu(1.0m));
        Assert.False(small > Eu(1.0m));
        Assert.False(large < small);
        Assert.False(small > large);
    }

    [Fact]
    public void MixedCurrencies_inAnyOperator_throwInvalidOperation()
    {
        var euros = Eu(1m);
        var dollars = Money.Create(1m, "USD").Value;

        var exception = Assert.Throws<InvalidOperationException>(() => euros + dollars);
        Assert.Contains("EUR", exception.Message, StringComparison.Ordinal);
        Assert.Contains("USD", exception.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => euros - dollars);
        Assert.Throws<InvalidOperationException>(() => euros < dollars);
        Assert.Throws<InvalidOperationException>(() => euros > dollars);
        Assert.Throws<InvalidOperationException>(() => euros <= dollars);
        Assert.Throws<InvalidOperationException>(() => euros >= dollars);
    }

    [Fact]
    public void DivideByZero_throws()
    {
        Assert.Throws<DivideByZeroException>(() => Eu(1m) / 0m);
    }

    [Fact]
    public void NullOperands_throwArgumentNull()
    {
        var one = Eu(1m);

        Assert.Throws<ArgumentNullException>(() => one + null!);
        Assert.Throws<ArgumentNullException>(() => null! - one);
        Assert.Throws<ArgumentNullException>(() => (null as Money)! * 2m);
        Assert.Throws<ArgumentNullException>(() => 2m * (null as Money)!);
        Assert.Throws<ArgumentNullException>(() => (null as Money)! / 2m);
        Assert.Throws<ArgumentNullException>(() => one < null!);
        Assert.Throws<ArgumentNullException>(() => null! >= one);
    }

    [Fact]
    public void Operators_keepTheCurrency()
    {
        var dollars = Money.Create(10m, "USD").Value;

        Assert.Equal("USD", (dollars + dollars).Currency);
        Assert.Equal("USD", (dollars - dollars).Currency);
        Assert.Equal("USD", (dollars * 2m).Currency);
        Assert.Equal("USD", (dollars / 2m).Currency);
    }

    // Failure cases (docs/domain/value-objects.md, "Money", Invariants), covered above: an invalid currency is a
    // ValidationError on field "currency" (owner decision, PR #36: exactly ^[A-Z]{3}$, no normalisation, so
    // whitespace-only is invalid); mixing currencies throws InvalidOperationException; dividing by zero throws
    // DivideByZeroException.
}
