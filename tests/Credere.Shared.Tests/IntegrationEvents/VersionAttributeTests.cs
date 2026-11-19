using System.Reflection;
using Credere.Shared.IntegrationEvents;
using Xunit;

namespace Credere.Shared.Tests.IntegrationEvents;

/// <summary>Tests <see cref="VersionAttribute"/>.</summary>
public sealed class VersionAttributeTests
{
    [Version(3)]
    private sealed record VersionedEvent(Guid EventId) : IIntegrationEvent;

    private sealed record UnversionedEvent(Guid EventId) : IIntegrationEvent;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void Constructor_acceptsAnyVersionFromOne(int version)
    {
        Assert.Equal(version, new VersionAttribute(version).Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_rejectsVersionBelowOne(int version)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VersionAttribute(version));
    }

    [Fact]
    public void Attribute_onAnEventType_canBeReadBackByReflection()
    {
        var attribute = typeof(VersionedEvent).GetCustomAttribute<VersionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(3, attribute.Version);
    }

    [Fact]
    public void Attribute_isAbsentWhenNotDeclared()
    {
        Assert.Null(typeof(UnversionedEvent).GetCustomAttribute<VersionAttribute>());
    }

    [Fact]
    public void AttributeUsage_allowsClassesAndStructsOnly_once_andIsNotInherited()
    {
        var usage = typeof(VersionAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }
}
