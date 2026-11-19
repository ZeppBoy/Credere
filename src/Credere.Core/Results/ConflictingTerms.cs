namespace Credere.Core.Results;

/// <summary>A set of loan terms contradicts itself (ADR-0006). Code <c>conflicting-terms</c>.</summary>
/// <param name="Reason">Which terms conflict and why. It is also the message.</param>
public sealed record ConflictingTerms(string Reason)
    : DomainError("conflicting-terms", Reason);
