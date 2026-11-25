using Credere.Modules.Origination.Contracts;
using Credere.Shared.IntegrationEvents;
using NodaTime;

namespace Credere.Modules.Origination.IntegrationEvents;

/// <summary>Published when the borrower accepts the approved terms.</summary>
/// <param name="EventId">The event identifier and outbox row key.</param>
/// <param name="ApplicationId">The application identifier.</param>
/// <param name="Product">The product key.</param>
/// <param name="TermsVersion">The version of the approved terms.</param>
/// <param name="AcceptedTerms">The terms the borrower accepted.</param>
/// <param name="AcceptedAt">The acceptance timestamp.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
[Version(1)]
public sealed record ApplicationAccepted(
    Guid EventId,
    LoanApplicationId ApplicationId,
    ProductCode Product,
    int TermsVersion,
    DecidedTerms AcceptedTerms,
    Instant AcceptedAt,
    Guid CorrelationId) : IIntegrationEvent;
