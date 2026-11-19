namespace Credere.Core.Results;

/// <summary>The requested aggregate does not exist (ADR-0006). Code <c>not-found</c>.</summary>
public sealed record NotFound : DomainError
{
    /// <summary>Initializes a new instance of the <see cref="NotFound"/> class.</summary>
    /// <param name="aggregate">The aggregate type name, for example <c>Loan</c>.</param>
    /// <param name="id">The id that was looked up.</param>
    public NotFound(string aggregate, Guid id)
        : base("not-found", $"{aggregate} {id} not found")
    {
        Aggregate = aggregate;
        Id = id;
    }

    /// <summary>Gets the aggregate type name.</summary>
    public string Aggregate { get; }

    /// <summary>Gets the id that was looked up.</summary>
    public Guid Id { get; }
}
