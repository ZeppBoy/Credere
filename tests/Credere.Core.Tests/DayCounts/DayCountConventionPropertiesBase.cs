using Credere.Core.DayCounts;
using FsCheck.Xunit;
using NodaTime;
using Xunit;

namespace Credere.Core.Tests.DayCounts;

/// <summary>Properties shared by the day-count convention tests.</summary>
public abstract class DayCountConventionPropertiesBase
{
    private static readonly LocalDate Origin = new(1900, 1, 1);

    /// <summary>Gets the convention under test.</summary>
    protected abstract DayCountConvention Convention { get; }

    /// <summary>Gets whether extending the interval must increase the fraction.</summary>
    protected virtual bool RequiresStrictIncrease => false;

    [Property]
    public bool Fraction_SameDate_IsZero(ushort startOffset)
    {
        var date = Origin.PlusDays(startOffset);
        return Convention.Fraction(date, date) == 0m;
    }

    [Property]
    public bool Fraction_LaterEnd_IsNonDecreasing(ushort startOffset, ushort firstLength, ushort extraDays)
    {
        var start = Origin.PlusDays(startOffset);
        var firstEnd = start.PlusDays(firstLength);
        var laterEnd = firstEnd.PlusDays(extraDays + 1);
        var first = Convention.Fraction(start, firstEnd);
        var later = Convention.Fraction(start, laterEnd);

        return first >= 0m && (RequiresStrictIncrease ? later > first : later >= first);
    }

    [Property]
    public void Fraction_ReversedDates_Throws(ushort startOffset, ushort dayCount)
    {
        var end = Origin.PlusDays(startOffset);
        var start = end.PlusDays(dayCount + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => Convention.Fraction(start, end));
    }

    /// <summary>Checks whether the fractions for two adjacent intervals sum to the whole interval.</summary>
    protected bool FractionsAreAdditive(ushort startOffset, ushort firstLength, ushort secondLength)
    {
        var start = Origin.PlusDays(startOffset);
        var split = start.PlusDays(firstLength);
        var end = split.PlusDays(secondLength);
        var parts = Convention.Fraction(start, split) + Convention.Fraction(split, end);
        var whole = Convention.Fraction(start, end);

        // Repeating fractions can differ at the limit of decimal precision after division and addition.
        const decimal tolerance = 1e-24m;
        return Math.Abs(parts - whole) <= tolerance;
    }
}
