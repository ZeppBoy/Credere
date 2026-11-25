using Credere.Core.Results;
using Credere.Core.ValueObjects;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain;
using FluentAssertions;
using NodaTime;
using Xunit;
using static Credere.Modules.Origination.Tests.Domain.ApplicationSamples;

namespace Credere.Modules.Origination.Tests.Domain;

/// <summary>
/// Every (state, command) pair of the transition table in docs/modules/origination.md ("States and transitions"),
/// written out by hand from that table so it checks the aggregate rather than restating its code.
/// </summary>
public sealed class LoanApplicationTransitionTests
{
    private const string Approve = "Approve";
    private const string Decline = "Decline";
    private const string HumanReview = "HumanReview";
    private const string AcceptTerms = "AcceptTerms";
    private const string Withdraw = "Withdraw";

    // The target state, or null when the table has no row for the pair, so InvalidStateTransition is expected.
    public static TheoryData<LoanApplicationState, string, LoanApplicationState?> Transitions() => new()
    {
        { LoanApplicationState.Submitted, Approve, LoanApplicationState.Approved },
        { LoanApplicationState.Submitted, Decline, LoanApplicationState.Declined },
        { LoanApplicationState.Submitted, HumanReview, LoanApplicationState.UnderReview },
        { LoanApplicationState.Submitted, AcceptTerms, null },
        { LoanApplicationState.Submitted, Withdraw, LoanApplicationState.Withdrawn },

        { LoanApplicationState.UnderReview, Approve, LoanApplicationState.Approved },
        { LoanApplicationState.UnderReview, Decline, LoanApplicationState.Declined },
        { LoanApplicationState.UnderReview, HumanReview, null },
        { LoanApplicationState.UnderReview, AcceptTerms, null },
        { LoanApplicationState.UnderReview, Withdraw, LoanApplicationState.Withdrawn },

        { LoanApplicationState.Approved, Approve, null },
        { LoanApplicationState.Approved, Decline, null },
        { LoanApplicationState.Approved, HumanReview, null },
        { LoanApplicationState.Approved, AcceptTerms, LoanApplicationState.AcceptedByBorrower },
        { LoanApplicationState.Approved, Withdraw, LoanApplicationState.Withdrawn },

        { LoanApplicationState.AcceptedByBorrower, Approve, null },
        { LoanApplicationState.AcceptedByBorrower, Decline, null },
        { LoanApplicationState.AcceptedByBorrower, HumanReview, null },
        { LoanApplicationState.AcceptedByBorrower, AcceptTerms, null },
        { LoanApplicationState.AcceptedByBorrower, Withdraw, null },

        { LoanApplicationState.Declined, Approve, null },
        { LoanApplicationState.Declined, Decline, null },
        { LoanApplicationState.Declined, HumanReview, null },
        { LoanApplicationState.Declined, AcceptTerms, null },
        { LoanApplicationState.Declined, Withdraw, null },

        { LoanApplicationState.Withdrawn, Approve, null },
        { LoanApplicationState.Withdrawn, Decline, null },
        { LoanApplicationState.Withdrawn, HumanReview, null },
        { LoanApplicationState.Withdrawn, AcceptTerms, null },
        { LoanApplicationState.Withdrawn, Withdraw, null },
    };

    [Fact]
    public void Transitions_CoverEveryStateAndCommandExactlyOnce()
    {
        var pairs = Transitions().Select(row => ((LoanApplicationState)row[0], (string)row[1])).ToList();

        pairs.Should().OnlyHaveUniqueItems();
        pairs.Should().HaveCount(Enum.GetValues<LoanApplicationState>().Length * 5);
    }

    [Theory]
    [MemberData(nameof(Transitions))]
    public void Command_InEachState_FollowsTheTransitionTable(
        LoanApplicationState from,
        string command,
        LoanApplicationState? expected)
    {
        var application = InState(from);
        var versionBefore = application.Version;
        var eventsBefore = application.UncommittedEvents.Count;

        var result = Execute(application, command);

        if (expected is { } target)
        {
            result.IsSuccess.Should().BeTrue($"{command} from {from} is a row of the table");
            application.State.Should().Be(target);
            application.Version.Should().Be(versionBefore + 1);
            application.UncommittedEvents.Should().HaveCount(eventsBefore + 1, "every transition emits one event");
        }
        else
        {
            result.IsSuccess.Should().BeFalse($"{command} from {from} has no row in the table");
            var error = result.Error.Should().BeOfType<InvalidStateTransition>().Subject;
            error.FromState.Should().Be(from.ToString());
            application.State.Should().Be(from);
            application.Version.Should().Be(versionBefore);
            application.UncommittedEvents.Should().HaveCount(eventsBefore, "a rejected command raises no event");
        }
    }

    [Fact]
    public void EveryStateIsReachableAndEveryNonTerminalStateHasAnExit()
    {
        var rows = Transitions().Select(row => ((LoanApplicationState)row[0], (LoanApplicationState?)row[2])).ToList();

        var reachable = rows.Where(row => row.Item2 is not null).Select(row => row.Item2!.Value).ToHashSet();
        reachable.Add(LoanApplicationState.Submitted);
        reachable.Should().BeEquivalentTo(Enum.GetValues<LoanApplicationState>());

        var withExit = rows.Where(row => row.Item2 is not null).Select(row => row.Item1).ToHashSet();
        withExit.Should().BeEquivalentTo(
            [LoanApplicationState.Submitted, LoanApplicationState.UnderReview, LoanApplicationState.Approved]);
    }

    private static Result<Unit, DomainError> Execute(LoanApplication application, string command) =>
        command switch
        {
            Approve => application.Decide(ApproveCommand(), Later),
            Decline => application.Decide(DecideCommand(DecisionOutcome.Decline), Later),
            HumanReview => application.Decide(DecideCommand(DecisionOutcome.HumanReview), Later),
            // Version 1 matches an approved application, so a rejection elsewhere is about the state, not the version.
            AcceptTerms => application.AcceptTerms(AcceptCommand(application.TermsVersion), Later),
            Withdraw => application.Withdraw(WithdrawCommand(), Later),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command."),
        };
}
