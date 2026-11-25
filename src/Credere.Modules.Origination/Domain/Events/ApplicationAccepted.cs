using Credere.Modules.Origination.Contracts;
using NodaTime;

namespace Credere.Modules.Origination.Domain.Events;

/// <summary>The borrower accepted the approved terms. It is the handoff to Servicing and the last event of a stream.</summary>
/// <param name="ApplicationId">The application the event belongs to.</param>
/// <param name="OccurredAt">When the acceptance was recorded.</param>
/// <param name="CommandId">The idempotency id of the command that produced the event.</param>
/// <param name="CorrelationId">The id that correlates the event with related work.</param>
/// <param name="Product">The product the application is for.</param>
/// <param name="TermsVersion">The version of the terms the borrower accepted.</param>
/// <param name="AcceptedTerms">The terms the borrower accepted.</param>
internal sealed record ApplicationAccepted(
    LoanApplicationId ApplicationId,
    Instant OccurredAt,
    Guid CommandId,
    Guid CorrelationId,
    ProductCode Product,
    int TermsVersion,
    DecidedTerms AcceptedTerms) : LoanApplicationEvent(ApplicationId, OccurredAt, CommandId, CorrelationId);
