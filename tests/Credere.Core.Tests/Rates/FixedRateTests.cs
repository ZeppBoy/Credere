using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.ValueObjects;
using NodaTime;
using NodaTime.Text;
using Xunit;

namespace Credere.Core.Tests.Rates;

/// <summary>
/// Tests <see cref="FixedRate"/> through <see cref="InterestCalculator"/> against reference vectors that the owner
/// approved before the implementation was written (PR #40, 2026-11-19).
/// </summary>
/// <remarks>
/// <para>
/// Formula under test: <c>interest = balance × (nominalRate / 100) × dayCountFraction(periodStart, periodEnd)</c>,
/// evaluated exactly and rounded once, to two places with <c>MidpointRounding.ToEven</c>, when the
/// <c>Money</c> result is created. The period includes <c>periodStart</c> and excludes <c>periodEnd</c>.
/// </para>
/// <para>
/// Sources: (1) the four published rows are the Actual/Actual (ISDA) worked examples in ISDA, "EMU and Market
/// Conventions: Recent Developments" (memorandum ISDA-BS:9951.1, November 1998), each on £10,000 at 10%, with the
/// interest the memo prints, https://www.isda.org/a/AIJEE/1998-ISDA-memo-EMU-and-Market-Conventions-Recent-Developments.pdf ;
/// (2) the day-count fractions come from ISDA 2006 Definitions §4.16(b), (d), (e), (f) and (g), as specified in
/// docs/domain/day-counts.md; (3) the rounding rule is docs/domain/value-objects.md, "Money" (Microsoft Learn,
/// "MidpointRounding Enum"; IEEE 754-2019 §4.3.1 roundTiesToEven). The other rows are formula-derived, with the
/// arithmetic in each comment. Every expected value was computed independently with exact rational arithmetic
/// (Python <c>fractions</c>) and rounded half-even once at the end, not by the implementation.
/// </para>
/// </remarks>
public sealed class FixedRateTests
{
    /// <summary>
    /// Gets nominal rate in percent, convention name, balance, currency, ISO start date, ISO end date (exclusive),
    /// and expected interest (published rows).
    /// </summary>
    public static TheoryData<decimal, string, decimal, string, string, string, decimal> PublishedVectors => new()
    {
        { 10m, "ACT/ACT (ISDA)", 10000.00m, "GBP", "2003-11-01", "2004-05-01", 497.72m }, // p. 3: 61/365 + 121/366, memo £497.72.
        { 10m, "ACT/ACT (ISDA)", 10000.00m, "GBP", "1999-02-01", "1999-07-01", 410.96m }, // pp. 4-5: 150/365, memo £410.96.
        { 10m, "ACT/ACT (ISDA)", 10000.00m, "GBP", "1999-07-01", "2000-07-01", 1001.38m }, // pp. 4-5: 184/365 + 182/366, memo £1,001.38.
        { 10m, "ACT/ACT (ISDA)", 10000.00m, "GBP", "2002-08-15", "2003-07-15", 915.07m }, // p. 6: 334/365, memo £915.07.
    };

