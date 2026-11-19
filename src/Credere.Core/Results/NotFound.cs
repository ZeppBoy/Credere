namespace Credere.Core.Results;

/// <summary>The requested aggregate does not exist (ADR-0006). Code <c>not-found</c>.</summary>
/// <param name="Aggregate">The aggregate type name, for example <c>Loan</c>.</param>
/// <param name="Id">The id that was looked up.</param>
public sealed record NotFound(string Aggregate, Guid Id)
    : DomainError("not-found", $"{Aggregate} {Id} not found");
