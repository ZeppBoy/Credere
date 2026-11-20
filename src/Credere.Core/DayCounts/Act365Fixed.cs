using NodaTime;

namespace Credere.Core.DayCounts;

/// <summary>
/// Calculates a year fraction using Actual/365 Fixed as defined in ISDA 2006 Definitions §4.16(d).
/// </summary>
public sealed record Act365Fixed : DayCountConvention
{
    /// <inheritdoc />
    public override string Name => "ACT/365 Fixed";

    /// <inheritdoc />
    public override decimal Fraction(LocalDate start, LocalDate end) => CountActualDays(start, end) / 365m;

    /// <inheritdoc />
    internal override ExactYearFraction ExactFraction(LocalDate start, LocalDate end) =>
        new(CountActualDays(start, end), 365);
}
