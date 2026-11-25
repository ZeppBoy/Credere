using Credere.Core.ValueObjects;

namespace Credere.Modules.Origination.Contracts;

/// <summary>Captures the terms an underwriter approved for acceptance.</summary>
/// <param name="Principal">The principal amount.</param>
/// <param name="NominalRate">The annual nominal interest rate.</param>
/// <param name="Term">The term in whole months.</param>
public sealed record DecidedTerms(Money Principal, Percentage NominalRate, LoanTerm Term);
