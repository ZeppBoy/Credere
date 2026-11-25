using Credere.Core.Results;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain.Events;
using FluentAssertions;
using Xunit;
using static Credere.Modules.Origination.Tests.Domain.ApplicationSamples;

namespace Credere.Modules.Origination.Tests.Domain;

public sealed class LoanApplicationAcceptTermsTests
{
    [Fact]
    public void AcceptTerms_CurrentVersion_AcceptsTheApprovedTerms()
    {
        var application = InState(LoanApplicationState.Approved);
        var command = AcceptCommand(1);
        var versionBefore = application.Version;

        var result = application.AcceptTerms(command, Later);

        result.IsSuccess.Should().BeTrue();
        application.State.Should().Be(LoanApplicationState.AcceptedByBorrower);
        application.Version.Should().Be(versionBefore + 1);
        var accepted = application.UncommittedEvents[^1].Should().BeOfType<ApplicationAccepted>().Subject;
        accepted.ApplicationId.Should().Be(SampleApplicationId);
        accepted.OccurredAt.Should().Be(Later);
        accepted.CommandId.Should().Be(command.CommandId);
        accepted.CorrelationId.Should().Be(command.CorrelationId);
        accepted.Product.Should().Be(application.Product);
        accepted.TermsVersion.Should().Be(1);
        accepted.AcceptedTerms.Should().Be(FinalTerms());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    public void AcceptTerms_OtherVersion_ReturnsStaleTermsVersionAndChangesNothing(int version)
    {
        var application = InState(LoanApplicationState.Approved);
        var eventsBefore = application.UncommittedEvents.Count;

        var result = application.AcceptTerms(AcceptCommand(version), Later);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(new StaleTermsVersion(Expected: 1, Actual: version));
        application.State.Should().Be(LoanApplicationState.Approved);
        application.UncommittedEvents.Should().HaveCount(eventsBefore);
    }

    [Fact]
    public void AcceptTerms_BeforeAnyApproval_ReturnsInvalidStateTransitionNotStale()
    {
        // TermsVersion is 0 on a submitted application, so version 0 matches. The state is what is wrong.
        var application = Submitted();

        var result = application.AcceptTerms(AcceptCommand(0), Later);

        var error = result.Error.Should().BeOfType<InvalidStateTransition>().Subject;
        error.FromState.Should().Be("Submitted");
        error.Command.Should().Be("AcceptTerms");
    }

    [Fact]
    public void AcceptTerms_Twice_ReturnsInvalidStateTransitionAndRaisesOneAcceptedEvent()
    {
        var application = InState(LoanApplicationState.Approved);
        application.AcceptTerms(AcceptCommand(1), Later).IsSuccess.Should().BeTrue();

        var second = application.AcceptTerms(AcceptCommand(1), Later);

        second.Error.Should().BeOfType<InvalidStateTransition>().Which.FromState.Should().Be("AcceptedByBorrower");
        application.UncommittedEvents.OfType<ApplicationAccepted>().Should().HaveCount(1);
    }

    [Fact]
    public void AcceptTerms_CommandForAnotherApplication_Throws()
    {
        var application = InState(LoanApplicationState.Approved);
        var other = AcceptCommand(1) with { Id = new LoanApplicationId(Guid.NewGuid()) };

        var act = () => application.AcceptTerms(other, Later);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AcceptTerms_NullCommand_Throws()
    {
        var act = () => InState(LoanApplicationState.Approved).AcceptTerms(null!, Later);

        act.Should().Throw<ArgumentNullException>();
    }
}
