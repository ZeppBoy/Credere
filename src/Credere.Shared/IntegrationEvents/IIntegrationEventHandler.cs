namespace Credere.Shared.IntegrationEvents;

/// <summary>
/// Reacts to an integration event published by another module (ADR-0005).
/// </summary>
/// <remarks>
/// Delivery is at least once, so a handler must be idempotent per subscriber and event id: handling the same
/// <see cref="IIntegrationEvent.EventId"/> twice has the effect of handling it once. A handler that throws is
/// retried by the dispatcher; it doesn't affect other handlers of the same event.
/// </remarks>
/// <typeparam name="T">The event type the handler consumes.</typeparam>
// CA1711 reserves the EventHandler suffix for delegates; D2 of the M1 plan and ADR-0005 fix this interface name.
#pragma warning disable CA1711
public interface IIntegrationEventHandler<in T>
    where T : IIntegrationEvent
{
    /// <summary>Handles one delivery of <paramref name="integrationEvent"/>.</summary>
    /// <param name="integrationEvent">The event.</param>
    /// <param name="cancellationToken">A token that cancels the handling.</param>
    /// <returns>A task that completes when the event has been handled.</returns>
    Task HandleAsync(T integrationEvent, CancellationToken cancellationToken);
}
#pragma warning restore CA1711
