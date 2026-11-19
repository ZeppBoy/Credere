namespace Credere.Shared.Commands;

/// <summary>
/// Marks a record as a command that changes state, and gives it the id used to make it idempotent (ADR-0007).
/// </summary>
/// <remarks>
/// A handler stores a receipt keyed on <see cref="CommandId"/> in the same transaction as the state change and the
/// outbox write. Resubmitting a command with the same id returns the stored outcome instead of running it again.
/// The caller generates the id before sending.
/// </remarks>
public interface ICommand
{
    /// <summary>Gets the id that identifies this submission of the command.</summary>
    Guid CommandId { get; }
}
