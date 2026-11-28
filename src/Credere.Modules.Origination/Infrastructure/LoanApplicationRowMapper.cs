using Credere.Core.ValueObjects;
using Credere.Modules.Origination.Application.Queries.GetApplication;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain;

namespace Credere.Modules.Origination.Infrastructure;

/// <summary>Copies an aggregate onto its current-state row, and a row onto the public <see cref="ApplicationView"/>.</summary>
internal static class LoanApplicationRowMapper
{
    /// <summary>Writes the aggregate's state onto <paramref name="row"/>, including its stream version.</summary>
    /// <param name="row">The row to fill. A tracked row keeps its concurrency token.</param>
    /// <param name="application">The aggregate.</param>
    internal static void Apply(LoanApplicationRow row, LoanApplication application)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(application);

        var applicant = application.Applicant;
        row.Id = application.Id;
        row.ApplicantId = applicant.ApplicantId;
        row.FullName = applicant.FullName;
        row.DateOfBirth = applicant.DateOfBirth;
        row.CountryCode = applicant.CountryCode;
        row.MonthlyIncomeAmount = applicant.MonthlyIncome.Amount;
        row.MonthlyIncomeCurrency = applicant.MonthlyIncome.Currency;
        row.MonthlyDebtPaymentsAmount = applicant.MonthlyDebtPayments.Amount;
        row.MonthlyDebtPaymentsCurrency = applicant.MonthlyDebtPayments.Currency;
        row.ProductCode = application.Product.Value;
        row.RequestedPrincipalAmount = application.Terms.Principal.Amount;
        row.RequestedPrincipalCurrency = application.Terms.Principal.Currency;
        row.RequestedTermMonths = application.Terms.Term.Months;
        row.SubmittedAt = application.SubmittedAt;
        row.State = application.State;
        row.TermsVersion = application.TermsVersion;

        var decided = application.DecidedTerms;
        row.DecidedPrincipalAmount = decided?.Principal.Amount;
        row.DecidedPrincipalCurrency = decided?.Principal.Currency;
        row.DecidedNominalRate = decided?.NominalRate.Value;
        row.DecidedTermMonths = decided?.Term.Months;

        var decision = application.Decision;
        row.LastOutcome = decision?.Outcome;
        row.LastRationale = decision?.Rationale;
        row.LastDecidedAt = decision?.DecidedAt;
        row.StreamVersion = application.Version;
    }

    /// <summary>Builds the public view of a row.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The view.</returns>
    /// <exception cref="InvalidOperationException">The row holds a value the domain would not have stored.</exception>
    internal static ApplicationView ToView(LoanApplicationRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var applicant = new ApplicantSnapshot(
            row.ApplicantId,
            row.FullName,
            row.DateOfBirth,
            row.CountryCode,
            ToMoney(row.MonthlyIncomeAmount, row.MonthlyIncomeCurrency),
            ToMoney(row.MonthlyDebtPaymentsAmount, row.MonthlyDebtPaymentsCurrency));
        var requested = new RequestedTerms(
            ToMoney(row.RequestedPrincipalAmount, row.RequestedPrincipalCurrency),
            LoanTerm.Create(row.RequestedTermMonths).Value);

        DecidedTerms? decided = null;
        if (row.DecidedPrincipalAmount is { } principal
            && row.DecidedPrincipalCurrency is { } currency
            && row.DecidedNominalRate is { } rate
            && row.DecidedTermMonths is { } months)
        {
            decided = new DecidedTerms(
                ToMoney(principal, currency),
                Percentage.Create(rate).Value,
                LoanTerm.Create(months).Value);
        }

        return new ApplicationView(
            row.Id,
            applicant,
            ProductCode.Create(row.ProductCode).Value,
            requested,
            decided,
            row.TermsVersion,
            row.State,
            row.LastOutcome,
            row.LastRationale,
            row.SubmittedAt);
    }

    private static Money ToMoney(decimal amount, string currency) => Money.Create(amount, currency).Value;
}
