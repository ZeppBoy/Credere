using NodaTime;

namespace Credere.Modules.Origination.Infrastructure;

/// <summary>
/// The receipt of one handled command (ADR-0007). Every handler has its own table of this shape, so a retried
/// command returns the stored outcome and changes nothing.
/// </summary>
internal sealed class CommandReceipt
{
    /// <summary>Gets or sets the id of the command, which is the key.</summary>
    public Guid CommandId { get; set; }

    /// <summary>Gets or sets when the command was handled.</summary>
    public Instant ReceivedAt { get; set; }

    /// <summary>Gets or sets the serialized outcome the handler returned.</summary>
    public string OutcomeJson { get; set; } = string.Empty;
}
