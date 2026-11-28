using Credere.Core.Results;
using Credere.Modules.Origination.Application.Commands.AcceptTerms;
using Credere.Modules.Origination.Application.Commands.DecideApplication;
using Credere.Modules.Origination.Application.Commands.SubmitApplication;
using Credere.Modules.Origination.Application.Commands.WithdrawApplication;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain;
using NodaTime;
using static Credere.Modules.Origination.Tests.Domain.ApplicationSamples;

namespace Credere.Modules.Origination.Tests.Infrastructure;

/// <summary>
/// Builders for persistence tests. Every application gets its own id and product code, so a test sees only its own rows
/// in the database the whole collection shares.
/// </summary>
internal static class PersistenceSamples
{
    internal static LoanApplicationId NewId() => new(Guid.NewGuid());

    internal static ProductCode NewProduct() =>
        ProductCode.Create("T" + Guid.NewGuid().ToString("N")[..20].ToUpperInvariant()).Value;

    internal static LoanApplication NewSubmitted(
        LoanApplicationId id,
        ProductCode? product = null,
        Instant? at = null,
        RequestedTerms? terms = null,
        ApplicantSnapshot? applicant = null)
    {
        var command = new SubmitApplication(
            Guid.NewGuid(),
            applicant ?? Applicant(),
            product ?? NewProduct(),
            terms ?? RequestedTerms(),
            CorrelationId);
        return LoanApplication.Submit(command, id, at ?? SubmittedAt).Value;
    }

    internal static void Approve(LoanApplication application, DecidedTerms? terms = null, Instant? at = null) =>
        Succeed(application.Decide(
            new DecideApplication(
                Guid.NewGuid(), application.Id, DecisionOutcome.Approve, "Meets the underwriting rules.", terms ?? FinalTerms(), CorrelationId),
            at ?? Later));

    internal static void Decline(LoanApplication application) =>
        Succeed(application.Decide(
            new DecideApplication(
                Guid.NewGuid(), application.Id, DecisionOutcome.Decline, "Debt payments are too high.", null, CorrelationId),
            Later));

    internal static void Accept(LoanApplication application) =>
        Succeed(application.AcceptTerms(
            new AcceptTerms(Guid.NewGuid(), application.Id, application.TermsVersion, CorrelationId),
            Later));

    internal static void Withdraw(LoanApplication application) =>
        Succeed(application.Withdraw(
            new WithdrawApplication(Guid.NewGuid(), application.Id, "The applicant changed their mind.", CorrelationId),
            Later));

    private static void Succeed(Result<Unit, DomainError> result)
    {
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"The test setup failed: {result.Error.Message}");
        }
    }
}
