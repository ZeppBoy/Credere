using Credere.Shared.IntegrationEvents;
using Xunit;

namespace Credere.Shared.Tests.IntegrationEvents;

/// <summary>Tests the shape of <see cref="IIntegrationEvent"/> and <see cref="IIntegrationEventHandler{T}"/>.</summary>
public sealed class IntegrationEventContractTests
{
    [Version(1)]
    private record SampleHappened(Guid EventId) : IIntegrationEvent;

    [Version(1)]
    private sealed record SpecificSampleHappened(Guid EventId, string Detail) : SampleHappened(EventId);

    private sealed class RecordingHandler : IIntegrationEventHandler<SampleHappened>
    {
        public List<(Guid EventId, CancellationToken Token)> Handled { get; } = [];

        public Task HandleAsync(SampleHappened integrationEvent, CancellationToken cancellationToken)
        {
            Handled.Add((integrationEvent.EventId, cancellationToken));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Event_exposesItsEventId()
    {
        var id = Guid.NewGuid();

        IIntegrationEvent integrationEvent = new SampleHappened(id);

        Assert.Equal(id, integrationEvent.EventId);
    }

    [Fact]
    public async Task Handler_receivesTheEventAndTheCancellationToken()
    {
        using var source = new CancellationTokenSource();
        var handler = new RecordingHandler();
        var id = Guid.NewGuid();

        await handler.HandleAsync(new SampleHappened(id), source.Token);

        Assert.Equal([(id, source.Token)], handler.Handled);
    }

    [Fact]
    public async Task Handler_ofABaseEventType_canBeUsedForADerivedEventType()
    {
        // T is contravariant (in T), so a subscriber of the general event also accepts the specific one.
        var handler = new RecordingHandler();
        IIntegrationEventHandler<SpecificSampleHappened> asSpecific = handler;
        var id = Guid.NewGuid();

        await asSpecific.HandleAsync(new SpecificSampleHappened(id, "x"), CancellationToken.None);

        Assert.Equal(id, Assert.Single(handler.Handled).EventId);
    }

    [Fact]
    public void Handler_typeParameter_isConstrainedToIntegrationEvents()
    {
        var parameter = typeof(IIntegrationEventHandler<>).GetGenericArguments()[0];

        Assert.Contains(typeof(IIntegrationEvent), parameter.GetGenericParameterConstraints());
    }
}
