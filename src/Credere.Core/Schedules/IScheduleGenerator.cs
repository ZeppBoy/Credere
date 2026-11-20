namespace Credere.Core.Schedules;

/// <summary>
/// Generates an <see cref="AmortizationSchedule"/> from a validated <see cref="ScheduleRequest"/>
/// (docs/domain/schedules.md, "Public contract").
/// </summary>
/// <remarks>
/// Every generator computes each row's interest through <c>IInterestCalculator</c>, over the periods of
/// <see cref="SchedulePeriods"/>, so the schedule's interest is the number Servicing accrues for that period.
/// </remarks>
public interface IScheduleGenerator
{
    /// <summary>Generates the schedule for a request.</summary>
    /// <param name="request">The validated request.</param>
    /// <returns>The schedule: one row per payment, the last row closing the loan at exactly zero.</returns>
    AmortizationSchedule Generate(ScheduleRequest request);
}
