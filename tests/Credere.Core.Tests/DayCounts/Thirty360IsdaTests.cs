using Credere.Core.DayCounts;
using NodaTime;
using NodaTime.Text;
using Xunit;

namespace Credere.Core.Tests.DayCounts;

/// <summary>Tests the 30/360 ISDA convention.</summary>
public sealed class Thirty360IsdaTests : DayCountConventionPropertiesBase
{
    /// <inheritdoc />
    protected override DayCountConvention Convention { get; } = new Thirty360Isda();

    /// <summary>Gets start dates, exclusive end dates, and expected year fractions.</summary>
    // Formula-derived from ISDA 2006 Definitions §4.16(f), including the conditional adjustment of D2.
    // https://www.isda.org/a/smMDE/Blackline-2000-v-2006-ISDA-Definitions.pdf
    // Each comment shows the numerator divided by 360. Fractions are shown to 12 decimal places.
    public static TheoryData<string, string, decimal> ReferenceVectors => new()
    {
        { "2024-01-31", "2024-01-31", 0.000000000000m }, // (30 - 30)/360.
        { "2024-01-15", "2024-01-16", 0.002777777778m }, // (16 - 15)/360.
        { "2024-01-30", "2024-01-31", 0.000000000000m }, // (30 - 30)/360.
        { "2024-01-29", "2024-01-31", 0.005555555556m }, // (31 - 29)/360.
        { "2024-01-15", "2024-01-31", 0.044444444444m }, // (31 - 15)/360.
        { "2024-01-31", "2024-02-01", 0.002777777778m }, // (30 + 1 - 30)/360.
        { "2024-01-15", "2024-02-01", 0.044444444444m }, // (30 + 1 - 15)/360.
        { "2024-01-31", "2024-03-31", 0.166666666667m }, // (60 + 30 - 30)/360.
        { "2024-01-30", "2024-03-31", 0.166666666667m }, // (60 + 30 - 30)/360.
        { "2024-01-29", "2024-03-31", 0.172222222222m }, // (60 + 31 - 29)/360.
        { "2024-01-31", "2024-02-29", 0.080555555556m }, // (30 + 29 - 30)/360.
        { "2023-01-31", "2023-02-28", 0.077777777778m }, // (30 + 28 - 30)/360.
        { "2023-02-28", "2023-03-31", 0.091666666667m }, // (30 + 31 - 28)/360.
        { "2024-02-29", "2024-03-31", 0.088888888889m }, // (30 + 31 - 29)/360.
        { "2024-02-28", "2024-02-29", 0.002777777778m }, // (29 - 28)/360.
        { "2024-02-29", "2024-03-01", 0.005555555556m }, // (30 + 1 - 29)/360.
        { "2023-02-28", "2023-03-01", 0.008333333333m }, // (30 + 1 - 28)/360.
        { "2023-12-31", "2024-01-01", 0.002777777778m }, // (360 - 330 + 1 - 30)/360.
        { "2023-12-31", "2024-01-31", 0.083333333333m }, // (360 - 330 + 30 - 30)/360.
        { "2023-12-15", "2024-01-31", 0.127777777778m }, // (360 - 330 + 31 - 15)/360.
        { "2019-08-31", "2024-02-29", 4.497222222222m }, // (5 × 360 - 180 + 29 - 30)/360.
        { "2022-10-31", "2025-03-31", 2.416666666667m }, // (3 × 360 - 210 + 30 - 30)/360.
        { "2023-02-28", "2024-02-29", 1.002777777778m }, // (360 + 29 - 28)/360.
        { "2024-01-31", "2025-01-31", 1.000000000000m }, // (360 + 30 - 30)/360.
    };

    [Theory]
    [MemberData(nameof(ReferenceVectors))]
    public void Fraction_IsdaReferencePeriod_MatchesExpected(string start, string end, decimal expected)
    {
        var actual = Convention.Fraction(LocalDatePattern.Iso.Parse(start).Value, LocalDatePattern.Iso.Parse(end).Value);

        Assert.Equal(expected, Math.Round(actual, 12, MidpointRounding.ToEven));
    }

    [Fact]
    public void Fraction_SplitAtJanuary31_CountsOneMoreDayThanWholePeriod()
    {
        // ISDA §4.16(f): 15 Jan to 31 Jan counts 16 days because D1 is 15.
        // Starting the next leg on 31 Jan adjusts its D1 to 30 and adds one more day.
        var start = new LocalDate(2024, 1, 15);
        var split = new LocalDate(2024, 1, 31);
        var end = new LocalDate(2024, 2, 1);

        var parts = Convention.Fraction(start, split) + Convention.Fraction(split, end);
        var whole = Convention.Fraction(start, end);

        Assert.Equal(Math.Round(17m / 360m, 12, MidpointRounding.ToEven), Math.Round(parts, 12, MidpointRounding.ToEven));
        Assert.Equal(16m / 360m, whole);
    }
}
