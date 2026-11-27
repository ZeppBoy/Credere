namespace Credere.Modules.Servicing.Domain.Events;

/// <summary>
/// Declares the schema version of a domain event record, as ADR-0004 requires ("Event schema evolution"). The history
/// serializer reads it to pick the upcaster chain for a stored row. A breaking change to a record increments the
/// version and ships with an upcaster from the previous one.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
internal sealed class EventVersionAttribute : Attribute
{
    /// <summary>Creates the attribute.</summary>
    /// <param name="version">The schema version, starting at 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is below 1, which is a programmer error.</exception>
    public EventVersionAttribute(int version)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        Version = version;
    }

    /// <summary>Gets the schema version of the event record.</summary>
    public int Version { get; }
}