    /// <summary>
    /// Gets nominal rate in percent, convention name, balance, currency, ISO start date, ISO end date (exclusive),
    /// and expected interest (formula-derived rows).
    /// </summary>
    public static TheoryData<decimal, string, decimal, string, string, string, decimal> FormulaDerivedVectors => new()
    {
        { 5m, "ACT/360", 100000.00m, "EUR", "2024-01-01", "2024-02-01", 430.56m }, // 31 days: 100000 x 0.05 x 31/360.
        { 5m, "ACT/365 Fixed", 100000.00m, "EUR", "2024-01-01", "2024-02-01", 424.66m }, // 31 days: 100000 x 0.05 x 31/365.
        { 5m, "ACT/ACT (ISDA)", 100000.00m, "EUR", "2024-01-01", "2024-02-01", 423.5m }, // 31 days in a leap year: 100000 x 0.05 x 31/366.
        { 5m, "30/360 ISDA", 100000.00m, "EUR", "2024-01-01", "2024-02-01", 416.67m }, // 30 days: 100000 x 0.05 x 30/360.
        { 5m, "30E/360", 100000.00m, "EUR", "2024-01-01", "2024-02-01", 416.67m }, // 30 days: 100000 x 0.05 x 30/360.
        { 6m, "30/360 ISDA", 50000.00m, "EUR", "2024-01-29", "2024-03-31", 516.67m }, // D1 29, D2 31 kept: 62 days: 50000 x 0.06 x 62/360.
        { 6m, "30E/360", 50000.00m, "EUR", "2024-01-29", "2024-03-31", 508.33m }, // D2 31 becomes 30: 61 days: 50000 x 0.06 x 61/360.
        { 4.5m, "ACT/365 Fixed", 25000.00m, "EUR", "2023-12-15", "2025-01-15", 1223.63m }, // year-end crossing, leap 2024: 397 days: 25000 x 0.045 x 397/365.
        { 3.75m, "ACT/360", 1000000.00m, "USD", "2024-02-28", "2024-03-01", 208.33m }, // leap day inside: 2 days: 1000000 x 0.0375 x 2/360.
        { 12m, "ACT/ACT (ISDA)", 10000.00m, "GBP", "2023-01-01", "2025-01-01", 2400m }, // two whole years: 10000 x 0.12 x 2.
        { 100m, "ACT/365 Fixed", 12345.67m, "EUR", "2023-01-01", "2024-01-01", 12345.67m }, // 100% for 365 days returns the balance.
        { 0m, "ACT/360", 12345.67m, "EUR", "2024-01-01", "2024-07-01", 0m }, // zero rate.
        { 7.25m, "ACT/360", 0.00m, "EUR", "2024-01-01", "2024-07-01", 0m }, // zero balance.
        { 7.25m, "30/360 ISDA", 9999.99m, "EUR", "2024-05-10", "2024-05-10", 0m }, // empty period.
        { 8m, "ACT/360", 1.00m, "EUR", "2024-01-01", "2024-12-31", 0.08m }, // tiny balance: 1 x 0.08 x 365/360 = 0.0811.
        { 2.5m, "ACT/ACT (ISDA)", -5000.00m, "EUR", "2024-06-01", "2024-09-01", -31.42m }, // negative balance gives negative interest: -5000 x 0.025 x 92/366.
        { 1m, "ACT/360", 100.00m, "EUR", "2024-01-01", "2024-01-28", 0.08m }, // exact tie 7.5c, floor 7 odd, rounds up: 100.00 x 1% x 27/360 = 0.075.
        { 1.5m, "ACT/365 Fixed", 73.00m, "EUR", "2024-01-01", "2024-01-06", 0.02m }, // exact tie 1.5c, floor odd, rounds up: 73.00 x 1.5% x 5/365.
        { 5m, "ACT/360", 100.00m, "EUR", "2024-01-01", "2024-01-10", 0.12m }, // exact tie 12.5c, floor 12 even, rounds down: 100.00 x 5% x 9/360 = 0.125.
    };

    /// <summary>
    /// Gets rate in percent, convention name, balance, number of days from 2024-01-01, and expected interest, for
    /// exact half-cent ties under a repeating day-count fraction (added after the owner approved the vectors above).
    /// </summary>
    // Each row is an exact tie, derived with integer arithmetic: cents = B x R / 10000 x d / 360, with B in cents and R in
    // hundredths of a percent. The cents value shown is the exact half-cent figure, and the expected interest goes to
    // the even cent. A plain decimal evaluation puts a 28-digit fraction such as 20/360 a hair above or below the tie,
    // and all 6 of these rows round the wrong way; a scan of 13.5 million exact ties found about 30% wrong. The exact
    // evaluation of ExactInterest rounds every one of them correctly.
    public static TheoryData<decimal, string, decimal, int, decimal> RepeatingFractionTieVectors => new()
    {
        { 0.06m, "ACT/360", 1950.00m, 20, 0.06m }, // 6.5 cents: floor 6 even, down.
        { 0.06m, "ACT/360", 1931.25m, 160, 0.52m }, // 51.5 cents: floor 51 odd, up.
        { 0.06m, "ACT/360", 1935.00m, 200, 0.64m }, // 64.5 cents: floor 64 even, down.
        { 0.07m, "ACT/360", 1912.50m, 160, 0.60m }, // 59.5 cents: floor 59 odd, up.
        { 0.07m, "ACT/360", 1710.00m, 200, 0.66m }, // 66.5 cents: floor 66 even, down.
        { 0.07m, "ACT/360", 1800.00m, 110, 0.38m }, // 38.5 cents: floor 38 even, down.
    };

    private static readonly InterestCalculator Calculator = new();

