using NodaTime;

namespace Credere.Core.DayCounts;

/// <summary>
/// Converts a date interval to a year fraction under a named day-count convention.
/// </summary>
/// <remarks>See ISDA 2006 Definitions §4.16 for market day-count conventions.</remarks>
public abstract record DayCountConvention
{
    /// <summary>Gets the market name of the convention.</summary>
    public abstract string Name { get; }

    /// <summary>Returns the year fraction from <paramref name="start" /> inclusive to <paramref name="end" /> exclusive.</summary>
    /// <param name="start">The first date in the interval.</param>
    /// <param name="end">The date immediately after the interval.</param>
    /// <returns>The non-negative year fraction, or zero when the dates coincide.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="end" /> precedes <paramref name="start" />.</exception>
    public abstract decimal Fraction(LocalDate start, LocalDate end);

    internal static int CountActualDays(LocalDate start, LocalDate end)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);

        // DateInterval includes both endpoints. Remove the exclusive end date from the count.
        return new DateInterval(start, end).Length - 1;
    }
}
