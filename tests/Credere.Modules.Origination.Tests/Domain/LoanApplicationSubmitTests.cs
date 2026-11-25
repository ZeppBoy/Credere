using Credere.Core.Results;
using Credere.Core.ValueObjects;
using Credere.Modules.Origination.Application.Commands.SubmitApplication;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain;
using Credere.Modules.Origination.Domain.Events;
using FluentAssertions;
using NodaTime;
using Xunit;
using static Credere.Modules.Origination.Tests.Domain.ApplicationSamples;

namespace Credere.Modules.Origination.Tests.Domain;

public sealed class LoanApplicationSubmitTests
{
    [Fact]
    public void Submit_ValidCommand_StartsASubmittedApplication()
    {
        var command = SubmitCommand();

        var result = LoanApplication.Submit(command, SampleApplicationId, SubmittedAt);

        result.IsSuccess.Should().BeTrue();
        var application = result.Value;
        application.Id.Should().Be(SampleApplicationId);
        application.State.Should().Be(LoanApplicationState.Submitted);
        application.Applicant.Should().Be(command.Applicant);
        application.Product.Should().Be(command.Product);
        application.Terms.Should().Be(command.Terms);
        application.SubmittedAt.Should().Be(SubmittedAt);
        application.TermsVersion.Should().Be(0);
        application.DecidedTerms.Should().BeNull();
        application.Decision.Should().BeNull();
        application.Version.Should().Be(1);
    }

    [Fact]
    public void Submit_ValidCommand_RaisesOneSubmittedEventWithEverythingConsumersNeed()
    {
        var command = SubmitCommand();

        var application = LoanApplication.Submit(command, SampleApplicationId, SubmittedAt).Value;

        application.UncommittedEvents.Should().HaveCount(1);
        var submitted = application.UncommittedEvents[0].Should().BeOfType<ApplicationSubmitted>().Subject;
        submitted.ApplicationId.Should().Be(SampleApplicationId);
        submitted.OccurredAt.Should().Be(SubmittedAt);
        submitted.CommandId.Should().Be(command.CommandId);
        submitted.CorrelationId.Should().Be(command.CorrelationId);
        submitted.Applicant.Should().Be(command.Applicant);
        submitted.Product.Should().Be(command.Product);
        submitted.Terms.Should().Be(command.Terms);
    }

    [Fact]
    public void Submit_NullCommand_Throws()
    {
        var act = () => LoanApplication.Submit(null!, SampleApplicationId, SubmittedAt);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Submit_ZeroIncomeAndZeroDebt_IsValid()
    {
        var applicant = Applicant() with { MonthlyIncome = Eur(0m), MonthlyDebtPayments = Eur(0m) };

        var result = LoanApplication.Submit(SubmitCommand(applicant), SampleApplicationId, SubmittedAt);

        result.IsSuccess.Should().BeTrue();
    }

    public static TheoryData<string, SubmitApplication> InvalidSubmissions() => new()
    {
        { "ApplicantId", SubmitCommand(Applicant() with { ApplicantId = Guid.Empty }) },
        { "FullName", SubmitCommand(Applicant() with { FullName = string.Empty }) },
        { "FullName", SubmitCommand(Applicant() with { FullName = "   " }) },
        { "CountryCode", SubmitCommand(Applicant() with { CountryCode = string.Empty }) },
        { "CountryCode", SubmitCommand(Applicant() with { CountryCode = "D" }) },
        { "CountryCode", SubmitCommand(Applicant() with { CountryCode = "DEU" }) },
        { "CountryCode", SubmitCommand(Applicant() with { CountryCode = "de" }) },
        { "CountryCode", SubmitCommand(Applicant() with { CountryCode = "D1" }) },
        { "CountryCode", SubmitCommand(Applicant() with { CountryCode = " DE" }) },
        { "CountryCode", SubmitCommand(Applicant() with { CountryCode = "XX" }) },
        { "CountryCode", SubmitCommand(Applicant() with { CountryCode = "ZZ" }) },
        { "CountryCode", SubmitCommand(Applicant() with { CountryCode = "XK" }) },
        { "CountryCode", SubmitCommand(Applicant() with { CountryCode = "UK" }) },
        { "CountryCode", SubmitCommand(Applicant() with { CountryCode = "EU" }) },
        { "CountryCode", SubmitCommand(Applicant() with { CountryCode = "AN" }) },
        { "MonthlyIncome", SubmitCommand(Applicant() with { MonthlyIncome = Eur(-0.01m) }) },
        { "MonthlyDebtPayments", SubmitCommand(Applicant() with { MonthlyDebtPayments = Eur(-0.01m) }) },
        {
            "MonthlyDebtPayments",
            SubmitCommand(Applicant() with { MonthlyDebtPayments = Money.Create(100m, "USD").Value })
        },
        { "RequestedTerms.Principal", SubmitCommand(terms: RequestedTerms() with { Principal = Eur(0m) }) },
        { "RequestedTerms.Principal", SubmitCommand(terms: RequestedTerms() with { Principal = Eur(-1m) }) },
    };

    [Theory]
    [MemberData(nameof(InvalidSubmissions))]
    public void Submit_InvalidField_ReturnsValidationErrorNamingTheField(string field, SubmitApplication command)
    {
        var result = LoanApplication.Submit(command, SampleApplicationId, SubmittedAt);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be(field);
    }

    [Fact]
    public void Submit_PrincipalOfOneCent_IsValid()
    {
        var terms = new RequestedTerms(Eur(0.01m), LoanTerm.FromMonths(1));

        var result = LoanApplication.Submit(SubmitCommand(terms: terms), SampleApplicationId, SubmittedAt);

        result.IsSuccess.Should().BeTrue();
    }
}
