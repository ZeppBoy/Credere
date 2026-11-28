using System.Text.Json;
using System.Text.Json.Nodes;
using Credere.Core.ValueObjects;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain.Events;
using Credere.Modules.Origination.Infrastructure.Serialization;
using FluentAssertions;
using NodaTime;
using Xunit;
using static Credere.Modules.Origination.Tests.Domain.ApplicationSamples;

namespace Credere.Modules.Origination.Tests.Infrastructure;

public sealed class OriginationJsonTests
{
    public static TheoryData<LoanApplicationState> EveryState() => new()
    {
        LoanApplicationState.Submitted,
        LoanApplicationState.UnderReview,
        LoanApplicationState.Approved,
        LoanApplicationState.AcceptedByBorrower,
        LoanApplicationState.Declined,
        LoanApplicationState.Withdrawn,
    };

    [Theory]
    [MemberData(nameof(EveryState))]
    public void RoundTrip_EveryEventOfAnApplicationInThatState_ComesBackEqual(LoanApplicationState state)
    {
        var events = InState(state).UncommittedEvents;

        foreach (var original in events)
        {
            var (type, version, json) = LoanApplicationEventSerializer.Serialize(original);

            var restored = LoanApplicationEventSerializer.Deserialize(type, version, json);

            restored.Should().Be(original, "{0} must survive its own history row", type);
        }
    }

    [Fact]
    public void RoundTrip_AllSixEventTypes_AreCoveredByTheStates()
    {
        var typesSeen = new[]
            {
                LoanApplicationState.Submitted,
                LoanApplicationState.UnderReview,
                LoanApplicationState.AcceptedByBorrower,
                LoanApplicationState.Declined,
                LoanApplicationState.Withdrawn,
            }
            .SelectMany(state => InState(state).UncommittedEvents)
            .Select(@event => @event.GetType())
            .Distinct()
            .ToList();

        typesSeen.Should().HaveCount(6).And.AllSatisfy(type => type.IsSubclassOf(typeof(LoanApplicationEvent)));
    }

    [Fact]
    public void Money_KeepsEveryDecimalDigitOfALargeAmount()
    {
        var money = Eur(12_345_678_901_234.56m);

        var json = JsonSerializer.Serialize(money, OriginationJson.Options);
        var restored = JsonSerializer.Deserialize<Money>(json, OriginationJson.Options);

        json.Should().Be("{\"Amount\":12345678901234.56,\"Currency\":\"EUR\"}");
        restored.Should().Be(money);
    }

    [Fact]
    public void Money_OfDecimalMaxValue_RoundTrips()
    {
        var money = Eur(decimal.MaxValue);

        var restored = JsonSerializer.Deserialize<Money>(
            JsonSerializer.Serialize(money, OriginationJson.Options),
            OriginationJson.Options);

        restored.Should().Be(money);
    }

    [Fact]
    public void Percentage_KeepsTwentyEightFractionalDigits()
    {
        var rate = new Percentage(7.1234567890123456789012345678m);

        var restored = JsonSerializer.Deserialize<Percentage>(
            JsonSerializer.Serialize(rate, OriginationJson.Options),
            OriginationJson.Options);

        restored.Should().Be(rate);
        restored!.Value.Should().Be(7.1234567890123456789012345678m);
    }

    [Fact]
    public void LocalDate_InAnotherCalendar_IsWrittenAsTheSameDayInTheIsoCalendar()
    {
        var julian = new LocalDate(1990, 2, 12, CalendarSystem.Julian);

        var json = JsonSerializer.Serialize(julian, OriginationJson.Options);
        var restored = JsonSerializer.Deserialize<LocalDate>(json, OriginationJson.Options);

        json.Should().Be("\"1990-02-25\"");
        restored.Should().Be(new LocalDate(1990, 2, 25));
    }

    [Fact]
    public void Instant_KeepsNanoseconds()
    {
        var instant = Instant.FromUtc(2026, 11, 25, 9, 0).PlusNanoseconds(123_456_789);

        var json = JsonSerializer.Serialize(instant, OriginationJson.Options);
        var restored = JsonSerializer.Deserialize<Instant>(json, OriginationJson.Options);

        json.Should().Be("\"2026-11-25T09:00:00.123456789Z\"");
        restored.Should().Be(instant);
    }

    [Fact]
    public void Deserialize_MoneyWithAnInvalidCurrency_Throws()
    {
        var act = () => JsonSerializer.Deserialize<Money>("{\"Amount\":1,\"Currency\":\"EU\"}", OriginationJson.Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_PercentageBelowZero_Throws()
    {
        var act = () => JsonSerializer.Deserialize<Percentage>("-1", OriginationJson.Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_LoanTermOutsideTheAllowedRange_Throws()
    {
        var act = () => JsonSerializer.Deserialize<LoanTerm>("0", OriginationJson.Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_ProductCodeInLowerCase_Throws()
    {
        var act = () => JsonSerializer.Deserialize<ProductCode>("\"term-fixed\"", OriginationJson.Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_AnEventMissingAField_Throws()
    {
        var (type, version, json) = LoanApplicationEventSerializer.Serialize(InState(LoanApplicationState.Approved).UncommittedEvents[1]);
        var payload = JsonNode.Parse(json)!.AsObject();
        payload.Remove("Rationale");

        var act = () => LoanApplicationEventSerializer.Deserialize(type, version, payload.ToJsonString());

        act.Should().Throw<JsonException>("a missing field must not become a default");
    }

    [Fact]
    public void Deserialize_AnEventWithAnUnknownField_Throws()
    {
        var (type, version, json) = LoanApplicationEventSerializer.Serialize(Submitted().UncommittedEvents[0]);
        var payload = JsonNode.Parse(json)!.AsObject();
        payload["Surprise"] = 1;

        var act = () => LoanApplicationEventSerializer.Deserialize(type, version, payload.ToJsonString());

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_AnEventWithANullRequiredField_Throws()
    {
        var (type, version, json) = LoanApplicationEventSerializer.Serialize(InState(LoanApplicationState.Approved).UncommittedEvents[1]);
        var payload = JsonNode.Parse(json)!.AsObject();
        payload["FinalTerms"] = null;

        var act = () => LoanApplicationEventSerializer.Deserialize(type, version, payload.ToJsonString());

        act.Should().Throw<JsonException>();
    }
}
