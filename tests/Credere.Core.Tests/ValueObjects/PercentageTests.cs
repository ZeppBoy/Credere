using Credere.Core.Results;
using Credere.Core.ValueObjects;
using FsCheck.Xunit;
using Xunit;

namespace Credere.Core.Tests.ValueObjects;

/// <summary>Tests <see cref="Percentage"/>.</summary>
public sealed class PercentageTests
{
    // A percentage with up to four decimal places, built from an integer so FsCheck input is exact in decimal.
    private static Percentage Make(uint hundredthsOfAPercent) => new(hundredthsOfAPercent / 100m);

    /// <summary>Gets a percent number and the fraction it equals, worked by hand: fraction = percent / 100.</summary>
    // Formula-derived (docs/domain/value-objects.md, "Percentage"): 5.25% is 0.0525, and so on.
    public static TheoryData<decimal, decimal> PercentToFraction => new()
    {
        { 0m, 0m },
        { 5.25m, 0.0525m },
        { 100m, 1m },
        { 12.5m, 0.125m },
        { 0.01m, 0.0001m },
        { 250m, 2.5m },
    };

    /// <summary>Gets a percent number and its text form.</summary>
    public static TheoryData<decimal, string> Text => new()
    {
        { 5.25m, "5.25%" },
        { 5.250m, "5.25%" },
        { 0m, "0%" },
        { 100m, "100%" },
        { 0.5m, "0.5%" },
    };

    [Theory]
    [MemberData(nameof(PercentToFraction))]
    public void Fraction_isThePercentNumberDividedByOneHundred(decimal percent, decimal fraction)
    {
        Assert.Equal(fraction, new Percentage(percent).Fraction);
        Assert.Equal(percent, Percentage.FromFraction(fraction).Value);
    }

    [Theory]
    [MemberData(nameof(Text))]
    public void ToString_isTheNumberWithoutTrailingZerosAndAPercentSign(decimal percent, string expected)
    {
        Assert.Equal(expected, new Percentage(percent).ToString());
    }

    [Fact]
    public void Zero_isZeroPercent()
    {
        Assert.Equal(0m, Percentage.Zero.Value);
        Assert.Equal(new Percentage(0m), Percentage.Zero);
    }

    [Fact]
    public void Create_acceptsZeroAndPositiveValues()
    {
        Assert.Equal(0m, Percentage.Create(0m).Value.Value);
        Assert.Equal(5.25m, Percentage.Create(5.25m).Value.Value);
    }

    [Fact]
    public void Create_negativeValue_returnsAValidationError()
    {
        var result = Percentage.Create(-0.01m);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal("value", error.Field);
    }

    [Fact]
    public void Constructor_andFromFraction_negative_throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Percentage(-0.01m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Percentage.FromFraction(-0.0001m));
    }

    [Fact]
    public void Operators_addSubtractAndCompareThePercentNumbers()
    {
        var a = new Percentage(5.25m);
        var b = new Percentage(1.75m);

        Assert.Equal(new Percentage(7m), a + b);
        Assert.Equal(new Percentage(3.5m), a - b);
        Assert.True(b < a);
        Assert.True(a > b);
        var sameAsA = new Percentage(5.25m);
        Assert.False(a < sameAsA);
        Assert.False(a > sameAsA);
    }

    [Fact]
    public void Subtraction_thatWouldGoNegative_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Percentage(1m) - new Percentage(2m));
    }

    [Fact]
    public void Operators_nullOperand_throw()
    {
        var a = new Percentage(1m);

        Assert.Throws<ArgumentNullException>(() => a + null!);
        Assert.Throws<ArgumentNullException>(() => null! - a);
        Assert.Throws<ArgumentNullException>(() => a < null!);
        Assert.Throws<ArgumentNullException>(() => null! > a);
    }

    [Fact]
    public void Equality_isByValue_andIgnoresTrailingZeros()
    {
        Assert.Equal(new Percentage(5.25m), new Percentage(5.250m));
        Assert.NotEqual(new Percentage(5.25m), new Percentage(5.26m));
    }

    [Property]
    public bool Addition_isCommutative(uint a, uint b)
    {
        return Make(a % 10_000_000) + Make(b % 10_000_000) == Make(b % 10_000_000) + Make(a % 10_000_000);
    }

    [Property]
    public bool Addition_isAssociative(uint a, uint b, uint c)
    {
        var (x, y, z) = (Make(a % 10_000_000), Make(b % 10_000_000), Make(c % 10_000_000));

        return (x + y) + z == x + (y + z);
    }

    [Property]
    public bool Addition_zeroIsTheIdentity(uint a)
    {
        var x = Make(a % 10_000_000);

        return x + Percentage.Zero == x && Percentage.Zero + x == x;
    }

    [Property]
    public bool Subtraction_cancelsAddition(uint a, uint b)
    {
        var (x, y) = (Make(a % 10_000_000), Make(b % 10_000_000));

        return (x + y) - y == x;
    }

    [Property]
    public bool Fraction_roundTripsThroughFromFraction(uint a)
    {
        var x = Make(a % 10_000_000);

        return Percentage.FromFraction(x.Fraction) == x;
    }

    [Property]
    public bool Ordering_isConsistentWithTheValue(uint a, uint b)
    {
        var (x, y) = (Make(a % 10_000_000), Make(b % 10_000_000));

        return (x < y) == (x.Value < y.Value) && (x > y) == (x.Value > y.Value);
    }
}
