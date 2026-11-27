using System.Text.Json;
using System.Text.Json.Nodes;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain.Events;
using Credere.Modules.Origination.Infrastructure.Serialization;
using Credere.Shared.IntegrationEvents;
using FluentAssertions;
using NodaTime;
using Xunit;
using static Credere.Modules.Origination.Tests.Domain.ApplicationSamples;

namespace Credere.Modules.Origination.Tests.Infrastructure;

public sealed class EventSerializerTests
{
    private static readonly Guid EventId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public void Serialize_DomainEvent_UsesTheShortNameAndTheDeclaredVersion()
    {
        var @event = Submitted().UncommittedEvents[0];

        var (type, version, _) = LoanApplicationEventSerializer.Serialize(@event);

        type.Should().Be(nameof(Credere.Modules.Origination.Domain.Events.ApplicationSubmitted));
        version.Should().Be(1);
    }

    [Fact]
    public void Deserialize_DomainEventOfUnknownType_Throws()
    {
        var act = () => LoanApplicationEventSerializer.Deserialize("ApplicationTeleported", 1, "{}");

        act.Should().Throw<InvalidOperationException>().WithMessage("*unknown type*");
    }

    [Fact]
    public void Deserialize_DomainEventAtAnotherVersion_ThrowsBecauseNoUpcasterExists()
    {
        var (type, _, json) = LoanApplicationEventSerializer.Serialize(Submitted().UncommittedEvents[0]);

        var act = () => LoanApplicationEventSerializer.Deserialize(type, 2, json);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no upcaster*");
    }

    [Fact]
    public void Serialize_IntegrationEvent_UsesTheShortNameAndTheDeclaredVersion()
    {
        var (type, version, _) = IntegrationEventSerializer.Serialize(Accepted());

        type.Should().Be(nameof(IntegrationEvents.ApplicationAccepted));
        version.Should().Be(1);
    }

    [Fact]
    public void RoundTrip_EveryPublishedIntegrationEvent_ComesBackEqual()
    {
        IIntegrationEvent[] published =
        [
            new IntegrationEvents.ApplicationSubmitted(
                EventId, SampleApplicationId, Applicant(), Product(), RequestedTerms(), SubmittedAt, CorrelationId),
            new IntegrationEvents.ApplicationDecided(
                EventId, SampleApplicationId, DecisionOutcome.Approve, "Meets the rules.", FinalTerms(), 1, Later, CorrelationId),
            new IntegrationEvents.ApplicationDecided(
                EventId, SampleApplicationId, DecisionOutcome.Decline, "Too much debt.", null, null, Later, CorrelationId),
            Accepted(),
            new IntegrationEvents.ApplicationWithdrawn(EventId, SampleApplicationId, "Changed their mind.", Later, CorrelationId),
        ];

        foreach (var original in published)
        {
            var (type, version, json) = IntegrationEventSerializer.Serialize(original);

            var restored = IntegrationEventSerializer.Deserialize(type, version, json);

            restored.Should().Be(original, "{0} must survive its outbox row", type);
        }
    }

    [Fact]
    public void Serialize_IntegrationEventOfAnotherModule_Throws()
    {
        var act = () => IntegrationEventSerializer.Serialize(new ForeignEvent(EventId));

        act.Should().Throw<InvalidOperationException>().WithMessage("*not an integration event of the Origination module*");
    }

    [Fact]
    public void Deserialize_IntegrationEventAtAnotherVersion_ThrowsBecauseNoUpcasterExists()
    {
        var (type, _, json) = IntegrationEventSerializer.Serialize(Accepted());

        var act = () => IntegrationEventSerializer.Deserialize(type, 3, json);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no upcaster*");
    }

    [Fact]
    public void Deserialize_EnumWrittenAsANumber_Throws()
    {
        var (type, version, json) = IntegrationEventSerializer.Serialize(
            new IntegrationEvents.ApplicationDecided(
                EventId, SampleApplicationId, DecisionOutcome.Decline, "Too much debt.", null, null, Later, CorrelationId));
        var payload = JsonNode.Parse(json)!.AsObject();
        payload["Outcome"] = 1;

        var act = () => IntegrationEventSerializer.Deserialize(type, version, payload.ToJsonString());

        act.Should().Throw<JsonException>("a reordered enum must not change what a stored row means");
    }

    [Fact]
    public void Serialize_Enum_IsWrittenAsItsName()
    {
        var (_, _, json) = IntegrationEventSerializer.Serialize(
            new IntegrationEvents.ApplicationDecided(
                EventId, SampleApplicationId, DecisionOutcome.HumanReview, "Needs a person.", null, null, Later, CorrelationId));

        json.Should().Contain("\"Outcome\":\"HumanReview\"");
    }

    private static IntegrationEvents.ApplicationAccepted Accepted() =>
        new(EventId, SampleApplicationId, Product(), 1, FinalTerms(), Later, CorrelationId);

    private sealed record ForeignEvent(Guid EventId) : IIntegrationEvent;
}
