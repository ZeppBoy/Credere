namespace Credere.Shared.IntegrationEvents;

/// <summary>
/// Marks a public record as an integration event: a fact one module publishes through its transactional outbox for
/// other modules to react to (ADR-0005).
/// </summary>
/// <remarks>
/// Implementations are public records under the publisher's <c>IntegrationEvents/</c> folder, marked with
/// <see cref="VersionAttribute"/>, and carry enough data that subscribers never re-query the publisher.
/// </remarks>
public interface IIntegrationEvent
{
    /// <summary>
    /// Gets the unique id of this event. The outbox row uses it as its key, and each subscriber uses it, together
    /// with its own key, to make delivery idempotent (ADR-0005).
    /// </summary>
    Guid EventId { get; }
}