    private static DayCountConvention Convention(string name) => name switch
    {
        "ACT/360" => new Act360(),
        "ACT/365 Fixed" => new Act365Fixed(),
        "ACT/ACT (ISDA)" => new ActActIsda(),
        "30/360 ISDA" => new Thirty360Isda(),
        "30E/360" => new ThirtyE360(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown convention in the vector data."),
    };

    private static LocalDate Date(string iso) => LocalDatePattern.Iso.Parse(iso).Value;

    private static Money CalculateFor(
        decimal ratePercent, string convention, decimal balance, string currency, string start, string end)
    {
        var rate = FixedRate.Create(new Percentage(ratePercent), Convention(convention)).Value;
        var money = Money.Create(balance, currency).Value;

        return Calculator.Calculate(rate, money, Date(start), Date(end));
    }

    [Theory]
    [MemberData(nameof(PublishedVectors))]
    [MemberData(nameof(FormulaDerivedVectors))]
    public void Calculate_matchesTheReferenceInterest_inTheBalancesCurrency(
        decimal ratePercent, string convention, decimal balance, string currency, string start, string end,
        decimal expected)
    {
        var interest = CalculateFor(ratePercent, convention, balance, currency, start, end);

        Assert.Equal(expected, interest.Amount);
        Assert.Equal(currency, interest.Currency);
    }

    [Fact]
    public void Calculate_halfCentTie_roundsToTheEvenCent_whenTheProductIsOrderedPrincipalTimesRateFirst()
    {
        // 73.00 x 1.5% x 5/365 is exactly 0.015. Multiplying 1.5% by 5/365 first gives 0.01499...96, which would
        // round down to 0.01; principal times rate first gives 0.015, which ties to the even neighbour 0.02.
        Assert.Equal(0.02m, CalculateFor(1.5m, "ACT/365 Fixed", 73.00m, "EUR", "2024-01-01", "2024-01-06").Amount);
    }

    [Theory]
    [MemberData(nameof(RepeatingFractionTieVectors))]
    public void Calculate_exactHalfCentTie_underARepeatingFraction_goesToTheEvenCent(
        decimal ratePercent, string convention, decimal balance, int days, decimal expected)
    {
        var end = new LocalDate(2024, 1, 1).PlusDays(days);

        var interest = CalculateFor(ratePercent, convention, balance, "EUR", "2024-01-01", LocalDatePattern.Iso.Format(end));

        Assert.Equal(expected, interest.Amount);
    }

    [Fact]
    public void Calculate_justAboveATie_stillRoundsUp_evenWithAManyDigitRate()
    {
        // 100.00 x 5.000000000001% x 9/360 is 0.125 plus 2.5e-14: above the tie by far more than decimal noise,
        // so it must round up to 0.13, not be mistaken for the tie (which would go down to 0.12).
        Assert.Equal(
            0.13m,
            CalculateFor(5.000000000001m, "ACT/360", 100.00m, "EUR", "2024-01-01", "2024-01-10").Amount);
    }

    [Fact]
    public void Calculate_justBelowATie_stillRoundsDown()
    {
        // 100.00 x 4.999999999999% x 9/360 is 0.125 minus 2.5e-14, which rounds down to 0.12.
        Assert.Equal(
            0.12m,
            CalculateFor(4.999999999999m, "ACT/360", 100.00m, "EUR", "2024-01-01", "2024-01-10").Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5.25)]
    [InlineData(100)]
    public void Create_acceptsRatesFromZeroToOneHundredPercent(double percent)
    {
        var result = FixedRate.Create(new Percentage((decimal)percent), new Act360());

        Assert.True(result.IsSuccess);
        Assert.Equal((decimal)percent, result.Value.NominalRate.Value);
        Assert.Equal(new Act360(), result.Value.DayCountConvention);
    }

    [Theory]
    [InlineData(100.01)]
    [InlineData(100.0000001)]
    [InlineData(250)]
    public void Create_rateAboveOneHundredPercent_returnsAValidationErrorOnNominalRate(double percent)
    {
        var result = FixedRate.Create(new Percentage((decimal)percent), new Act360());

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal("nominalRate", error.Field);
        Assert.Contains("100%", error.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_nullArguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => FixedRate.Create(null!, new Act360()));
        Assert.Throws<ArgumentNullException>(() => FixedRate.Create(new Percentage(5m), null!));
    }

    [Fact]
    public void Equality_isByRateAndConvention()
    {
        var a = FixedRate.Create(new Percentage(5m), new Act360()).Value;

        Assert.Equal(a, FixedRate.Create(new Percentage(5.0m), new Act360()).Value);
        Assert.NotEqual(a, FixedRate.Create(new Percentage(5.1m), new Act360()).Value);
        Assert.NotEqual(a, FixedRate.Create(new Percentage(5m), new Act365Fixed()).Value);
    }

    [Fact]
    public void ToString_namesTheRateAndTheConvention()
    {
        var rate = FixedRate.Create(new Percentage(5.25m), new Act365Fixed()).Value;

        Assert.Equal("5.25% (ACT/365 Fixed)", rate.ToString());
    }

    // Also covered in InterestCalculatorTests (reversed period, nulls, delegation) and FixedRatePropertiesTests
    // (zero balance, empty period, linearity within one cent, and an exact-arithmetic oracle).
}
