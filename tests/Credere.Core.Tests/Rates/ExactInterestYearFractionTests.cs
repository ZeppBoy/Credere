using System.Numerics;
using Credere.Core.DayCounts;
using FsCheck.Xunit;
using NodaTime;
using Xunit;

namespace Credere.Core.Tests.Rates;

/// <summary>
/// Tests <see cref="ExactYearFraction"/> and the exact fractions of the built-in day-count conventions, which
/// share their day-count calculation with the public <see cref="DayCountConvention.Fraction"/> (M1-22).
/// </summary>
public sealed class ExactInterestYearFractionTests
{
    private static readonly LocalDate Origin = new(1990, 1, 1);

    public static TheoryData<string, string, long, long> KnownFractions => new()
    {
        // Source: ISDA 2006 Definitions §4.16 as specified in docs/domain/day-counts.md; the numbers are the day counts.
        { "ACT/360", "2024-01-01|2024-01-21", 20, 360 },
        { "ACT/365 Fixed", "2023-01-01|2024-01-01", 365, 365 },
        { "30/360 ISDA", "2024-01-29|2024-03-31", 62, 360 }, // D1 29, D2 31 kept.
        { "30E/360", "2024-01-29|2024-03-31", 61, 360 }, // D2 31 becomes 30.
        { "ACT/ACT (ISDA)", "2003-11-01|2004-05-01", 61 * 366 + 121 * 365, 365 * 366 }, // ISDA memo p. 3: 61/365 + 121/366.
        { "ACT/ACT (ISDA)", "1999-07-01|2000-07-01", 184 * 366 + 182 * 365, 365 * 366 }, // pp. 4-5: 184/365 + 182/366.
    };

    private static DayCountConvention Convention(string name) => name switch
    {
        "ACT/360" => new Act360(),
        "ACT/365 Fixed" => new Act365Fixed(),
        "ACT/ACT (ISDA)" => new ActActIsda(),
        "30/360 ISDA" => new Thirty360Isda(),
        "30E/360" => new ThirtyE360(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown convention."),
    };

    [Theory]
    [MemberData(nameof(KnownFractions))]
    public void ExactFraction_ofTheBuiltInConventions_isTheExactRatioOfTheDayCounts(
        string convention, string period, long numerator, long denominator)
    {
        var parts = period.Split('|');
        var start = NodaTime.Text.LocalDatePattern.Iso.Parse(parts[0]).Value;
        var end = NodaTime.Text.LocalDatePattern.Iso.Parse(parts[1]).Value;

        var fraction = Convention(convention).ExactFraction(start, end);

        // Compare as ratios, so an unreduced numerator and denominator are accepted.
        Assert.Equal(numerator * fraction.Denominator, fraction.Numerator * denominator);
    }

    [Property]
    public bool ExactFraction_agreesWithTheDecimalFraction_toTwentySixDigits(
        byte convention, ushort offset, ushort length)
    {
        var names = new[] { "ACT/360", "ACT/365 Fixed", "ACT/ACT (ISDA)", "30/360 ISDA", "30E/360" };
        var day = Convention(names[convention % names.Length]);
        var (start, end) = (Origin.PlusDays(offset), Origin.PlusDays(offset).PlusDays(length % 7301));

        var exact = day.ExactFraction(start, end);
        var rounded = ExactYearFraction.FromDecimal(day.Fraction(start, end));

        // |n1/d1 - n2/d2| <= 1e-26, as |n1 d2 - n2 d1| x 1e26 <= d1 d2.
        var difference = BigInteger.Abs(exact.Numerator * rounded.Denominator - rounded.Numerator * exact.Denominator);

        return difference * BigInteger.Pow(10, 26) <= exact.Denominator * rounded.Denominator;
    }

    [Theory]
    [InlineData("ACT/360")]
    [InlineData("ACT/365 Fixed")]
    [InlineData("ACT/ACT (ISDA)")]
    [InlineData("30/360 ISDA")]
    [InlineData("30E/360")]
    public void ExactFraction_endBeforeStart_throwsArgumentOutOfRange(string convention)
    {
        var day = Convention(convention);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => day.ExactFraction(new LocalDate(2024, 1, 2), new LocalDate(2024, 1, 1)));
    }

    [Theory]
    [InlineData("-1.50", "-150", "100")]
    [InlineData("0", "0", "1")]
    [InlineData("5.25", "525", "100")]
    [InlineData("0.0000000000000000000000000001", "1", "10000000000000000000000000000")] // 1e-28: scale 28.
    public void FromDecimal_isTheCoefficientOverAPowerOfTen(string value, string numerator, string denominator)
    {
        var fraction = ExactYearFraction.FromDecimal(
            decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(BigInteger.Parse(numerator, System.Globalization.CultureInfo.InvariantCulture), fraction.Numerator);
        Assert.Equal(BigInteger.Parse(denominator, System.Globalization.CultureInfo.InvariantCulture), fraction.Denominator);
    }

    [Fact]
    public void FromDecimal_maxValue_isTheWholeNumber()
    {
        var fraction = ExactYearFraction.FromDecimal(decimal.MaxValue);

        Assert.Equal(new BigInteger(decimal.MaxValue), fraction.Numerator);
        Assert.Equal(BigInteger.One, fraction.Denominator);
    }

    [Fact]
    public void FromDecimal_minValue_isNegative()
    {
        Assert.Equal(new BigInteger(decimal.MinValue), ExactYearFraction.FromDecimal(decimal.MinValue).Numerator);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_nonPositiveDenominator_throws(int denominator)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExactYearFraction(1, denominator));
    }
}
