using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using Credere.Shared.IntegrationEvents;
using NodaTime;

namespace Credere.Modules.Servicing.IntegrationEvents;

/// <summary>Published when treasury disbursement is confirmed.</summary>
/// <param name="EventId">The event identifier and outbox row key.</param>
/// <param name="LoanId">The loan identifier.</param>
/// <param name="SourceReference">The originating application's opaque reference.</param>
/// <param name="AcceptedTermsVersion">The version accepted by the borrower.</param>
/// <param name="Product">The product key.</param>
/// <param name="Principal">The principal amount.</param>
/// <param name="NominalRate">The annual nominal interest rate.</param>
/// <param name="DayCountConvention">The name of the day-count convention.</param>
/// <param name="Term">The term in whole months.</param>
/// <param name="Frequency">The scheduled payment frequency.</param>
/// <param name="Repayment">The repayment schedule type.</param>
/// <param name="DisbursementDate">The business date of the disbursement.</param>
/// <param name="FirstPaymentDate">The date of the first scheduled payment.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
[Version(1)]
public sealed record LoanDisbursed(
    Guid EventId,
    LoanId LoanId,
    Guid SourceReference,
    int AcceptedTermsVersion,
    string Product,
    Money Principal,
    Percentage NominalRate,
    string DayCountConvention,
    LoanTerm Term,
    PaymentFrequency Frequency,
    RepaymentType Repayment,
    LocalDate DisbursementDate,
    LocalDate FirstPaymentDate,
    Guid CorrelationId) : IIntegrationEvent;
