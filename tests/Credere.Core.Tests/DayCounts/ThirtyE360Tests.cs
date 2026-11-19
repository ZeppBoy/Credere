using Credere.Core.DayCounts;
using FsCheck.Xunit;
using NodaTime.Text;
using Xunit;

namespace Credere.Core.Tests.DayCounts;

/// <summary>Tests the 30E/360 Eurobond convention.</summary>
public sealed class ThirtyE360Tests : DayCountConventionPropertiesBase
{
    /// <inheritdoc />
    protected override DayCountConvention Convention { get; } = new ThirtyE360();

    /// <summary>Gets start dates, exclusive end dates, and expected year fractions.</summary>
    // Formula-derived from ISDA 2006 Definitions §4.16(g) (Eurobond Basis).
    // https://www.isda.org/a/smMDE/Blackline-2000-v-2006-ISDA-Definitions.pdf
    // ISDA's 2022 response to ROC CDE v3, row A011, confirms the independent 31-to-30 adjustments and Y2 - Y1.
    // https://www.isda.org/a/HAxgE/ISDA-Response-to-ROC-CDEv3-Consultation-103122.pdf
    // Each comment shows the numerator divided by 360. Fractions are shown to 12 decimal places.
    public static TheoryData<string, string, decimal> ReferenceVectors => new()
    {
        { "2024-01-31", "2024-01-31", 0.000000000000m }, // (30 - 30)/360.
        { "2024-01-15", "2024-01-16", 0.002777777778m }, // (16 - 15)/360.
        { "2024-01-30", "2024-01-31", 0.000000000000m }, // (30 - 30)/360.
        { "2024-01-29", "2024-01-31", 0.002777777778m }, // (30 - 29)/360.
        { "2024-01-15", "2024-01-31", 0.041666666667m }, // (30 - 15)/360.
        { "2024-01-31", "2024-02-01", 0.002777777778m }, // (30 + 1 - 30)/360.
        { "2024-01-15", "2024-02-01", 0.044444444444m }, // (30 + 1 - 15)/360.
        { "2024-01-31", "2024-03-31", 0.166666666667m }, // (60 + 30 - 30)/360.
        { "2024-01-30", "2024-03-31", 0.166666666667m }, // (60 + 30 - 30)/360.
        { "2024-01-29", "2024-03-31", 0.169444444444m }, // (60 + 30 - 29)/360.
        { "2024-01-31", "2024-02-29", 0.080555555556m }, // (30 + 29 - 30)/360.
        { "2023-01-31", "2023-02-28", 0.077777777778m }, // (30 + 28 - 30)/360.
        { "2023-02-28", "2023-03-31", 0.088888888889m }, // (30 + 30 - 28)/360.
        { "2024-02-29", "2024-03-31", 0.086111111111m }, // (30 + 30 - 29)/360.
        { "2024-02-28", "2024-02-29", 0.002777777778m }, // (29 - 28)/360.
        { "2024-02-29", "2024-03-01", 0.005555555556m }, // (30 + 1 - 29)/360.
        { "2023-02-28", "2023-03-01", 0.008333333333m }, // (30 + 1 - 28)/360.
        { "2023-12-31", "2024-01-01", 0.002777777778m }, // (360 - 330 + 1 - 30)/360.
        { "2023-12-31", "2024-01-31", 0.083333333333m }, // (360 - 330 + 30 - 30)/360.
        { "2023-12-15", "2024-01-31", 0.125000000000m }, // (360 - 330 + 30 - 15)/360.
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

    [Property]
    public bool Fraction_SplitPeriod_IsAdditive(ushort startOffset, ushort firstLength, ushort secondLength) =>
        FractionsAreAdditive(startOffset, firstLength, secondLength);
}
