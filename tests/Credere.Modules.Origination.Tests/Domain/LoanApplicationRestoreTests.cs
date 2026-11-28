using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain;
using Credere.Modules.Origination.Domain.Events;
using FluentAssertions;
using Xunit;
using static Credere.Modules.Origination.Tests.Domain.ApplicationSamples;

namespace Credere.Modules.Origination.Tests.Domain;

public sealed class LoanApplicationRestoreTests
{
    [Theory]
    [InlineData(LoanApplicationState.Submitted)]
    [InlineData(LoanApplicationState.UnderReview)]
    [InlineData(LoanApplicationState.Approved)]
    [InlineData(LoanApplicationState.AcceptedByBorrower)]
    [InlineData(LoanApplicationState.Declined)]
    [InlineData(LoanApplicationState.Withdrawn)]
    public void Restore_CommittedSnapshot_RestoresAllStateWithoutRaisingEvents(LoanApplicationState state)
    {
        var original = InState(state);

        var restored = LoanApplication.Restore(SnapshotOf(original));

        SnapshotOf(restored).Should().Be(SnapshotOf(original));
        restored.UncommittedEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(LoanApplicationState.UnderReview)]
    [InlineData(LoanApplicationState.Approved)]
    public void Restore_WithdrawalAfterADecision_PreservesTheDecisionAndTerms(LoanApplicationState priorState)
    {
        var original = InState(priorState);
        original.Withdraw(WithdrawCommand(), Later).IsSuccess.Should().BeTrue();

        var restored = LoanApplication.Restore(SnapshotOf(original));

        SnapshotOf(restored).Should().Be(SnapshotOf(original));
        restored.UncommittedEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(LoanApplicationState.Approved, 3)]
    [InlineData(LoanApplicationState.Declined, 3)]
    [InlineData(LoanApplicationState.AcceptedByBorrower, 4)]
    [InlineData(LoanApplicationState.Withdrawn, 4)]
    public void Restore_OptionalReviewBeforeDecision_PreservesTheLongerStream(LoanApplicationState state, int version)
    {
        var original = InState(LoanApplicationState.UnderReview);
        var decision = state == LoanApplicationState.Declined ? DecideCommand(DecisionOutcome.Decline) : ApproveCommand();
        original.Decide(decision, Later).IsSuccess.Should().BeTrue();
        if (state == LoanApplicationState.AcceptedByBorrower)
        {
            original.AcceptTerms(AcceptCommand(1), Later).IsSuccess.Should().BeTrue();
        }
        else if (state == LoanApplicationState.Withdrawn)
        {
            original.Withdraw(WithdrawCommand(), Later).IsSuccess.Should().BeTrue();
        }

        var restored = LoanApplication.Restore(SnapshotOf(original));

        restored.State.Should().Be(state);
        restored.Version.Should().Be(version);
        SnapshotOf(restored).Should().Be(SnapshotOf(LoanApplication.Rehydrate(original.UncommittedEvents)));
        restored.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void Restore_ThenAcceptTerms_AppendsOnlyTheNewEventAtTheNextVersion()
    {
        var original = InState(LoanApplicationState.UnderReview);
        original.Decide(ApproveCommand(), Later).IsSuccess.Should().BeTrue();
        var restored = LoanApplication.Restore(SnapshotOf(original));

        var result = restored.AcceptTerms(AcceptCommand(original.TermsVersion), Later);

        result.IsSuccess.Should().BeTrue();
        restored.State.Should().Be(LoanApplicationState.AcceptedByBorrower);
        restored.Version.Should().Be(original.Version + 1);
        var raised = restored.UncommittedEvents.Should().ContainSingle().Which;
        var accepted = raised.Should().BeOfType<ApplicationAccepted>().Which;
        accepted.AcceptedTerms.Should().Be(original.DecidedTerms);
        accepted.TermsVersion.Should().Be(original.TermsVersion);
        accepted.Product.Should().Be(original.Product);
    }

    [Fact]
    public void Restore_ApprovalWithoutDecidedTerms_Throws()
    {
        var snapshot = SnapshotOf(InState(LoanApplicationState.Approved)) with { DecidedTerms = null };

        var act = () => LoanApplication.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(LoanApplicationState.Approved, LoanApplicationState.Declined)]
    [InlineData(LoanApplicationState.Declined, LoanApplicationState.Withdrawn)]
    public void Restore_DecisionDoesNotMatchTheState_Throws(LoanApplicationState original, LoanApplicationState state)
    {
        var snapshot = SnapshotOf(InState(original)) with { State = state };

        var act = () => LoanApplication.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(LoanApplicationState.AcceptedByBorrower, 2)]
    [InlineData(LoanApplicationState.Submitted, 40)]
    [InlineData(LoanApplicationState.Approved, 4)]
    public void Restore_VersionCannotProduceTheStoredState_Throws(LoanApplicationState state, int version)
    {
        var snapshot = SnapshotOf(InState(state)) with { Version = version };

        var act = () => LoanApplication.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(LoanApplicationState.Approved, 0)]
    [InlineData(LoanApplicationState.Approved, 5)]
    [InlineData(LoanApplicationState.UnderReview, 1)]
    public void Restore_TermsVersionDoesNotMatchTheDecision_Throws(LoanApplicationState state, int termsVersion)
    {
        var snapshot = SnapshotOf(InState(state)) with { TermsVersion = termsVersion };

        var act = () => LoanApplication.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Restore_DecidedTermsWithoutAnApproval_Throws()
    {
        var snapshot = SnapshotOf(InState(LoanApplicationState.UnderReview)) with { DecidedTerms = FinalTerms() };

        var act = () => LoanApplication.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>();
    }

    private static LoanApplicationSnapshot SnapshotOf(LoanApplication application) => new(
        application.Id,
        application.Applicant,
        application.Product,
        application.Terms,
        application.SubmittedAt,
        application.State,
        application.Decision,
        application.DecidedTerms,
        application.TermsVersion,
        application.Version);
}
