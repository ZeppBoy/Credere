using Credere.Core.Results;
using Credere.Core.ValueObjects;
using Credere.Modules.Origination.Application.Commands.AcceptTerms;
using Credere.Modules.Origination.Application.Commands.DecideApplication;
using Credere.Modules.Origination.Application.Commands.SubmitApplication;
using Credere.Modules.Origination.Application.Commands.WithdrawApplication;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain;
using NodaTime;

namespace Credere.Modules.Origination.Tests.Domain;

/// <summary>
/// Hand-written sample data for the aggregate tests. The values are arbitrary test inputs, not financial references:
/// no test here asserts a calculated amount, only how the aggregate records and validates what it is given.
/// </summary>
internal static class ApplicationSamples
{
    internal static readonly Instant SubmittedAt = Instant.FromUtc(2026, 11, 25, 9, 0);
    internal static readonly Instant Later = Instant.FromUtc(2026, 11, 26, 10, 30);
    internal static readonly LoanApplicationId SampleApplicationId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    internal static readonly Guid CommandId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    internal static readonly Guid CorrelationId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    internal static Money Eur(decimal amount) => Money.Create(amount, "EUR").Value;

    internal static ApplicantSnapshot Applicant() => new(
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        "Test Applicant",
        new LocalDate(1985, 6, 15),
        "DE",
        Eur(4200m),
        Eur(600m));

    internal static ProductCode Product() => ProductCode.Create("TERM-FIXED").Value;

    internal static RequestedTerms RequestedTerms() => new(Eur(10_000m), LoanTerm.FromMonths(24));

    internal static DecidedTerms FinalTerms(decimal principal = 9_000m, decimal ratePercent = 7.5m) =>
        new(Eur(principal), new Percentage(ratePercent), LoanTerm.FromMonths(24));

    internal static SubmitApplication SubmitCommand(
        ApplicantSnapshot? applicant = null,
        RequestedTerms? terms = null) =>
        new(CommandId, applicant ?? Applicant(), Product(), terms ?? RequestedTerms(), CorrelationId);

    internal static DecideApplication DecideCommand(
        DecisionOutcome outcome,
        string rationale = "Meets the underwriting rules.",
        DecidedTerms? finalTerms = null) =>
        new(CommandId, SampleApplicationId, outcome, rationale, finalTerms, CorrelationId);

    internal static DecideApplication ApproveCommand(DecidedTerms? finalTerms = null) =>
        DecideCommand(DecisionOutcome.Approve, finalTerms: finalTerms ?? FinalTerms());

    internal static AcceptTerms AcceptCommand(int termsVersion) =>
        new(CommandId, SampleApplicationId, termsVersion, CorrelationId);

    internal static WithdrawApplication WithdrawCommand(string reason = "The applicant changed their mind.") =>
        new(CommandId, SampleApplicationId, reason, CorrelationId);

    /// <summary>Submits a valid application, which is in <see cref="LoanApplicationState.Submitted"/>.</summary>
    internal static LoanApplication Submitted() =>
        LoanApplication.Submit(SubmitCommand(), SampleApplicationId, SubmittedAt).Value;

    /// <summary>Builds an application in <paramref name="state"/> using only the aggregate's own commands.</summary>
    internal static LoanApplication InState(LoanApplicationState state)
    {
        var application = Submitted();
        switch (state)
        {
            case LoanApplicationState.Submitted:
                break;
            case LoanApplicationState.UnderReview:
                Succeed(application.Decide(DecideCommand(DecisionOutcome.HumanReview), Later));
                break;
            case LoanApplicationState.Approved:
                Succeed(application.Decide(ApproveCommand(), Later));
                break;
            case LoanApplicationState.AcceptedByBorrower:
                Succeed(application.Decide(ApproveCommand(), Later));
                Succeed(application.AcceptTerms(AcceptCommand(1), Later));
                break;
            case LoanApplicationState.Declined:
                Succeed(application.Decide(DecideCommand(DecisionOutcome.Decline), Later));
                break;
            case LoanApplicationState.Withdrawn:
                Succeed(application.Withdraw(WithdrawCommand(), Later));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown state.");
        }

        return application;
    }

    private static void Succeed(Result<Unit, DomainError> result)
    {
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"The test setup failed: {result.Error.Message}");
        }
    }
}
