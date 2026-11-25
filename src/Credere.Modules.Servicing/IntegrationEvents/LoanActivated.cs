using Credere.Modules.Servicing.Contracts;
using Credere.Shared.IntegrationEvents;
using NodaTime;

namespace Credere.Modules.Servicing.IntegrationEvents;

/// <summary>Published when a disbursed loan becomes active.</summary>
/// <param name="EventId">The event identifier and outbox row key.</param>
/// <param name="LoanId">The loan identifier.</param>
/// <param name="EffectiveDate">The date on which this operation takes effect.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
[Version(1)]
public sealed record LoanActivated(
    Guid EventId,
    LoanId LoanId,
    LocalDate EffectiveDate,
    Guid CorrelationId) : IIntegrationEvent;
