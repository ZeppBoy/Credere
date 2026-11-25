namespace Credere.Modules.Servicing.Contracts;

/// <summary>Identifies a payment in Servicing.</summary>
/// <param name="Value">The underlying identifier.</param>
public readonly record struct PaymentId(Guid Value);
