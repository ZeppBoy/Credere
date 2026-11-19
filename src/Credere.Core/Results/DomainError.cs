namespace Credere.Core.Results;

/// <summary>
/// The base of every expected business failure that a command, query or aggregate method can return in a
/// <see cref="Result{T, E}"/> (ADR-0006, placed in Core by ADR-0009).
/// </summary>
/// <remarks>
/// <para>
/// This file and its siblings hold the common errors. A module-specific error extends this record in the owning
/// module (ADR-0006) and is public only when the module's facade exposes it (ADR-0008).
/// </para>
/// <para>
/// The properties are get-only and set by constructors, not positional, so a <c>with</c> expression can't change a
/// field and leave the message describing the old value (AGENTS.md §6). Derived errors should do the same.
/// </para>
/// </remarks>
public abstract record DomainError
{
    /// <summary>Initializes a new instance of the <see cref="DomainError"/> class.</summary>
    /// <param name="code">A stable, machine-readable code such as <c>validation</c>, used by the transport mapping.</param>
    /// <param name="message">A human-readable description of the failure.</param>
    protected DomainError(string code, string message)
    {
        Code = code;
        Message = message;
    }

    /// <summary>Gets the stable, machine-readable code, such as <c>validation</c>.</summary>
    public string Code { get; }

    /// <summary>Gets the human-readable description of the failure.</summary>
    public string Message { get; }
}
