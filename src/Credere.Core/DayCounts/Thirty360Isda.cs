using NodaTime;

namespace Credere.Core.DayCounts;

/// <summary>
/// Calculates a year fraction using 30/360 Bond Basis as defined in ISDA 2006 Definitions §4.16(f).
/// </summary>
public sealed record Thirty360Isda : DayCountConvention
{
    /// <inheritdoc />
    public override string Name => "30/360 ISDA";

    /// <inheritdoc />
    public override decimal Fraction(LocalDate start, LocalDate end)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);

        var startDay = Math.Min(start.Day, 30);
        var endDay = startDay == 30 ? Math.Min(end.Day, 30) : end.Day;
        var numerator = 360 * (end.Year - start.Year) + 30 * (end.Month - start.Month) + (endDay - startDay);

        return numerator / 360m;
    }
}
