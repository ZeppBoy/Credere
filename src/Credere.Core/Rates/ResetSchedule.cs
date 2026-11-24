using Credere.Core.ValueObjects;

namespace Credere.Core.Rates;

/// <summary>Supplies the base rate for the current fixed-reset floating-rate schedule.</summary>
public sealed record ResetSchedule
{
    private ResetSchedule(Percentage baseRate) => BaseRate = baseRate;

    /// <summary>Gets the base rate that applies throughout the calculation period.</summary>
    public Percentage BaseRate { get; }

    /// <summary>Creates a schedule with one base rate for the entire period.</summary>
    /// <param name="baseRate">The base rate in percent units.</param>
    /// <returns>The fixed reset schedule.</returns>
    /// <exception cref="ArgumentNullException">The base rate is null.</exception>
    public static ResetSchedule Fixed(Percentage baseRate)
    {
        ArgumentNullException.ThrowIfNull(baseRate);

        return new ResetSchedule(baseRate);
    }
}
