using Credere.Modules.Origination.Contracts;
using NodaTime;

namespace Credere.Modules.Origination.Domain;

/// <summary>The persisted current state needed to restore an application for commands (ADR-0004).</summary>
internal sealed record LoanApplicationSnapshot(
    LoanApplicationId Id,
    ApplicantSnapshot Applicant,
    ProductCode Product,
    RequestedTerms Terms,
    Instant SubmittedAt,
    LoanApplicationState State,
    UnderwritingDecision? Decision,
    DecidedTerms? DecidedTerms,
    int TermsVersion,
    int Version);
