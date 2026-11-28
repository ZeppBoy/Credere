using System.Text.Json;
using System.Text.Json.Nodes;
using Credere.Core.DayCounts;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain;
using Credere.Modules.Servicing.Domain.Events;
using Credere.Modules.Servicing.Infrastructure.Serialization;
using FluentAssertions;
using NodaTime;
using Xunit;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;
using static Credere.Modules.Servicing.Tests.Domain.Loans.LoanSamples;

namespace Credere.Modules.Servicing.Tests.Infrastructure;

public sealed class ServicingJsonTests
{
    public static TheoryData<LoanState> EveryState() => new()
    {
        LoanState.Disbursed,
        LoanState.Active,
        LoanState.InGrace,
        LoanState.PastDue,
        LoanState.Defaulted,
        LoanState.WrittenOff,
        LoanState.Restructured,
        LoanState.Recovered,
        LoanState.PaidOff,
    };

    [Theory]
    [MemberData(nameof(EveryState))]
    public void RoundTrip_EveryEventOfALoanInThatState_ComesBackEqual(LoanState state)
    {
        foreach (var original in CreateLoan(state).UncommittedEvents)
        {
            var (type, version, json) = LoanEventSerializer.Serialize(original);

            var restored = LoanEventSerializer.Deserialize(type, version, json);

            restored.Should().Be(original, "{0} must survive its own history row", type);
        }
    }

    [Fact]
    public void RoundTrip_TheLifecyclesTogetherCoverEveryRegisteredEventType()
    {
        var loan = CreateLoan(LoanState.PastDue);
        _ = loan.ApplyAging(Age(PastDueThreshold + 1)).Value;
        _ = loan.ApplyAging(Age(0)).Value;
        _ = loan.RecordPayment(Pay(Euro(10006.95m), Date("2026-01-20")), SamplePaymentId, Calculator).Value;
        var events = loan.UncommittedEvents
            .Concat(Enum.GetValues<LoanState>().Select(state => CreateLoan(state)).SelectMany(item => item.UncommittedEvents))
            .ToList();

        foreach (var original in events)
        {
            var (type, version, json) = LoanEventSerializer.Serialize(original);
            LoanEventSerializer.Deserialize(type, version, json).Should().Be(original);
        }

        events.Select(item => item.GetType().Name).Distinct().Should().BeEquivalentTo(LoanEventSerializer.Versions.Keys);
    }

    [Fact]
    public void Serialize_DomainEvent_UsesTheShortNameAndTheDeclaredVersion()
    {
        var (type, version, _) = LoanEventSerializer.Serialize(CreateLoan().UncommittedEvents[0]);

        type.Should().Be(nameof(LoanDisbursed));
        version.Should().Be(1);
    }

    [Fact]
    public void Deserialize_DomainEventOfUnknownType_Throws()
    {
        var act = () => LoanEventSerializer.Deserialize("LoanTeleported", 1, "{}");

        act.Should().Throw<InvalidOperationException>().WithMessage("*unknown type*");
    }

    [Fact]
    public void Deserialize_DomainEventAtAnotherVersion_ThrowsBecauseNoUpcasterExists()
    {
        var (type, _, json) = LoanEventSerializer.Serialize(CreateLoan().UncommittedEvents[0]);

        var act = () => LoanEventSerializer.Deserialize(type, 2, json);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no upcaster*");
    }

    [Fact]
    public void Deserialize_AnEventMissingAField_Throws()
    {
        var (type, version, json) = LoanEventSerializer.Serialize(CreateLoan(LoanState.WrittenOff).UncommittedEvents[^1]);
        var payload = JsonNode.Parse(json)!.AsObject();
        payload.Remove("Reason");

        var act = () => LoanEventSerializer.Deserialize(type, version, payload.ToJsonString());

        act.Should().Throw<JsonException>("a missing field must not become a default");
    }

