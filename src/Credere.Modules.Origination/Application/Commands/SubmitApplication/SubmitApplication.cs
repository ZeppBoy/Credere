using Credere.Modules.Origination.Contracts;
using Credere.Shared.Commands;

namespace Credere.Modules.Origination.Application.Commands.SubmitApplication;

/// <summary>Submits a new loan application.</summary>
/// <param name="CommandId">The idempotency identifier for the command.</param>
/// <param name="Applicant">The applicant snapshot supplied for underwriting.</param>
/// <param name="Product">The product key.</param>
/// <param name="Terms">The terms carried with this operation.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
public sealed record SubmitApplication(
    Guid CommandId,
    ApplicantSnapshot Applicant,
    ProductCode Product,
    RequestedTerms Terms,
    Guid CorrelationId) : ICommand;
