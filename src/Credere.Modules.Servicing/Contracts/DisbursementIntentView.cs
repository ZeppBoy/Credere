namespace Credere.Modules.Servicing.Contracts;

/// <summary>Describes the accepted terms and current state of a disbursement intent.</summary>
/// <param name="Id">The disbursement intent identifier.</param>
/// <param name="SourceReference">The originating application's opaque reference.</param>
/// <param name="AcceptedTermsVersion">The version accepted by the borrower.</param>
/// <param name="Product">The product key.</param>
/// <param name="Terms">The terms accepted by the borrower.</param>
/// <param name="State">The current or resulting state.</param>
/// <param name="LoanId">The loan identifier after disbursement, or null while the intent is pending.</param>
public sealed record DisbursementIntentView(
    DisbursementIntentId Id,
    Guid SourceReference,
    int AcceptedTermsVersion,
    string Product,
    AcceptedLoanTerms Terms,
    DisbursementIntentState State,
    LoanId? LoanId);
