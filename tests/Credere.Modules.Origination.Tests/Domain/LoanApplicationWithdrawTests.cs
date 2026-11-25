using Credere.Core.Results;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain.Events;
using FluentAssertions;
using Xunit;
using static Credere.Modules.Origination.Tests.Domain.ApplicationSamples;

namespace Credere.Modules.Origination.Tests.Domain;

public sealed class LoanApplicationWithdrawTests
{
    [Theory]
    [InlineData(LoanApplicationState.Submitted)]
    [InlineData(LoanApplicationState.UnderReview)]
    [InlineData(LoanApplicationState.Approved)]
    public void Withdraw_BeforeAcceptance_EndsTheApplication(LoanApplicationState from)
    {
        var application = InState(from);
        var command = WithdrawCommand("Found a better offer.");
        var versionBefore = application.Version;

        var result = application.Withdraw(command, Later);

        result.IsSuccess.Should().BeTrue();
        application.State.Should().Be(LoanApplicationState.Withdrawn);
        application.Version.Should().Be(versionBefore + 1);
        var withdrawn = application.UncommittedEvents[^1].Should().BeOfType<ApplicationWithdrawn>().Subject;
        withdrawn.ApplicationId.Should().Be(SampleApplicationId);
        withdrawn.OccurredAt.Should().Be(Later);
        withdrawn.CommandId.Should().Be(command.CommandId);
        withdrawn.CorrelationId.Should().Be(command.CorrelationId);
        withdrawn.Reason.Should().Be("Found a better offer.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Withdraw_BlankReason_ReturnsValidationErrorAndChangesNothing(string reason)
    {
        var application = Submitted();

        var result = application.Withdraw(WithdrawCommand(reason), Later);

        result.Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("Reason");
        application.State.Should().Be(LoanApplicationState.Submitted);
        application.UncommittedEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Withdraw_AfterTheBorrowerAccepted_IsRejectedBecauseTheHandoffIsUnderWay()
    {
        var application = InState(LoanApplicationState.AcceptedByBorrower);

        var result = application.Withdraw(WithdrawCommand(), Later);

        var error = result.Error.Should().BeOfType<InvalidStateTransition>().Subject;
        error.FromState.Should().Be("AcceptedByBorrower");
        error.Command.Should().Be("WithdrawApplication");
        error.Reason.Should().Contain("handoff");
    }

    [Fact]
    public void Withdraw_CommandForAnotherApplication_Throws()
    {
        var application = Submitted();
        var other = WithdrawCommand() with { Id = new LoanApplicationId(Guid.NewGuid()) };

        var act = () => application.Withdraw(other, Later);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Withdraw_NullCommand_Throws()
    {
        var act = () => Submitted().Withdraw(null!, Later);

        act.Should().Throw<ArgumentNullException>();
    }
}
