using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using Credere.Modules.Origination.Domain.Events;

namespace Credere.Modules.Origination.Infrastructure.Serialization;

/// <summary>
/// Turns a domain event into a history row's type name, schema version and JSON, and back (ADR-0004).
/// </summary>
/// <remarks>
/// The type name is the short class name and the version is the event's <c>[EventVersion(n)]</c>. Reading a row whose
/// version is not the current one throws, because no upcaster exists yet. A breaking change to an event adds one in
/// <c>Infrastructure/Upcasters/</c> and teaches <see cref="Deserialize"/> to apply it.
/// </remarks>
internal static class LoanApplicationEventSerializer
{
    private static readonly FrozenDictionary<string, (Type Type, int Version)> Known = typeof(LoanApplicationEvent)
        .Assembly
        .GetTypes()
        .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(LoanApplicationEvent)))
        .ToFrozenDictionary(
            type => type.Name,
            type => (
                type,
                (type.GetCustomAttribute<EventVersionAttribute>()
                    ?? throw new InvalidOperationException($"{type.Name} has no [EventVersion(n)] (ADR-0004).")).Version),
            StringComparer.Ordinal);

    /// <summary>Serializes an event for its history row.</summary>
    /// <param name="event">The domain event.</param>
    /// <returns>The type name, schema version and JSON payload.</returns>
    internal static (string Type, int Version, string Json) Serialize(LoanApplicationEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var type = @event.GetType();
        if (!Known.TryGetValue(type.Name, out var known) || known.Type != type)
        {
            throw new InvalidOperationException($"{type.Name} is not a registered {nameof(LoanApplicationEvent)}.");
        }

        return (type.Name, known.Version, JsonSerializer.Serialize(@event, type, OriginationJson.Options));
    }

    /// <summary>Rebuilds an event from its history row.</summary>
    /// <param name="type">The stored type name.</param>
    /// <param name="version">The stored schema version.</param>
    /// <param name="json">The stored payload.</param>
    /// <returns>The event.</returns>
    /// <exception cref="InvalidOperationException">The type is unknown, or the stored version has no upcaster.</exception>
    internal static LoanApplicationEvent Deserialize(string type, int version, string json)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(json);

        if (!Known.TryGetValue(type, out var known))
        {
            throw new InvalidOperationException($"The history holds an event of unknown type '{type}'.");
        }

        if (version != known.Version)
        {
            throw new InvalidOperationException(
                $"The history holds {type} at version {version}, but the code is at version {known.Version} and has no upcaster.");
        }

        return (LoanApplicationEvent?)JsonSerializer.Deserialize(json, known.Type, OriginationJson.Options)
            ?? throw new InvalidOperationException($"The payload of {type} is null.");
    }
}
