namespace Credere.Core.Results;

/// <summary>An input failed validation (ADR-0006). Code <c>validation</c>.</summary>
public sealed record ValidationError : DomainError
{
    /// <summary>Initializes a new instance of the <see cref="ValidationError"/> class.</summary>
    /// <param name="field">The name of the offending field or parameter.</param>
    /// <param name="reason">Why the value was rejected.</param>
    public ValidationError(string field, string reason)
        : base("validation", $"{field}: {reason}")
    {
        Field = field;
        Reason = reason;
    }

    /// <summary>Gets the name of the offending field or parameter.</summary>
    public string Field { get; }

    /// <summary>Gets why the value was rejected.</summary>
    public string Reason { get; }
}
