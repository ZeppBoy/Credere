namespace Credere.Core.Schedules;

/// <summary>
/// How often a schedule has a payment (docs/domain/schedules.md, "Public contract"). The value is the number of
/// periods per year.
/// </summary>
public enum PaymentFrequency
{
    /// <summary>Twelve payments a year. The primary case in M1.</summary>
    Monthly = 12,

    /// <summary>Four payments a year. <c>Term.Months</c> must be a multiple of 3.</summary>
    Quarterly = 4,
}
