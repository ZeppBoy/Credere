namespace Credere.Modules.Servicing.Contracts;

/// <summary>Identifies a disbursement intent in Servicing.</summary>
/// <param name="Value">The underlying identifier.</param>
public readonly record struct DisbursementIntentId(Guid Value);
