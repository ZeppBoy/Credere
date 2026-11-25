using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using Credere.Shared.IntegrationEvents;
using NodaTime;

namespace Credere.Modules.Servicing.IntegrationEvents;

/// <summary>Published when a defaulted loan is written off.</summary>
/// <param name="EventId">The event identifier and outbox row key.</param>
/// <param name="LoanId">The loan identifier.</param>
/// <param name="BusinessDate">The business date supplied by the caller.</param>
/// <param name="Reason">The reason supplied for the operation.</param>
/// <param name="WrittenOffBalance">The balance written off.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
[Version(1)]
public sealed record LoanWrittenOff(
    Guid EventId,
    LoanId LoanId,
    LocalDate BusinessDate,
    string Reason,
    Money WrittenOffBalance,
    Guid CorrelationId) : IIntegrationEvent;
