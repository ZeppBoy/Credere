using Credere.Modules.Servicing.Contracts;
using Credere.Shared.IntegrationEvents;
using NodaTime;

namespace Credere.Modules.Servicing.IntegrationEvents;

/// <summary>Published when aging changes a loan's state or days past due.</summary>
/// <param name="EventId">The event identifier and outbox row key.</param>
/// <param name="LoanId">The loan identifier.</param>
/// <param name="BusinessDate">The business date supplied by the caller.</param>
/// <param name="PreviousState">The loan state before aging.</param>
/// <param name="NewState">The loan state after aging.</param>
/// <param name="DaysPastDue">The days past due.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
[Version(1)]
public sealed record LoanAged(
    Guid EventId,
    LoanId LoanId,
    LocalDate BusinessDate,
    LoanState PreviousState,
    LoanState NewState,
    int? DaysPastDue,
    Guid CorrelationId) : IIntegrationEvent;
