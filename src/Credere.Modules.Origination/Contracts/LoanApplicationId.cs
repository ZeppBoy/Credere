namespace Credere.Modules.Origination.Contracts;

/// <summary>Identifies an application in Origination.</summary>
/// <param name="Value">The underlying identifier.</param>
public readonly record struct LoanApplicationId(Guid Value);
