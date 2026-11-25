using Credere.Modules.Origination.Contracts;
using NodaTime;

namespace Credere.Modules.Origination.Application.Queries.GetApplication;

/// <summary>Describes the current application and its underwriting decision.</summary>
/// <param name="Id">The application identifier.</param>
/// <param name="Applicant">The applicant snapshot supplied for underwriting.</param>
/// <param name="Product">The product key.</param>
/// <param name="RequestedTerms">The terms requested by the applicant.</param>
/// <param name="DecidedTerms">The terms issued on approval, if available.</param>
/// <param name="TermsVersion">The current terms version, or zero before approval.</param>
/// <param name="State">The current or resulting state.</param>
/// <param name="LastOutcome">The most recent underwriting outcome, if available.</param>
/// <param name="LastRationale">The rationale of the most recent underwriting decision, if available.</param>
/// <param name="SubmittedAt">The submission timestamp.</param>
public sealed record ApplicationView(
    LoanApplicationId Id,
    ApplicantSnapshot Applicant,
    ProductCode Product,
    RequestedTerms RequestedTerms,
    DecidedTerms? DecidedTerms,
    int TermsVersion,
    LoanApplicationState State,
    DecisionOutcome? LastOutcome,
    string? LastRationale,
    Instant SubmittedAt);
