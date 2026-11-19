using NodaTime;

namespace Credere.Core.DayCounts;

/// <summary>
/// Calculates a year fraction using Actual/Actual (ISDA) as defined in ISDA 2006 Definitions §4.16(b).
/// </summary>
/// <remarks>Worked examples appear in ISDA memorandum ISDA-BS:9951.1, §4, pp. 3–6.</remarks>
public sealed record ActActIsda : DayCountConvention
{
    /// <inheritdoc />
    public override string Name => "ACT/ACT (ISDA)";

    /// <inheritdoc />
    public override decimal Fraction(LocalDate start, LocalDate end)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);

        // Year boundaries and leap years are Gregorian, even if the dates use another calendar.
        var segmentStart = start.WithCalendar(CalendarSystem.Iso);
        var endDate = end.WithCalendar(CalendarSystem.Iso);
        var commonYearDays = 0;
        var leapYearDays = 0;

        while (segmentStart < endDate)
        {
            var year = segmentStart.Year;
            var segmentEnd = year == endDate.Year ? endDate : new LocalDate(year + 1, 1, 1);
            var days = CountActualDays(segmentStart, segmentEnd);

            if (CalendarSystem.Iso.IsLeapYear(year))
            {
                leapYearDays += days;
            }
            else
            {
                commonYearDays += days;
            }

            segmentStart = segmentEnd;
        }

        return commonYearDays / 365m + leapYearDays / 366m;
    }
}
