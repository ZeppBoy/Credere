using System.Numerics;
using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.ValueObjects;
using NodaTime;
using NodaTime.Text;
using Xunit;

namespace Credere.Core.Tests.Rates;

/// <summary>
/// Reference data for exact half-even rounding of simple interest (M1-22, docs/domain/rates.md, "IInterestCalculator").
/// The owner approved these vectors (PR #47, 2026-11-20) before the implementation was written.
/// </summary>
/// <remarks>
/// <para>
/// Rule under test: <c>interest = balance × (nominalRate / 100) × dayCountFraction</c> is evaluated exactly (as a rational
/// number) and rounded once to whole cents with <c>MidpointRounding.ToEven</c>. A product exactly halfway between two cents
/// goes to the even cent; any other product goes to the nearer cent, however small the distance from the halfway point.
/// The sign follows the balance, so a negative balance rounds the magnitude and keeps its sign.
/// </para>
/// <para>
/// Sources: the rounding rule is docs/domain/value-objects.md, "Money" (Microsoft Learn, "MidpointRounding Enum";
/// IEEE 754-2019 §4.3.1 roundTiesToEven); the day-count fractions are ISDA 2006 Definitions §4.16 as specified in
/// docs/domain/day-counts.md. The first five rows come from the M1-08 implementation note and the cross-vendor code review
/// (PR #40); they and their expected values are listed in issue #46. Every expected value
/// below was recomputed independently with exact rational arithmetic (Python <c>fractions</c>), not by the implementation.
/// </para>
/// </remarks>
public sealed class ExactInterestTests
{
    /// <summary>
    /// Gets nominal rate in percent, convention name, balance, currency, ISO start date, ISO end date (exclusive),
    /// and expected interest. The first five rows are the regressions listed in issue #46.
    /// </summary>
    public static TheoryData<decimal, string, decimal, string, string, string, decimal> ExactRoundingVectors => new()
    {
        // Exact tie with a repeating fraction: 1950.00 x 0.0006 x 20/360 = 0.065 exactly; floor 6 is even, so 0.06.
        // A plain decimal evaluation of 1950m * (0.06m / 100m) * (20m / 360m) gives 0.0650000000000000000000000001m on .NET 10, which
        // rounds up to 0.07 under plain half-even (the problem M1-08's tolerance worked around).
        { 0.06m, "ACT/360", 1950.00m, "EUR", "2024-01-01", "2024-01-21", 0.06m },

        // Just above a tie: 100.00 x 0.0500000000000000000000001 x 9/360 = 0.12500000000000000000000025 exactly, so 0.13.
        // The 1e-22 tolerance of the M1-08 implementation wrongly returns 0.12 (Codex P1 finding on PR #40).
        { 5.00000000000000000000001m, "ACT/360", 100.00m, "EUR", "2024-01-01", "2024-01-10", 0.13m },

        // Largest supported amount: decimal.MaxValue x 100% x 365/365 = decimal.MaxValue exactly (Codex P2 finding on PR #40:
        // the intermediate product overflowed).
        { 100m, "ACT/365 Fixed", decimal.MaxValue, "EUR", "2023-01-01", "2024-01-01", decimal.MaxValue },

        // Smallest representable rate percentage: decimal.MaxValue x 1e-30 = 0.079228162514264337593543950335 exactly, so 0.08.
        // The rate fraction 1e-30 itself is below decimal's 28-digit scale, so it must not be materialised as a decimal.
        { 0.0000000000000000000000000001m, "ACT/365 Fixed", decimal.MaxValue, "EUR", "2023-01-01", "2024-01-01", 0.08m },

        // Negative balance: the same exact tie as the first row, magnitude 0.065 rounds to 0.06, sign kept.
        { 0.06m, "ACT/360", -1950.00m, "EUR", "2024-01-01", "2024-01-21", -0.06m },

        // Added by the implementer, not in issue #46 (owner to accept or drop): just below the tie of the second row.
        // 100.00 x 0.0499999999999999999999999 x 9/360 = 0.12499999999999999999999975 exactly, so 0.12.
        { 4.99999999999999999999999m, "ACT/360", 100.00m, "EUR", "2024-01-01", "2024-01-10", 0.12m },
    };

    private static readonly InterestCalculator Calculator = new();

