using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain;
using Credere.Modules.Origination.Domain.Events;
using FluentAssertions;
using Xunit;
using static Credere.Modules.Origination.Tests.Domain.ApplicationSamples;

namespace Credere.Modules.Origination.Tests.Domain;

public sealed class LoanApplicationRehydrateTests
{
    public static TheoryData<LoanApplicationState> EveryState()
    {
        var data = new TheoryData<LoanApplicationState>();
        foreach (var state in Enum.GetValues<LoanApplicationState>())
        {
            data.Add(state);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryState))]
    public void Rehydrate_TheHistoryOfAnApplication_RestoresItsState(LoanApplicationState state)
    {
        var original = InState(state);

        var replayed = LoanApplication.Rehydrate(original.UncommittedEvents);

        replayed.Id.Should().Be(original.Id);
        replayed.State.Should().Be(original.State);
        replayed.Applicant.Should().Be(original.Applicant);
        replayed.Product.Should().Be(original.Product);
        replayed.Terms.Should().Be(original.Terms);
        replayed.SubmittedAt.Should().Be(original.SubmittedAt);
        replayed.TermsVersion.Should().Be(original.TermsVersion);
        replayed.DecidedTerms.Should().Be(original.DecidedTerms);
        replayed.Decision.Should().Be(original.Decision);
        replayed.Version.Should().Be(original.Version);
        replayed.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void Rehydrate_AReviewThenApprovalThenAcceptance_RestoresTheFullPath()
    {
        var original = InState(LoanApplicationState.UnderReview);
        original.Decide(ApproveCommand(), Later).IsSuccess.Should().BeTrue();
        original.AcceptTerms(AcceptCommand(1), Later).IsSuccess.Should().BeTrue();

        var replayed = LoanApplication.Rehydrate(original.UncommittedEvents);

        replayed.State.Should().Be(LoanApplicationState.AcceptedByBorrower);
        replayed.Version.Should().Be(4);
        replayed.Decision!.Outcome.Should().Be(DecisionOutcome.Approve);
    }

    [Fact]
    public void Rehydrate_ThenACommand_ContinuesFromTheReplayedState()
    {
        var original = InState(LoanApplicationState.Approved);
        var replayed = LoanApplication.Rehydrate(original.UncommittedEvents);

        var result = replayed.AcceptTerms(AcceptCommand(1), Later);

        result.IsSuccess.Should().BeTrue();
        replayed.State.Should().Be(LoanApplicationState.AcceptedByBorrower);
        replayed.Version.Should().Be(original.Version + 1);
        replayed.UncommittedEvents.Should().HaveCount(1);
    }

    [Fact]
    public void MarkEventsCommitted_ClearsTheUncommittedEventsAndKeepsTheVersion()
    {
        var application = InState(LoanApplicationState.Approved);
        var version = application.Version;

        application.MarkEventsCommitted();

        application.UncommittedEvents.Should().BeEmpty();
        application.Version.Should().Be(version);
        application.Withdraw(WithdrawCommand(), Later).IsSuccess.Should().BeTrue();
        application.UncommittedEvents.Should().HaveCount(1);
        application.Version.Should().Be(version + 1);
    }

    [Fact]
    public void Rehydrate_NoEvents_Throws()
    {
        var act = () => LoanApplication.Rehydrate([]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rehydrate_NullHistory_Throws()
    {
        var act = () => LoanApplication.Rehydrate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Rehydrate_HistoryNotStartingWithASubmission_Throws()
    {
        var approved = InState(LoanApplicationState.Approved).UncommittedEvents.Skip(1);

        var act = () => LoanApplication.Rehydrate(approved);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rehydrate_ASecondSubmission_Throws()
    {
        var submitted = Submitted().UncommittedEvents[0];

        var act = () => LoanApplication.Rehydrate([submitted, submitted]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rehydrate_AnEventOfAnotherApplication_Throws()
    {
        var events = InState(LoanApplicationState.Declined).UncommittedEvents.ToList();
        events[1] = events[1] with { ApplicationId = new LoanApplicationId(Guid.NewGuid()) };

        var act = () => LoanApplication.Rehydrate(events);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(LoanApplicationState.Declined)]
    [InlineData(LoanApplicationState.Withdrawn)]
    [InlineData(LoanApplicationState.AcceptedByBorrower)]
    public void Rehydrate_AnEventAfterTheEnd_Throws(LoanApplicationState terminal)
    {
        var events = InState(terminal).UncommittedEvents.ToList();
        events.Add(new ApplicationWithdrawn(SampleApplicationId, Later, CommandId, CorrelationId, "Too late."));

        var act = () => LoanApplication.Rehydrate(events);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Rehydrate_AnApprovalThatSkipsAVersion_Throws(int termsVersion)
    {
        var events = InState(LoanApplicationState.Approved).UncommittedEvents.ToList();
        events[1] = ((ApplicationApproved)events[1]) with { TermsVersion = termsVersion };

        var act = () => LoanApplication.Rehydrate(events);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rehydrate_AnAcceptanceOfAnotherVersion_Throws()
    {
        var events = InState(LoanApplicationState.AcceptedByBorrower).UncommittedEvents.ToList();
        events[2] = ((ApplicationAccepted)events[2]) with { TermsVersion = 2 };

        var act = () => LoanApplication.Rehydrate(events);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rehydrate_AnAcceptanceOfOtherTermsThanTheApproval_Throws()
    {
        var events = InState(LoanApplicationState.AcceptedByBorrower).UncommittedEvents.ToList();
        var accepted = (ApplicationAccepted)events[2];
        events[2] = accepted with { AcceptedTerms = FinalTerms(principal: 9_500m) };

        var act = () => LoanApplication.Rehydrate(events);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rehydrate_AnAcceptanceOfAnotherProduct_Throws()
    {
        var events = InState(LoanApplicationState.AcceptedByBorrower).UncommittedEvents.ToList();
        var accepted = (ApplicationAccepted)events[2];
        events[2] = accepted with { Product = ProductCode.Create("TERM-OTHER").Value };

        var act = () => LoanApplication.Rehydrate(events);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rehydrate_AnAcceptanceThatRestatesTheApproval_Succeeds()
    {
        var events = InState(LoanApplicationState.AcceptedByBorrower).UncommittedEvents.ToList();
        var accepted = (ApplicationAccepted)events[2];
        events[2] = accepted with { AcceptedTerms = FinalTerms() };

        var replayed = LoanApplication.Rehydrate(events);

        replayed.State.Should().Be(LoanApplicationState.AcceptedByBorrower);
    }

    [Fact]
    public void Rehydrate_AnUnknownEventType_Throws()
    {
        var events = new List<LoanApplicationEvent>(Submitted().UncommittedEvents)
        {
            new UnknownEvent(SampleApplicationId, Later, CommandId, CorrelationId),
        };

        var act = () => LoanApplication.Rehydrate(events);

        act.Should().Throw<InvalidOperationException>();
    }

    private sealed record UnknownEvent(
        LoanApplicationId ApplicationId,
        NodaTime.Instant OccurredAt,
        Guid CommandId,
        Guid CorrelationId) : LoanApplicationEvent(ApplicationId, OccurredAt, CommandId, CorrelationId);
}
