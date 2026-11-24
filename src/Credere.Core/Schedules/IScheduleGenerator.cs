using Credere.Core.Results;

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
    /// <returns>
    /// A schedule with one row per period and a final closing balance of zero, or a domain error
    /// when the request cannot produce a valid schedule.
    /// </returns>
    Result<AmortizationSchedule, DomainError> Generate(ScheduleRequest request);
}
