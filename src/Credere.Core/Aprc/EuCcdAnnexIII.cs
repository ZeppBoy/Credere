using Credere.Core.DayCounts;
using NodaTime;

namespace Credere.Core.Aprc;

/// <summary>
/// Measures time from the first drawdown under Directive (EU) 2023/2225, Annex III, Part I, remark (c).
/// </summary>
/// <remarks>
/// Credere counts whole months between Gregorian month-ends under decision D4. For other dates, the backward
/// month count and look-back year follow the examples in SWD(2012) 128 final, §4.1.1, pp. 26–27.
/// </remarks>
internal sealed record EuCcdAnnexIII : DayCountConvention
{
    /// <inheritdoc />
    public override string Name => "EU CCD Annex III";

    /// <summary>
    /// Measures the interval in years under remark (c)(iii). The residual day count excludes the drawdown
    /// and includes the anchor date.
    /// </summary>
    /// <param name="start">The date of the first drawdown.</param>
    /// <param name="end">The date of the cash flow.</param>
    /// <returns>The non-negative fraction of a year, or zero for dates that coincide.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The cash flow precedes the drawdown.</exception>
    public override decimal Fraction(LocalDate start, LocalDate end)
    {
        start = start.WithCalendar(CalendarSystem.Iso);
        end = end.WithCalendar(CalendarSystem.Iso);
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);

        var months = (end.Year - start.Year) * 12 + end.Month - start.Month;
        if (IsMonthEnd(start) && IsMonthEnd(end))
        {
            return months / 12m;
        }

        var anchor = end.PlusMonths(-months);
        if (anchor < start)
        {
            // The first candidate lies within the drawdown's month. One fewer month moves it past the drawdown.
            months--;
            anchor = end.PlusMonths(-months);
        }

        var monthFraction = months / 12m;
        if (anchor == start)
        {
            return monthFraction;
        }

        var days = CountActualDays(start, anchor);

        // A complete 400-year Gregorian cycle preserves the look-back length at the calendar's lower limit.
        var lookBackEnd = anchor.Year == CalendarSystem.Iso.MinYear ? anchor.PlusYears(400) : anchor;
        var lookBackDays = CountActualDays(lookBackEnd.PlusYears(-1), lookBackEnd);
        return monthFraction + days / (decimal)lookBackDays;
    }

    private static bool IsMonthEnd(LocalDate date) => date.Day == CalendarSystem.Iso.GetDaysInMonth(date.Year, date.Month);
}
