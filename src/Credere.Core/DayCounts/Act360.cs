using NodaTime;

namespace Credere.Core.DayCounts;

/// <summary>
/// Calculates a year fraction using Actual/360 as defined in ISDA 2006 Definitions §4.16(e).
/// </summary>
public sealed record Act360 : DayCountConvention
{
    /// <inheritdoc />
    public override string Name => "ACT/360";

    /// <inheritdoc />
    public override decimal Fraction(LocalDate start, LocalDate end) => CountActualDays(start, end) / 360m;

    /// <inheritdoc />
    internal override ExactYearFraction ExactFraction(LocalDate start, LocalDate end) =>
        new(CountActualDays(start, end), 360);
}
