using Credere.Modules.Origination.Contracts;

namespace Credere.Modules.Origination.Application.Commands.DecideApplication;

/// <summary>Describes the recorded underwriting decision and resulting application state.</summary>
/// <param name="Id">The application identifier.</param>
/// <param name="Outcome">The underwriting outcome.</param>
/// <param name="Rationale">The reason for the underwriting outcome.</param>
/// <param name="FinalTerms">The approved terms, when the outcome is approval.</param>
/// <param name="TermsVersion">The version of the approved terms.</param>
/// <param name="State">The current or resulting state.</param>
public sealed record DecisionView(
    LoanApplicationId Id,
    DecisionOutcome Outcome,
    string Rationale,
    DecidedTerms? FinalTerms,
    int TermsVersion,
    LoanApplicationState State);
