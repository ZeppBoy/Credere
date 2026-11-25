using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using Credere.Shared.IntegrationEvents;
using NodaTime;

namespace Credere.Modules.Servicing.IntegrationEvents;

/// <summary>Published when a payment leaves an excess to return.</summary>
/// <param name="EventId">The event identifier and outbox row key.</param>
/// <param name="LoanId">The loan identifier.</param>
/// <param name="PaymentId">The payment identifier.</param>
/// <param name="EffectiveDate">The date on which this operation takes effect.</param>
/// <param name="Excess">The part of the payment recorded for return.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
[Version(1)]
public sealed record PaymentExcessRecorded(
    Guid EventId,
    LoanId LoanId,
    PaymentId PaymentId,
    LocalDate EffectiveDate,
    Money Excess,
    Guid CorrelationId) : IIntegrationEvent;
