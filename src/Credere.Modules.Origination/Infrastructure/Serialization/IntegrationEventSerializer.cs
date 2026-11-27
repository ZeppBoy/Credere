using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using Credere.Shared.IntegrationEvents;

namespace Credere.Modules.Origination.Infrastructure.Serialization;

/// <summary>
/// Turns an integration event into an outbox row's type name, schema version and JSON, and back (ADR-0005).
/// </summary>
/// <remarks>
/// The type name is the short class name and the version is the event's <c>[Version(n)]</c>. Only this module's
/// published events are known, so a row of another module's event is rejected.
/// </remarks>
internal static class IntegrationEventSerializer
{
    private static readonly FrozenDictionary<string, (Type Type, int Version)> Known = typeof(IntegrationEventSerializer)
        .Assembly
        .GetTypes()
        .Where(type => type is { IsAbstract: false, IsClass: true } && typeof(IIntegrationEvent).IsAssignableFrom(type))
        .ToFrozenDictionary(
            type => type.Name,
            type => (
                type,
                (type.GetCustomAttribute<VersionAttribute>()
                    ?? throw new InvalidOperationException($"{type.Name} has no [Version(n)] (AGENTS.md section 6).")).Version),
            StringComparer.Ordinal);

    /// <summary>Serializes an event for its outbox row.</summary>
    /// <param name="integrationEvent">The integration event this module publishes.</param>
    /// <returns>The type name, schema version and JSON payload.</returns>
    internal static (string Type, int Version, string Json) Serialize(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var type = integrationEvent.GetType();
        if (!Known.TryGetValue(type.Name, out var known) || known.Type != type)
        {
            throw new InvalidOperationException($"{type.Name} is not an integration event of the Origination module.");
        }

        return (type.Name, known.Version, JsonSerializer.Serialize(integrationEvent, type, OriginationJson.Options));
    }

    /// <summary>Rebuilds an event from its outbox row.</summary>
    /// <param name="type">The stored type name.</param>
    /// <param name="version">The stored schema version.</param>
    /// <param name="json">The stored payload.</param>
    /// <returns>The event.</returns>
    /// <exception cref="InvalidOperationException">The type is unknown, or the stored version has no upcaster.</exception>
    internal static IIntegrationEvent Deserialize(string type, int version, string json)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(json);

        if (!Known.TryGetValue(type, out var known))
        {
            throw new InvalidOperationException($"The outbox holds an event of unknown type '{type}'.");
        }

        if (version != known.Version)
        {
            throw new InvalidOperationException(
                $"The outbox holds {type} at version {version}, but the code is at version {known.Version} and has no upcaster.");
        }

        return (IIntegrationEvent?)JsonSerializer.Deserialize(json, known.Type, OriginationJson.Options)
            ?? throw new InvalidOperationException($"The payload of {type} is null.");
    }
}
