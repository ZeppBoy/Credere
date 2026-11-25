using Credere.Core.Results;
using Credere.Core.ValueObjects;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain;
using Credere.Modules.Origination.Domain.Events;
using FluentAssertions;
using Xunit;
using static Credere.Modules.Origination.Tests.Domain.ApplicationSamples;

namespace Credere.Modules.Origination.Tests.Domain;

public sealed class LoanApplicationDecideTests
{
    [Theory]
    [InlineData(LoanApplicationState.Submitted)]
    [InlineData(LoanApplicationState.UnderReview)]
    public void Decide_Approve_RecordsTheFinalTermsAsVersionOne(LoanApplicationState from)
    {
        var application = InState(from);
        var finalTerms = FinalTerms();
        var command = ApproveCommand(finalTerms);
        var eventsBefore = application.UncommittedEvents.Count;
        var versionBefore = application.Version;

        var result = application.Decide(command, Later);

        result.IsSuccess.Should().BeTrue();
        application.State.Should().Be(LoanApplicationState.Approved);
        application.TermsVersion.Should().Be(1);
        application.DecidedTerms.Should().Be(finalTerms);
        application.Decision.Should().Be(new UnderwritingDecision(DecisionOutcome.Approve, command.Rationale, Later));
        application.Version.Should().Be(versionBefore + 1);
        application.UncommittedEvents.Should().HaveCount(eventsBefore + 1);
        var approved = application.UncommittedEvents[^1].Should().BeOfType<ApplicationApproved>().Subject;
        approved.ApplicationId.Should().Be(SampleApplicationId);
        approved.OccurredAt.Should().Be(Later);
        approved.CommandId.Should().Be(command.CommandId);
        approved.CorrelationId.Should().Be(command.CorrelationId);
        approved.Rationale.Should().Be(command.Rationale);
        approved.FinalTerms.Should().Be(finalTerms);
        approved.TermsVersion.Should().Be(1);
    }

    [Theory]
    [InlineData(LoanApplicationState.Submitted)]
    [InlineData(LoanApplicationState.UnderReview)]
    public void Decide_Decline_EndsTheApplicationWithoutDecidedTerms(LoanApplicationState from)
    {
        var application = InState(from);
        var command = DecideCommand(DecisionOutcome.Decline, "Debt-to-income is too high.");

        var result = application.Decide(command, Later);

        result.IsSuccess.Should().BeTrue();
        application.State.Should().Be(LoanApplicationState.Declined);
        application.TermsVersion.Should().Be(0);
        application.DecidedTerms.Should().BeNull();
        application.Decision.Should().Be(new UnderwritingDecision(DecisionOutcome.Decline, command.Rationale, Later));
        var declined = application.UncommittedEvents[^1].Should().BeOfType<ApplicationDeclined>().Subject;
        declined.Rationale.Should().Be(command.Rationale);
        declined.OccurredAt.Should().Be(Later);
    }

    [Fact]
    public void Decide_HumanReview_MovesASubmittedApplicationToUnderReview()
    {
        var application = Submitted();
        var command = DecideCommand(DecisionOutcome.HumanReview, "Income could not be verified.");

        var result = application.Decide(command, Later);

        result.IsSuccess.Should().BeTrue();
        application.State.Should().Be(LoanApplicationState.UnderReview);
        application.TermsVersion.Should().Be(0);
        application.Decision.Should().Be(new UnderwritingDecision(DecisionOutcome.HumanReview, command.Rationale, Later));
        var review = application.UncommittedEvents[^1].Should().BeOfType<ApplicationSentToReview>().Subject;
        review.Rationale.Should().Be(command.Rationale);
    }

    [Fact]
    public void Decide_ReviewThenApprove_KeepsTheLatestDecision()
    {
        var application = InState(LoanApplicationState.UnderReview);

        application.Decide(ApproveCommand(), Later).IsSuccess.Should().BeTrue();

        application.Decision!.Outcome.Should().Be(DecisionOutcome.Approve);
        application.UncommittedEvents.Select(@event => @event.GetType()).Should().Equal(
            typeof(ApplicationSubmitted),
            typeof(ApplicationSentToReview),
            typeof(ApplicationApproved));
    }

