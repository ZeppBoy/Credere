namespace Credere.Core.Results;

/// <summary>An aggregate was changed by someone else since it was read (ADR-0006). Code <c>concurrency-conflict</c>.</summary>
public sealed record ConcurrencyConflict : DomainError
{
    /// <summary>Initializes a new instance of the <see cref="ConcurrencyConflict"/> class.</summary>
    /// <param name="aggregate">The aggregate type name, for example <c>Loan</c>.</param>
    /// <param name="id">The aggregate id.</param>
    public ConcurrencyConflict(string aggregate, Guid id)
        : base("concurrency-conflict", $"Concurrent update on {aggregate} {id}")
    {
        Aggregate = aggregate;
        Id = id;
    }

    /// <summary>Gets the aggregate type name.</summary>
    public string Aggregate { get; }

    /// <summary>Gets the aggregate id.</summary>
    public Guid Id { get; }
}
