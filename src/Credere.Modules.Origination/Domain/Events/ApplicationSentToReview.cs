using Credere.Modules.Origination.Contracts;
using NodaTime;

namespace Credere.Modules.Origination.Domain.Events;

/// <summary>Underwriting sent the application to a human reviewer.</summary>
/// <param name="ApplicationId">The application the event belongs to.</param>
/// <param name="OccurredAt">When the decision was recorded.</param>
/// <param name="CommandId">The idempotency id of the command that produced the event.</param>
/// <param name="CorrelationId">The id that correlates the event with related work.</param>
/// <param name="Rationale">The short reason the application needs a human review.</param>
[EventVersion(1)]
internal sealed record ApplicationSentToReview(
    LoanApplicationId ApplicationId,
    Instant OccurredAt,
    Guid CommandId,
    Guid CorrelationId,
    string Rationale) : LoanApplicationEvent(ApplicationId, OccurredAt, CommandId, CorrelationId);
