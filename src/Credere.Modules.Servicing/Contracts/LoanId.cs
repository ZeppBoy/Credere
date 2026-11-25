namespace Credere.Modules.Servicing.Contracts;

/// <summary>Identifies a loan in Servicing.</summary>
/// <param name="Value">The underlying identifier.</param>
public readonly record struct LoanId(Guid Value);