    [Theory]
    [InlineData(DecisionOutcome.Approve)]
    [InlineData(DecisionOutcome.Decline)]
    [InlineData(DecisionOutcome.HumanReview)]
    public void Decide_BlankRationale_ReturnsValidationError(DecisionOutcome outcome)
    {
        foreach (var rationale in new[] { string.Empty, "   " })
        {
            var application = Submitted();
            var finalTerms = outcome == DecisionOutcome.Approve ? FinalTerms() : null;

            var result = application.Decide(DecideCommand(outcome, rationale, finalTerms), Later);

            result.IsSuccess.Should().BeFalse();
            result.Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("Rationale");
            AssertUnchanged(application);
        }
    }

    [Fact]
    public void Decide_ApproveWithoutFinalTerms_ReturnsValidationError()
    {
        var application = Submitted();

        var result = application.Decide(DecideCommand(DecisionOutcome.Approve), Later);

        result.Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("FinalTerms");
        AssertUnchanged(application);
    }

    [Theory]
    [InlineData(DecisionOutcome.Decline)]
    [InlineData(DecisionOutcome.HumanReview)]
    public void Decide_NonApprovalWithFinalTerms_ReturnsValidationError(DecisionOutcome outcome)
    {
        var application = Submitted();

        var result = application.Decide(DecideCommand(outcome, finalTerms: FinalTerms()), Later);

        result.Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("FinalTerms");
        AssertUnchanged(application);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Decide_ApproveWithNonPositivePrincipal_ReturnsValidationError(int principal)
    {
        var application = Submitted();

        var result = application.Decide(ApproveCommand(FinalTerms(principal: principal)), Later);

        result.Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("DecidedTerms.Principal");
        AssertUnchanged(application);
    }

    [Fact]
    public void Decide_ApproveWithRateOfExactlyOneHundredPercent_IsAccepted()
    {
        var application = Submitted();

        var result = application.Decide(ApproveCommand(FinalTerms(ratePercent: 100m)), Later);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Decide_ApproveWithRateOfZeroPercent_IsAccepted()
    {
        var application = Submitted();

        var result = application.Decide(ApproveCommand(FinalTerms(ratePercent: 0m)), Later);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Decide_ApproveWithRateAboveOneHundredPercent_ReturnsValidationError()
    {
        var application = Submitted();

        var result = application.Decide(ApproveCommand(FinalTerms(ratePercent: 100.01m)), Later);

        result.Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("DecidedTerms.NominalRate");
        AssertUnchanged(application);
    }

    [Fact]
    public void Decide_UnknownOutcome_ReturnsValidationError()
    {
        var application = Submitted();

        var result = application.Decide(DecideCommand((DecisionOutcome)99), Later);

        result.Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("Outcome");
        AssertUnchanged(application);
    }

    [Fact]
    public void Decide_OnADeclinedApplicationWithABlankRationale_ReportsTheStateFirst()
    {
        var application = InState(LoanApplicationState.Declined);

        var result = application.Decide(DecideCommand(DecisionOutcome.Approve, rationale: " ", finalTerms: FinalTerms()), Later);

        result.Error.Should().BeOfType<InvalidStateTransition>();
    }

    [Fact]
    public void Decide_CommandForAnotherApplication_Throws()
    {
        var application = Submitted();
        var other = ApproveCommand() with { Id = new LoanApplicationId(Guid.NewGuid()) };

        var act = () => application.Decide(other, Later);

        act.Should().Throw<ArgumentException>();
        AssertUnchanged(application);
    }

    [Fact]
    public void Decide_NullCommand_Throws()
    {
        var act = () => Submitted().Decide(null!, Later);

        act.Should().Throw<ArgumentNullException>();
    }

    private static void AssertUnchanged(LoanApplication application)
    {
        application.State.Should().Be(LoanApplicationState.Submitted);
        application.TermsVersion.Should().Be(0);
        application.DecidedTerms.Should().BeNull();
        application.Decision.Should().BeNull();
        application.Version.Should().Be(1);
        application.UncommittedEvents.Should().HaveCount(1);
    }
}
