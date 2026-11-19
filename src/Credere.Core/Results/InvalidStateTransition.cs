namespace Credere.Core.Results;

/// <summary>
/// A command is not allowed in the aggregate's current state (ADR-0006). Code <c>invalid-state-transition</c>.
/// </summary>
/// <remarks>The state is a <see cref="string"/> because public APIs take no <c>object</c> (ADR-0009).</remarks>
public sealed record InvalidStateTransition : DomainError
{
    /// <summary>Initializes a new instance of the <see cref="InvalidStateTransition"/> class.</summary>
    /// <param name="fromState">The name of the current state, for example <c>Active</c>.</param>
    /// <param name="command">The name of the rejected command.</param>
    /// <param name="reason">Why the command is not allowed from that state.</param>
    public InvalidStateTransition(string fromState, string command, string reason)
        : base("invalid-state-transition", $"{command} not allowed from {fromState}: {reason}")
    {
        FromState = fromState;
        Command = command;
        Reason = reason;
    }

    /// <summary>Gets the name of the current state.</summary>
    public string FromState { get; }

    /// <summary>Gets the name of the rejected command.</summary>
    public string Command { get; }

    /// <summary>Gets why the command is not allowed from that state.</summary>
    public string Reason { get; }
}
