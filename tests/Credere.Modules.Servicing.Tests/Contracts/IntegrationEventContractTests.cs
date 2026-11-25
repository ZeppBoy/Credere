using System.Reflection;
using Credere.Modules.Servicing.IntegrationEvents;
using Credere.Shared.IntegrationEvents;
using Xunit;

namespace Credere.Modules.Servicing.Tests.Contracts;

public sealed class IntegrationEventContractTests
{
    [Theory]
    [InlineData(typeof(LoanDisbursed))]
    [InlineData(typeof(LoanActivated))]
    [InlineData(typeof(PaymentRecorded))]
    [InlineData(typeof(PaymentExcessRecorded))]
    [InlineData(typeof(LoanAged))]
    [InlineData(typeof(LoanPaidOff))]
    [InlineData(typeof(LoanWrittenOff))]
    [InlineData(typeof(LoanRestructured))]
    [InlineData(typeof(LoanRecovered))]
    public void PublishedEvent_Contract_UsesVersionOneAndSharedEventIdentity(Type eventType)
    {
        Assert.True(typeof(IIntegrationEvent).IsAssignableFrom(eventType));
        Assert.Equal(1, eventType.GetCustomAttribute<VersionAttribute>()?.Version);
        Assert.Equal(typeof(Guid), eventType.GetProperty("CorrelationId")?.PropertyType);
    }
}
