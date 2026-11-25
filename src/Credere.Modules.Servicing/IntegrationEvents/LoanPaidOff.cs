using Credere.Modules.Servicing.Contracts;
using Credere.Shared.IntegrationEvents;
using NodaTime;

namespace Credere.Modules.Servicing.IntegrationEvents;

/// <summary>Published when a payment closes all outstanding balances.</summary>
/// <param name="EventId">The event identifier and outbox row key.</param>
/// <param name="LoanId">The loan identifier.</param>
/// <param name="FinalPaymentId">The payment that closed the loan.</param>
/// <param name="PaidOffOn">The effective date of the final payment.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
[Version(1)]
public sealed record LoanPaidOff(
    Guid EventId,
    LoanId LoanId,
    PaymentId FinalPaymentId,
    LocalDate PaidOffOn,
    Guid CorrelationId) : IIntegrationEvent;
