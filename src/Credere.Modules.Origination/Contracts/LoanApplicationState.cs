namespace Credere.Modules.Origination.Contracts;

/// <summary>The lifecycle state of a loan application.</summary>
public enum LoanApplicationState
{
    /// <summary>The applicant submitted the application.</summary>
    Submitted,

    /// <summary>The application is awaiting human review.</summary>
    UnderReview,

    /// <summary>The underwriter approved terms for the borrower to accept.</summary>
    Approved,

    /// <summary>The borrower accepted the approved terms.</summary>
    AcceptedByBorrower,

    /// <summary>The application was declined.</summary>
    Declined,

    /// <summary>The application was withdrawn.</summary>
    Withdrawn,
}
