using Credere.Modules.Origination.Contracts;
using NodaTime;

namespace Credere.Modules.Origination.Domain.Events;

/// <summary>Underwriting approved the application on final terms, which the borrower may now accept.</summary>
/// <param name="ApplicationId">The application the event belongs to.</param>
/// <param name="OccurredAt">When the decision was recorded.</param>
/// <param name="CommandId">The idempotency id of the command that produced the event.</param>
/// <param name="CorrelationId">The id that correlates the event with related work.</param>
/// <param name="Rationale">The short reason for the decision.</param>
/// <param name="FinalTerms">The approved terms.</param>
/// <param name="TermsVersion">The version number of <paramref name="FinalTerms"/>, at least 1.</param>
[EventVersion(1)]
internal sealed record ApplicationApproved(
    LoanApplicationId ApplicationId,
    Instant OccurredAt,
    Guid CommandId,
    Guid CorrelationId,
    string Rationale,
    DecidedTerms FinalTerms,
    int TermsVersion) : LoanApplicationEvent(ApplicationId, OccurredAt, CommandId, CorrelationId);
