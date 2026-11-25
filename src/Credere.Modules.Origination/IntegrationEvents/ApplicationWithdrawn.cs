using Credere.Modules.Origination.Contracts;
using Credere.Shared.IntegrationEvents;
using NodaTime;

namespace Credere.Modules.Origination.IntegrationEvents;

/// <summary>Published when an application is withdrawn.</summary>
/// <param name="EventId">The event identifier and outbox row key.</param>
/// <param name="ApplicationId">The application identifier.</param>
/// <param name="Reason">The reason supplied for the operation.</param>
/// <param name="WithdrawnAt">The withdrawal timestamp.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
[Version(1)]
public sealed record ApplicationWithdrawn(
    Guid EventId,
    LoanApplicationId ApplicationId,
    string Reason,
    Instant WithdrawnAt,
    Guid CorrelationId) : IIntegrationEvent;
