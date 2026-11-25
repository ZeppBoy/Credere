using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using Credere.Shared.IntegrationEvents;
using NodaTime;

namespace Credere.Modules.Servicing.IntegrationEvents;

/// <summary>Published when a borrower payment or payoff is recorded.</summary>
/// <param name="EventId">The event identifier and outbox row key.</param>
/// <param name="LoanId">The loan identifier.</param>
/// <param name="PaymentId">The payment identifier.</param>
/// <param name="EffectiveDate">The date on which this operation takes effect.</param>
/// <param name="Amount">The amount received from the borrower.</param>
/// <param name="Allocation">The payment allocation and remaining balances.</param>
/// <param name="State">The current or resulting state.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
[Version(1)]
public sealed record PaymentRecorded(
    Guid EventId,
    LoanId LoanId,
    PaymentId PaymentId,
    LocalDate EffectiveDate,
    Money Amount,
    PaymentAllocation Allocation,
    LoanState State,
    Guid CorrelationId) : IIntegrationEvent;
