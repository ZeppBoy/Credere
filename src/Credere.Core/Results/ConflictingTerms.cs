namespace Credere.Core.Results;

/// <summary>A set of loan terms contradicts itself (ADR-0006). Code <c>conflicting-terms</c>.</summary>
public sealed record ConflictingTerms : DomainError
{
    /// <summary>Initializes a new instance of the <see cref="ConflictingTerms"/> class.</summary>
    /// <param name="reason">Which terms conflict and why. It is also the message.</param>
    public ConflictingTerms(string reason)
        : base("conflicting-terms", reason)
    {
        Reason = reason;
    }

    /// <summary>Gets which terms conflict and why.</summary>
    public string Reason { get; }
}
