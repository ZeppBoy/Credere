using NodaTime;

namespace Credere.Core.DayCounts;

/// <summary>
/// Calculates a year fraction using 30E/360 Eurobond Basis as defined in ISDA 2006 Definitions §4.16(g).
/// </summary>
public sealed record ThirtyE360 : DayCountConvention
{
    /// <inheritdoc />
    public override string Name => "30E/360";

    /// <inheritdoc />
    public override decimal Fraction(LocalDate start, LocalDate end) => Numerator(start, end) / 360m;

    /// <inheritdoc />
    internal override ExactYearFraction ExactFraction(LocalDate start, LocalDate end) =>
        new(Numerator(start, end), 360);

    private static int Numerator(LocalDate start, LocalDate end)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);

        var startDay = Math.Min(start.Day, 30);
        var endDay = Math.Min(end.Day, 30);

        return 360 * (end.Year - start.Year) + 30 * (end.Month - start.Month) + (endDay - startDay);
    }
}