    private static DayCountConvention Convention(string name) => name switch
    {
        "ACT/360" => new Act360(),
        "ACT/365 Fixed" => new Act365Fixed(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown convention in the vector data."),
    };

    private static LocalDate Date(string iso) => LocalDatePattern.Iso.Parse(iso).Value;

    [Theory]
    [MemberData(nameof(ExactRoundingVectors))]
    public void Calculate_roundsTheExactInterestOnceHalfEven(
        decimal ratePercent, string convention, decimal balance, string currency, string start, string end,
        decimal expected)
    {
        var rate = FixedRate.Create(new Percentage(ratePercent), Convention(convention)).Value;

        var interest = Calculator.Calculate(rate, Money.Create(balance, currency).Value, Date(start), Date(end));

        Assert.Equal(expected, interest.Amount);
        Assert.Equal(currency, interest.Currency);
    }

    [Fact]
    public void Calculate_resultAboveTheMaximumAmount_throwsOverflowException()
    {
        // 100% over two years on decimal.MaxValue is twice the largest amount Money can hold.
        var rate = FixedRate.Create(new Percentage(100m), new Act365Fixed()).Value;
        var balance = Money.Create(decimal.MaxValue, "EUR").Value;

        Assert.Throws<OverflowException>(
            () => Calculator.Calculate(rate, balance, Date("2023-01-01"), Date("2025-01-01")));
    }

    [Fact]
    public void Calculate_customConvention_usesTheExactValueOfTheDecimalItReturns()
    {
        // 1/3 as 28 digits: 3000.00 x 100% x 0.3333333333333333333333333333 = 999.9999999999999999999999999, which is 1000.00.
        var rate = FixedRate.Create(new Percentage(100m), new OneThirdConvention()).Value;

        var interest = Calculator.Calculate(
            rate, Money.Create(3000.00m, "EUR").Value, Date("2024-01-01"), Date("2024-02-01"));

        Assert.Equal(1000.00m, interest.Amount);
    }

    [Fact]
    public void Calculate_customConvention_halfCentTieGoesToTheEvenCent()
    {
        // 0.5 is an exact decimal, and 1.00 x 1% x 0.5 = 0.005 is an exact tie (floor 0, even), so it rounds to 0.00.
        var rate = FixedRate.Create(new Percentage(1m), new HalfYearConvention()).Value;

        var interest = Calculator.Calculate(
            rate, Money.Create(1.00m, "EUR").Value, Date("2024-01-01"), Date("2024-07-01"));

        Assert.Equal(0.00m, interest.Amount);
    }

    [Fact]
    public void Zero_roundsToZero_andSoDoesTheDefaultValue()
    {
        Assert.Equal(0m, ExactInterest.Zero.ToMoney("EUR").Amount);
        Assert.Equal(0m, default(ExactInterest).ToMoney("EUR").Amount);
        Assert.Equal("EUR", default(ExactInterest).ToMoney("EUR").Currency);
    }

    [Fact]
    public void Sum_staysExactUntilItIsRoundedOnce()
    {
        // Each 1.00 x 0.5% x 1/1 is 0.005, a tie that rounds to 0.00 on its own; the sum 0.01 is not a tie.
        var half = ExactInterest.Of(Money.Create(1.00m, "EUR").Value, new Percentage(0.5m), new ExactYearFraction(1, 1));

        Assert.Equal(0.00m, half.ToMoney("EUR").Amount);
        Assert.Equal(0.01m, (half + half).ToMoney("EUR").Amount);
        Assert.Equal(0.01m, (ExactInterest.Zero + half + half).ToMoney("EUR").Amount);
    }

    [Fact]
    public void Sum_ofThirds_isRoundedOnceNotPerTerm()
    {
        // Three terms of 1.00 x 1% x 1/3 are 0.0033333... each, 0.01 in total, although each rounds to 0.00.
        var third = ExactInterest.Of(Money.Create(1.00m, "EUR").Value, new Percentage(1m), new ExactYearFraction(1, 3));

        Assert.Equal(0.00m, third.ToMoney("EUR").Amount);
        Assert.Equal(0.01m, (third + third + third).ToMoney("EUR").Amount);
    }

    [Theory]
    [InlineData(-5, 100, -0.05)]
    [InlineData(-6, 1000, -0.01)] // -0.006 rounds to -0.01 (not a tie).
    [InlineData(-5, 1000, 0.00)] // -0.005 is a tie: the even cent is 0.00.
    [InlineData(-15, 1000, -0.02)] // -0.015 is a tie: the even cent is 0.02.
    [InlineData(25, 1000, 0.02)] // 0.025 is a tie: the even cent is 0.02.
    [InlineData(35, 1000, 0.04)] // 0.035 is a tie: the even cent is 0.04.
    public void ToMoney_roundsTheMagnitudeHalfEven_andKeepsTheSign(int numerator, int denominator, double expected)
    {
        // Balance 1.00 at 100% gives numerator / denominator once the year fraction is that ratio.
        var interest = ExactInterest.Of(
            Money.Create(1.00m, "EUR").Value, new Percentage(100m), new ExactYearFraction(numerator, denominator));

        Assert.Equal((decimal)expected, interest.ToMoney("EUR").Amount);
    }

    [Fact]
    public void Of_nullArguments_throw()
    {
        var fraction = new ExactYearFraction(1, 1);

        Assert.Throws<ArgumentNullException>(() => ExactInterest.Of(null!, new Percentage(1m), fraction));
        Assert.Throws<ArgumentNullException>(() => ExactInterest.Of(Money.Zero("EUR"), null!, fraction));
    }

    // Other coverage: the owner-approved FixedRate and day-count vectors (FixedRateTests and the DayCounts suites) are
    // unchanged and pass; the exact fractions of the built-in conventions are checked in ExactInterestYearFractionTests.

    private sealed record OneThirdConvention : DayCountConvention
    {
        public override string Name => "custom 1/3";

        public override decimal Fraction(LocalDate start, LocalDate end) => 0.3333333333333333333333333333m;
    }

    private sealed record HalfYearConvention : DayCountConvention
    {
        public override string Name => "custom 1/2";

        public override decimal Fraction(LocalDate start, LocalDate end) => 0.5m;
    }
}
