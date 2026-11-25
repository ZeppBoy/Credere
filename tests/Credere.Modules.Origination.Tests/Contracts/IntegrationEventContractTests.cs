using System.Reflection;
using Credere.Modules.Origination.IntegrationEvents;
using Credere.Shared.IntegrationEvents;
using Xunit;

namespace Credere.Modules.Origination.Tests.Contracts;

public sealed class IntegrationEventContractTests
{
    [Theory]
    [InlineData(typeof(ApplicationSubmitted))]
    [InlineData(typeof(ApplicationDecided))]
    [InlineData(typeof(ApplicationAccepted))]
    [InlineData(typeof(ApplicationWithdrawn))]
    public void PublishedEvent_Contract_UsesVersionOneAndSharedEventIdentity(Type eventType)
    {
        Assert.True(typeof(IIntegrationEvent).IsAssignableFrom(eventType));
        Assert.Equal(1, eventType.GetCustomAttribute<VersionAttribute>()?.Version);
        Assert.Equal(typeof(Guid), eventType.GetProperty("CorrelationId")?.PropertyType);
    }
}
