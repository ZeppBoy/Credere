using Credere.Core.Aprc;
using Credere.Core.DayCounts;
using Credere.Core.Tests.DayCounts;
using NodaTime;
using NodaTime.Text;
using Xunit;

namespace Credere.Core.Tests.Aprc;

/// <summary>
/// Tests Directive (EU) 2023/2225, Annex III, Part I, remark (c), and Credere's D4 month-end rule.
/// </summary>
public sealed class EuCcdAnnexIIITests : DayCountConventionPropertiesBase
{
    /// <inheritdoc />
    protected override DayCountConvention Convention { get; } = new EuCcdAnnexIII();

    /// <summary>Gets the cases from docs/domain/aprc.md, with the final case updated for the D4 decision.</summary>
    // Directive (EU) 2023/2225, Annex III, Part I, remark (c)(i)–(iii).
    // https://eur-lex.europa.eu/eli/dir/2023/2225/oj/eng#anx_III
    // The references express months as twelfths and compare fractions at 12 decimal places, half-even.
    public static TheoryData<string, string, decimal> ProjectVectors => new()
    {
        { "2026-01-15", "2027-07-15", 1.500000000000m }, // 18/12.
        { "2026-01-15", "2026-03-01", 0.129908675799m }, // 1/12 + 17/365, anchor 1 Feb 2026.
        { "2027-12-20", "2028-03-10", 0.224200913242m }, // 2/12 + 21/365, anchor 10 Jan 2028.
        { "2028-02-20", "2028-04-10", 0.135245901639m }, // 1/12 + 19/366, anchor 10 Mar 2028.
        { "2026-01-31", "2026-03-31", 0.166666666667m }, // 2/12, both dates at month-end.
        { "2026-01-31", "2026-02-28", 0.083333333333m }, // 1/12 under the owner-approved D4 rule.
    };

    /// <summary>Gets additional cases derived from remark (c), using the D4 rule for pairs of month-end dates.</summary>
    // These cases reuse dates from the project examples and the day-count and LoanTerm test data.
    public static TheoryData<string, string, decimal> FormulaDerivedVectors => new()
    {
        { "2026-01-15", "2026-01-15", 0.000000000000m }, // No elapsed time.
        { "2023-02-28", "2023-03-01", 0.002739726027m }, // 1/365, no whole month.
        { "2024-01-30", "2024-02-29", 0.081967213115m }, // 30/366, only payment is at month-end.
        { "2026-02-28", "2026-03-31", 0.083333333333m }, // 1/12, common-year February to March.
        { "2024-01-31", "2024-03-01", 0.086073059361m }, // 1/12 + 1/365, anchor 1 Feb 2024.
        { "2024-01-31", "2024-02-29", 0.083333333333m }, // 1/12, leap-year February payment.
        { "2024-01-31", "2024-04-30", 0.250000000000m }, // 3/12, quarterly payment at month-end.
        { "2024-02-29", "2025-02-28", 1.000000000000m }, // 12/12, leap-year February to common-year February.
        { "2024-02-28", "2024-03-31", 0.086065573770m }, // 1/12 + 1/366, anchor 29 Feb 2024.
        { "2024-02-28", "2024-02-29", 0.002732240437m }, // 1/366, includes the leap day.
        { "2024-02-28", "2024-03-01", 0.005464480874m }, // 2/366, leap day and following day.
        { "2023-02-28", "2024-02-29", 1.000000000000m }, // 12/12, common-year February to leap-year February.
        { "2023-12-31", "2024-01-01", 0.002739726027m }, // 1/365, look-back year ends before the leap day.
        { "2024-12-31", "2025-01-01", 0.002732240437m }, // 1/366, look-back year contains the leap day.
        { "1999-07-01", "2000-07-01", 1.000000000000m }, // 12/12, same calendar day, with a leap day between.
        { "1999-02-01", "1999-07-01", 0.416666666667m }, // 5/12, first day to first day.
        { "2023-12-15", "2025-01-15", 1.083333333333m }, // 13/12, spans two year boundaries.
    };

    /// <summary>Gets worked examples of backward counting from earlier European Commission guidance.</summary>
    // SWD(2012) 128 final, §4.1.1, pp. 26–27, examples under remark (c) for Directive 2008/48/EC.
    // https://mf.gov.cz/assets/attachments/EU-MFCR_Metodika_2012_128-Guidelines-consumer-credit-directive-swd-en.pdf#page=26
    public static TheoryData<string, string, decimal> CommissionGuidanceVectors => new()
    {
        { "2013-02-25", "2013-03-28", 0.091530054645m }, // p. 26, 1/12 + 3/366, anchor 28 Feb 2013.
        { "2013-02-26", "2013-03-29", 0.088797814208m }, // p. 26, 1/12 + 2/366, clamp to 28 Feb 2013.
        { "2012-02-26", "2012-03-29", 0.091530054645m }, // p. 26, 1/12 + 3/366, anchor 29 Feb 2012.
        { "2012-12-01", "2013-02-02", 0.169398907104m }, // p. 27, 2/12 + 1/366, anchor 2 Dec 2012.
    };

    [Theory]
    [MemberData(nameof(ProjectVectors))]
    [MemberData(nameof(FormulaDerivedVectors))]
    [MemberData(nameof(CommissionGuidanceVectors))]
    public void Fraction_ReferencePeriod_MatchesExpected(string start, string end, decimal expected)
    {
        var startDate = LocalDatePattern.Iso.Parse(start).Value;
        var endDate = LocalDatePattern.Iso.Parse(end).Value;

        var fraction = Convention.Fraction(startDate, endDate);

        Assert.Equal(expected, Math.Round(fraction, 12, MidpointRounding.ToEven));
    }

    [Fact]
    public void Name_AnnexIIIConvention_IdentifiesItsTimeBasis() =>
        Assert.Equal("EU CCD Annex III", Convention.Name);

    [Fact]
    public void Fraction_LookBackPrecedesCalendarRange_MatchesNextGregorianCycle()
    {
        var start = new LocalDate(CalendarSystem.Iso.MinYear, 1, 1);
        var end = start.PlusDays(1);

        Assert.Equal(Convention.Fraction(start.PlusYears(400), end.PlusYears(400)), Convention.Fraction(start, end));
    }

    [Fact]
    public void Fraction_DatesInDifferentCalendars_UsesGregorianMonthEnds()
    {
        var start = new LocalDate(2026, 1, 31).WithCalendar(CalendarSystem.Julian);
        var end = new LocalDate(2026, 2, 28);

        Assert.Equal(1m / 12m, Convention.Fraction(start, end));
    }
}