    [Fact]
    public void Deserialize_AnEventWithAnUnknownField_Throws()
    {
        var (type, version, json) = LoanEventSerializer.Serialize(CreateLoan().UncommittedEvents[0]);
        var payload = JsonNode.Parse(json)!.AsObject();
        payload["Surprise"] = 1;

        var act = () => LoanEventSerializer.Deserialize(type, version, payload.ToJsonString());

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_AnEnumWrittenAsANumber_Throws()
    {
        var (type, version, json) = LoanEventSerializer.Serialize(CreateLoan(LoanState.PastDue).UncommittedEvents[^1]);
        var payload = JsonNode.Parse(json)!.AsObject();
        payload["PreviousState"] = 1;

        var act = () => LoanEventSerializer.Deserialize(type, version, payload.ToJsonString());

        act.Should().Throw<JsonException>("a reordered enum must not change what a stored row means");
    }

    [Fact]
    public void DayCountConvention_EveryPublicConventionOfTheCoreAssemblyIsKnownByName()
    {
        // The internal APRC convention (EuCcdAnnexIII) is not a loan day count. docs/modules/servicing.md names the
        // five public ones as the products' choices.
        var inCore = typeof(DayCountConvention).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true } && type.IsSubclassOf(typeof(DayCountConvention)))
            .Select(type => ((DayCountConvention)Activator.CreateInstance(type)!).Name)
            .ToList();

        DayCountConventionJsonConverter.Conventions.Keys.Should().BeEquivalentTo(inCore);
    }

    [Fact]
    public void DayCountConvention_RoundTripsEveryBuiltInByName()
    {
        foreach (var convention in DayCountConventionJsonConverter.Conventions.Values)
        {
            var json = JsonSerializer.Serialize(convention, ServicingJson.Options);

            json.Should().Be($"\"{convention.Name}\"");
            JsonSerializer.Deserialize<DayCountConvention>(json, ServicingJson.Options).Should().Be(convention);
        }
    }

    [Fact]
    public void DayCountConvention_UnknownName_Throws()
    {
        var act = () => JsonSerializer.Deserialize<DayCountConvention>("\"ACT/999\"", ServicingJson.Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Schedule_RoundTripsEveryRow()
    {
        var json = JsonSerializer.Serialize(Schedule, ServicingJson.Options);

        JsonSerializer.Deserialize<AmortizationSchedule>(json, ServicingJson.Options).Should().Be(Schedule);
    }

    [Fact]
    public void Schedule_EmptyRows_Throws()
    {
        var act = () => JsonSerializer.Deserialize<AmortizationSchedule>("[]", ServicingJson.Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void ProductTerms_RoundTrip_ComesBackEqual()
    {
        var json = JsonSerializer.Serialize(Product, ServicingJson.Options);

        JsonSerializer.Deserialize<ProductTerms>(json, ServicingJson.Options).Should().Be(Product);
    }

    [Fact]
    public void ProductTerms_DefaultThresholdBelowThePastDueThreshold_Throws()
    {
        var json = JsonSerializer.Serialize(Product, ServicingJson.Options);
        var payload = JsonNode.Parse(json)!.AsObject();
        payload["DefaultThreshold"] = 1;
        payload["PastDueThreshold"] = 5;

        var act = () => JsonSerializer.Deserialize<ProductTerms>(payload.ToJsonString(), ServicingJson.Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void LocalDate_InAnotherCalendar_IsWrittenAsTheSameDayInTheIsoCalendar()
    {
        var julian = new LocalDate(1990, 2, 12, CalendarSystem.Julian);

        var json = JsonSerializer.Serialize(julian, ServicingJson.Options);

        json.Should().Be("\"1990-02-25\"");
        JsonSerializer.Deserialize<LocalDate>(json, ServicingJson.Options).Should().Be(new LocalDate(1990, 2, 25));
    }

    [Fact]
    public void Money_KeepsEveryDecimalDigitIncludingDecimalMaxValue()
    {
        foreach (var amount in new[] { 0.01m, 100m, 100.50m, 12_345_678_901_234.56m, decimal.MaxValue })
        {
            var money = Euro(amount);

            var restored = JsonSerializer.Deserialize<Money>(
                JsonSerializer.Serialize(money, ServicingJson.Options),
                ServicingJson.Options);

            restored.Should().Be(money);
        }
    }
}
