namespace Credere.Shared.IntegrationEvents;

/// <summary>
/// States the schema version of an integration event. Increase it on a breaking change, and add an upcaster for
/// the previous version (AGENTS.md §6).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class VersionAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="VersionAttribute"/> class.</summary>
    /// <param name="version">The schema version, starting at 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is less than 1.</exception>
    public VersionAttribute(int version)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        Version = version;
    }

    /// <summary>Gets the schema version.</summary>
    public int Version { get; }
}
