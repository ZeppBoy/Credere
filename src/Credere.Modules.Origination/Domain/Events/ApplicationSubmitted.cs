using Credere.Modules.Origination.Contracts;
using NodaTime;

namespace Credere.Modules.Origination.Domain.Events;

/// <summary>An applicant submitted a new application. It always starts a stream.</summary>
/// <param name="ApplicationId">The application the event belongs to.</param>
/// <param name="OccurredAt">When the application was submitted.</param>
/// <param name="CommandId">The idempotency id of the command that produced the event.</param>
/// <param name="CorrelationId">The id that correlates the event with related work.</param>
/// <param name="Applicant">The applicant snapshot supplied for underwriting. It contains personal data.</param>
/// <param name="Product">The product applied for.</param>
/// <param name="Terms">The terms the applicant requested.</param>
internal sealed record ApplicationSubmitted(
    LoanApplicationId ApplicationId,
    Instant OccurredAt,
    Guid CommandId,
    Guid CorrelationId,
    ApplicantSnapshot Applicant,
    ProductCode Product,
    RequestedTerms Terms) : LoanApplicationEvent(ApplicationId, OccurredAt, CommandId, CorrelationId);
