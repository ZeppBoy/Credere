namespace Credere.Core.Results;

/// <summary>
/// The base of every expected business failure that a command, query or aggregate method can return in a
/// <see cref="Result{T, E}"/> (ADR-0006, placed in Core by ADR-0009).
/// </summary>
/// <remarks>
/// This file and its siblings hold the common errors. A module-specific error extends this record in the owning
/// module (ADR-0006) and is public only when the module's facade exposes it (ADR-0008).
/// </remarks>
/// <param name="Code">A stable, machine-readable code such as <c>validation</c>, used by the transport mapping.</param>
/// <param name="Message">A human-readable description of the failure.</param>
public abstract record DomainError(string Code, string Message);
