using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Infrastructure.Serialization;
using Credere.Shared.IntegrationEvents;
using FluentAssertions;
using NodaTime;
using Xunit;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;
using static Credere.Modules.Servicing.Tests.Domain.Loans.LoanSamples;
using Published = Credere.Modules.Servicing.IntegrationEvents;

namespace Credere.Modules.Servicing.Tests.Infrastructure;

public sealed class IntegrationEventSerializerTests
{
    private static readonly Guid EventId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly LocalDate Day = new(2026, 2, 15);

    private static PaymentAllocation Allocation() =>
        new(Euro(0m), Euro(41.67m), Euro(1.39m), Euro(100m), Euro(0.01m), Euro(0m), Euro(0m), Euro(0m), Euro(9900m));

    private static IIntegrationEvent[] Published_EveryEvent() =>
    [
        new Published.LoanDisbursed(
            EventId, Id, SourceReference, 1, "TERM", Principal, Rate.NominalRate, Rate.DayCountConvention.Name,
            LoanTerm.FromMonths(24), Credere.Core.Schedules.PaymentFrequency.Monthly, RepaymentType.InterestOnlyBullet,
            DisbursedOn, FirstDueDate, CorrelationId),
        new Published.LoanActivated(EventId, Id, ActivatedOn, CorrelationId),
        new Published.LoanAged(EventId, Id, Day, LoanState.Active, LoanState.PastDue, 5, CorrelationId),
        new Published.LoanAged(EventId, Id, Day, LoanState.PastDue, LoanState.Active, null, CorrelationId),
        new Published.PaymentRecorded(EventId, Id, SamplePaymentId, Day, Euro(142.07m), Allocation(), LoanState.Active, CorrelationId),
        new Published.PaymentExcessRecorded(EventId, Id, SamplePaymentId, Day, Euro(0.01m), CorrelationId),
        new Published.LoanPaidOff(EventId, Id, SamplePaymentId, Day, CorrelationId),
        new Published.LoanWrittenOff(EventId, Id, Day, "Portion uncollectible", Euro(10043.06m), CorrelationId),
        new Published.LoanRestructured(EventId, Id, Day, "Agreed plan", CorrelationId),
        new Published.LoanRecovered(EventId, Id, Day, "Settlement", Euro(0m), CorrelationId),
    ];

    [Fact]
    public void RoundTrip_EveryPublishedIntegrationEvent_ComesBackEqual()
    {
        foreach (var original in Published_EveryEvent())
        {
            var (type, version, json) = IntegrationEventSerializer.Serialize(original);

            var restored = IntegrationEventSerializer.Deserialize(type, version, json);

            restored.Should().Be(original, "{0} must survive its outbox row", type);
        }
    }

    [Fact]
    public void RoundTrip_TheExamplesCoverEveryPublishedEventType()
    {
        var covered = Published_EveryEvent().Select(item => item.GetType().Name).Distinct();

        covered.Should().BeEquivalentTo(IntegrationEventSerializer.TypeNames);
    }

    [Fact]
    public void Serialize_UsesTheShortNameAndTheDeclaredVersion()
    {
        var (type, version, _) = IntegrationEventSerializer.Serialize(Published_EveryEvent()[0]);

        type.Should().Be("LoanDisbursed");
        version.Should().Be(1);
    }

    [Fact]
    public void Serialize_IntegrationEventOfAnotherModule_Throws()
    {
        var act = () => IntegrationEventSerializer.Serialize(new ForeignEvent(EventId));

        act.Should().Throw<InvalidOperationException>().WithMessage("*not an integration event of the Servicing module*");
    }

    [Fact]
    public void Deserialize_AtAnotherVersion_ThrowsBecauseNoUpcasterExists()
    {
        var (type, _, json) = IntegrationEventSerializer.Serialize(Published_EveryEvent()[1]);

        var act = () => IntegrationEventSerializer.Deserialize(type, 3, json);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no upcaster*");
    }

    private sealed record ForeignEvent(Guid EventId) : IIntegrationEvent;
}
