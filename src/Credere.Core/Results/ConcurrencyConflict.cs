namespace Credere.Core.Results;

/// <summary>An aggregate was changed by someone else since it was read (ADR-0006). Code <c>concurrency-conflict</c>.</summary>
/// <param name="Aggregate">The aggregate type name, for example <c>Loan</c>.</param>
/// <param name="Id">The aggregate id.</param>
public sealed record ConcurrencyConflict(string Aggregate, Guid Id)
    : DomainError("concurrency-conflict", $"Concurrent update on {Aggregate} {Id}");
