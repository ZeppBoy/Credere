using Credere.Modules.Origination.Contracts;
using Credere.Shared.IntegrationEvents;
using NodaTime;

namespace Credere.Modules.Origination.IntegrationEvents;

/// <summary>Published when underwriting records an outcome.</summary>
/// <param name="EventId">The event identifier and outbox row key.</param>
/// <param name="ApplicationId">The application identifier.</param>
/// <param name="Outcome">The underwriting outcome.</param>
/// <param name="Rationale">The reason for the underwriting outcome.</param>
/// <param name="FinalTerms">The approved terms, when the outcome is approval.</param>
/// <param name="TermsVersion">The version assigned to the approved terms, or null for other outcomes.</param>
/// <param name="DecidedAt">The decision timestamp.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
[Version(1)]
public sealed record ApplicationDecided(
    Guid EventId,
    LoanApplicationId ApplicationId,
    DecisionOutcome Outcome,
    string Rationale,
    DecidedTerms? FinalTerms,
    int? TermsVersion,
    Instant DecidedAt,
    Guid CorrelationId) : IIntegrationEvent;
