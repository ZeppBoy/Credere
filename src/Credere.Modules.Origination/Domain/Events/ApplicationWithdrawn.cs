using Credere.Modules.Origination.Contracts;
using NodaTime;

namespace Credere.Modules.Origination.Domain.Events;

/// <summary>The application was withdrawn. The application is terminal.</summary>
/// <param name="ApplicationId">The application the event belongs to.</param>
/// <param name="OccurredAt">When the withdrawal was recorded.</param>
/// <param name="CommandId">The idempotency id of the command that produced the event.</param>
/// <param name="CorrelationId">The id that correlates the event with related work.</param>
/// <param name="Reason">The reason supplied for the withdrawal.</param>
[EventVersion(1)]
internal sealed record ApplicationWithdrawn(
    LoanApplicationId ApplicationId,
    Instant OccurredAt,
    Guid CommandId,
    Guid CorrelationId,
    string Reason) : LoanApplicationEvent(ApplicationId, OccurredAt, CommandId, CorrelationId);
