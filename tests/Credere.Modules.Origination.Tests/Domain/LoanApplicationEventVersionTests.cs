using System.Reflection;
using Credere.Modules.Origination.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Credere.Modules.Origination.Tests.Domain;

/// <summary>ADR-0004, "Event schema evolution": every event record declares its schema version.</summary>
public sealed class LoanApplicationEventVersionTests
{
    public static TheoryData<Type> EveryEventType()
    {
        var data = new TheoryData<Type>();
        foreach (var type in DomainEventTypes())
        {
            data.Add(type);
        }

        return data;
    }

    [Fact]
    public void DomainEventTypes_AreTheSixEventsOfTheModuleDoc()
    {
        DomainEventTypes().Select(type => type.Name).Should().BeEquivalentTo(
            "ApplicationSubmitted",
            "ApplicationApproved",
            "ApplicationDeclined",
            "ApplicationSentToReview",
            "ApplicationAccepted",
            "ApplicationWithdrawn");
    }

    [Theory]
    [MemberData(nameof(EveryEventType))]
    public void EventRecord_DeclaresASchemaVersionOfAtLeastOne(Type eventType)
    {
        var attribute = eventType.GetCustomAttribute<EventVersionAttribute>(inherit: false);

        attribute.Should().NotBeNull($"ADR-0004 requires [EventVersion(n)] on {eventType.Name}");
        attribute!.Version.Should().BeGreaterThanOrEqualTo(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EventVersionAttribute_RejectsAVersionBelowOne(int version)
    {
        var act = () => new EventVersionAttribute(version);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static IEnumerable<Type> DomainEventTypes() =>
        typeof(LoanApplicationEvent).Assembly
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(LoanApplicationEvent)))
            .OrderBy(type => type.Name, StringComparer.Ordinal);
}
