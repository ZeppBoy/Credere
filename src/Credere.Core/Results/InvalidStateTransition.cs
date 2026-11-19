namespace Credere.Core.Results;

/// <summary>
/// A command is not allowed in the aggregate's current state (ADR-0006). Code <c>invalid-state-transition</c>.
/// </summary>
/// <remarks>The state is a <see cref="string"/> because public APIs take no <c>object</c> (ADR-0009).</remarks>
/// <param name="FromState">The name of the current state, for example <c>Active</c>.</param>
/// <param name="Command">The name of the rejected command.</param>
/// <param name="Reason">Why the command is not allowed from that state.</param>
public sealed record InvalidStateTransition(string FromState, string Command, string Reason)
    : DomainError("invalid-state-transition", $"{Command} not allowed from {FromState}: {Reason}");
