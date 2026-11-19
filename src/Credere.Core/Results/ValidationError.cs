namespace Credere.Core.Results;

/// <summary>An input failed validation (ADR-0006). Code <c>validation</c>.</summary>
/// <param name="Field">The name of the offending field or parameter.</param>
/// <param name="Reason">Why the value was rejected.</param>
public sealed record ValidationError(string Field, string Reason)
    : DomainError("validation", $"{Field}: {Reason}");
