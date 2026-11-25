using Credere.Modules.Origination.Contracts;
using Credere.Shared.IntegrationEvents;
using NodaTime;

namespace Credere.Modules.Origination.IntegrationEvents;

/// <summary>Published when a loan application is submitted.</summary>
/// <param name="EventId">The event identifier and outbox row key.</param>
/// <param name="ApplicationId">The application identifier.</param>
/// <param name="Applicant">The applicant snapshot supplied for underwriting.</param>
/// <param name="Product">The product key.</param>
/// <param name="Terms">The requested terms.</param>
/// <param name="SubmittedAt">The submission timestamp.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
[Version(1)]
public sealed record ApplicationSubmitted(
    Guid EventId,
    LoanApplicationId ApplicationId,
    ApplicantSnapshot Applicant,
    ProductCode Product,
    RequestedTerms Terms,
    Instant SubmittedAt,
    Guid CorrelationId) : IIntegrationEvent;
