using Credere.Core.ValueObjects;
using Credere.Modules.Origination.Application.Queries.GetApplication;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Infrastructure;
using FluentAssertions;
using Xunit;
using static Credere.Modules.Origination.Tests.Domain.ApplicationSamples;

namespace Credere.Modules.Origination.Tests.Infrastructure;

public sealed class LoanApplicationRowMapperTests
{
    [Fact]
    public void ToView_SubmittedApplication_HasNoDecidedTermsAndNoDecision()
    {
        var application = InState(LoanApplicationState.Submitted);
        var row = new LoanApplicationRow();

        LoanApplicationRowMapper.Apply(row, application);
        var view = LoanApplicationRowMapper.ToView(row);

        view.Should().Be(new ApplicationView(
            SampleApplicationId,
            Applicant(),
            Product(),
            RequestedTerms(),
            null,
            0,
            LoanApplicationState.Submitted,
            null,
            null,
            SubmittedAt));
        row.StreamVersion.Should().Be(1);
    }

    [Fact]
    public void ToView_ApprovedApplication_CarriesTheDecidedTermsAndTheLatestDecision()
    {
        var application = InState(LoanApplicationState.Approved);
        var row = new LoanApplicationRow();

        LoanApplicationRowMapper.Apply(row, application);
        var view = LoanApplicationRowMapper.ToView(row);

        view.Should().Be(new ApplicationView(
            SampleApplicationId,
            Applicant(),
            Product(),
            RequestedTerms(),
            FinalTerms(),
            1,
            LoanApplicationState.Approved,
            DecisionOutcome.Approve,
            "Meets the underwriting rules.",
            SubmittedAt));
        row.StreamVersion.Should().Be(2);
        row.LastDecidedAt.Should().Be(Later);
    }

    [Theory]
    [InlineData(LoanApplicationState.UnderReview, DecisionOutcome.HumanReview)]
    [InlineData(LoanApplicationState.Declined, DecisionOutcome.Decline)]
    public void ToView_DecisionWithoutApproval_KeepsTheOutcomeAndLeavesTermsEmpty(
        LoanApplicationState state,
        DecisionOutcome outcome)
    {
        var row = new LoanApplicationRow();

        LoanApplicationRowMapper.Apply(row, InState(state));
        var view = LoanApplicationRowMapper.ToView(row);

        view.State.Should().Be(state);
        view.LastOutcome.Should().Be(outcome);
        view.DecidedTerms.Should().BeNull();
        view.TermsVersion.Should().Be(0);
    }

    [Fact]
    public void Apply_AcceptedAndWithdrawn_KeepTheLastDecisionAndAdvanceTheStreamVersion()
    {
        var accepted = new LoanApplicationRow();
        var withdrawn = new LoanApplicationRow();

        LoanApplicationRowMapper.Apply(accepted, InState(LoanApplicationState.AcceptedByBorrower));
        LoanApplicationRowMapper.Apply(withdrawn, InState(LoanApplicationState.Withdrawn));

        accepted.StreamVersion.Should().Be(3);
        accepted.LastOutcome.Should().Be(DecisionOutcome.Approve);
        withdrawn.StreamVersion.Should().Be(2);
        withdrawn.LastOutcome.Should().BeNull();
    }

    [Fact]
    public void ToView_AmountAndRateAtTheLimitsOfTheColumns_AreNotChanged()
    {
        var row = new LoanApplicationRow();
        LoanApplicationRowMapper.Apply(row, InState(LoanApplicationState.Approved));
        row.DecidedNominalRate = 7.1234567890123456789012345678m;
        row.RequestedPrincipalAmount = 12_345_678_901_234.56m;

        var view = LoanApplicationRowMapper.ToView(row);

        view.DecidedTerms!.NominalRate.Should().Be(new Percentage(7.1234567890123456789012345678m));
        view.RequestedTerms.Principal.Should().Be(Eur(12_345_678_901_234.56m));
    }
}
