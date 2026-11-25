using Credere.Modules.Origination.Contracts;
using Credere.Shared.Commands;

namespace Credere.Modules.Origination.Application.Commands.DecideApplication;

/// <summary>Records the outcome of underwriting an application.</summary>
/// <param name="CommandId">The idempotency identifier for the command.</param>
/// <param name="Id">The application identifier.</param>
/// <param name="Outcome">The underwriting outcome.</param>
/// <param name="Rationale">The reason for the underwriting outcome.</param>
/// <param name="FinalTerms">The approved terms, when the outcome is approval.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
public sealed record DecideApplication(
    Guid CommandId,
    LoanApplicationId Id,
    DecisionOutcome Outcome,
    string Rationale,
    DecidedTerms? FinalTerms,
    Guid CorrelationId) : ICommand;
