using NodaTime;

namespace Credere.Modules.Origination.Infrastructure;

/// <summary>An integration event waiting for dispatch, written in the transaction of its state change (ADR-0005).</summary>
internal sealed class OutboxMessage
{
    /// <summary>The longest <see cref="LastError"/> kept, in characters.</summary>
    internal const int LastErrorMaxLength = 2000;

    /// <summary>Gets or sets the row id, which is the event id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets when the event was written to the outbox.</summary>
    public Instant OccurredAt { get; set; }

    /// <summary>Gets or sets the short name of the event type.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the schema version of the payload, from the event's <c>[Version(n)]</c>.</summary>
    public int EventVersion { get; set; }

    /// <summary>Gets or sets the serialized event.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>Gets or sets when the event was delivered, or <see langword="null"/> while it is pending.</summary>
    public Instant? DispatchedAt { get; set; }

    /// <summary>Gets or sets how many deliveries failed.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Gets or sets the error of the latest failed delivery, truncated.</summary>
    public string? LastError { get; set; }
}
