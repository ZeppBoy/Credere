using Credere.Core.ValueObjects;

namespace Credere.Modules.Servicing.Contracts;

/// <summary>Captures the borrower-approved terms received by Servicing.</summary>
/// <param name="Principal">The principal amount.</param>
/// <param name="NominalRate">The annual nominal interest rate.</param>
/// <param name="Term">The term in whole months.</param>
public sealed record AcceptedLoanTerms(Money Principal, Percentage NominalRate, LoanTerm Term);
