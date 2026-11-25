using Credere.Core.ValueObjects;

namespace Credere.Modules.Origination.Contracts;

/// <summary>Captures the terms requested by the applicant.</summary>
/// <param name="Principal">The principal amount.</param>
/// <param name="Term">The term in whole months.</param>
public sealed record RequestedTerms(Money Principal, LoanTerm Term);
