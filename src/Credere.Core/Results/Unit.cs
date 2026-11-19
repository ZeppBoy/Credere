namespace Credere.Core.Results;

/// <summary>
/// The value of a <see cref="Result{T, E}"/> that succeeds without producing data, as in
/// <c>Result&lt;Unit, DomainError&gt;</c> (ADR-0006).
/// </summary>
public readonly record struct Unit
{
    /// <summary>Gets the only value of <see cref="Unit"/>.</summary>
    public static Unit Value => default;

    /// <inheritdoc />
    public override string ToString() => "()";
}
