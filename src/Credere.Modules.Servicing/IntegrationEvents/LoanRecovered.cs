using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using Credere.Shared.IntegrationEvents;
using NodaTime;

namespace Credere.Modules.Servicing.IntegrationEvents;

/// <summary>Published when a defaulted loan is recovered.</summary>
/// <param name="EventId">The event identifier and outbox row key.</param>
/// <param name="LoanId">The loan identifier.</param>
/// <param name="BusinessDate">The business date supplied by the caller.</param>
/// <param name="Method">The recovery method.</param>
/// <param name="RecoveredAmount">The amount recovered.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
[Version(1)]
public sealed record LoanRecovered(
    Guid EventId,
    LoanId LoanId,
    LocalDate BusinessDate,
    string Method,
    Money RecoveredAmount,
    Guid CorrelationId) : IIntegrationEvent;
