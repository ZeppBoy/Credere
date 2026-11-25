using Credere.Core.Results;
using Credere.Core.ValueObjects;
using Credere.Modules.Origination.Contracts;

namespace Credere.Modules.Origination.Domain;

/// <summary>
/// The input rules of docs/modules/origination.md ("Public contract") that the <see cref="LoanApplication"/>
/// enforces on every command, so an invalid application can't exist whatever the caller validated first.
/// </summary>
internal static class LoanApplicationRules
{
    /// <summary>The largest nominal rate an underwriter can set, in percent.</summary>
    internal const decimal MaximumNominalRatePercent = 100m;

    private const int CountryCodeLength = 2;

    /// <summary>Checks the applicant snapshot and the requested terms of a submission.</summary>
    /// <param name="applicant">The applicant snapshot.</param>
    /// <param name="terms">The requested terms.</param>
    /// <returns>A <see cref="ValidationError"/> for the first bad field, or <see langword="null"/> when all are valid.</returns>
    internal static DomainError? ValidateSubmission(ApplicantSnapshot applicant, RequestedTerms terms)
    {
        if (applicant.ApplicantId == Guid.Empty)
        {
            return new ValidationError(nameof(applicant.ApplicantId), "must not be empty");
        }

        if (string.IsNullOrWhiteSpace(applicant.FullName))
        {
            return new ValidationError(nameof(applicant.FullName), "must not be empty");
        }

        // Structure only: docs/modules/origination.md does not name the ISO 3166-1 list the module checks against.
        if (!IsCountryCodeShape(applicant.CountryCode))
        {
            return new ValidationError(
                nameof(applicant.CountryCode),
                "must be an ISO 3166-1 alpha-2 code of two uppercase letters A-Z");
        }

        if (applicant.MonthlyIncome.Amount < 0m)
        {
            return new ValidationError(nameof(applicant.MonthlyIncome), "must not be negative");
        }

        if (applicant.MonthlyDebtPayments.Amount < 0m)
        {
            return new ValidationError(nameof(applicant.MonthlyDebtPayments), "must not be negative");
        }

        // The debt-to-income rule divides one by the other, so mixed currencies would be a programmer error later.
        if (applicant.MonthlyDebtPayments.Currency != applicant.MonthlyIncome.Currency)
        {
            return new ValidationError(
                nameof(applicant.MonthlyDebtPayments),
                "must be in the same currency as the monthly income");
        }

        return ValidatePrincipal(terms.Principal, $"{nameof(RequestedTerms)}.{nameof(RequestedTerms.Principal)}");
    }

    /// <summary>Checks the final terms of an approval.</summary>
    /// <param name="terms">The final terms.</param>
    /// <returns>A <see cref="ValidationError"/> for the first bad field, or <see langword="null"/> when all are valid.</returns>
    internal static DomainError? ValidateFinalTerms(DecidedTerms terms)
    {
        var principalError = ValidatePrincipal(terms.Principal, $"{nameof(DecidedTerms)}.{nameof(DecidedTerms.Principal)}");
        if (principalError is not null)
        {
            return principalError;
        }

        return terms.NominalRate.Value > MaximumNominalRatePercent
            ? new ValidationError(
                $"{nameof(DecidedTerms)}.{nameof(DecidedTerms.NominalRate)}",
                $"must not exceed {MaximumNominalRatePercent:0}%")
            : null;
    }

    private static ValidationError? ValidatePrincipal(Money principal, string field) =>
        principal.Amount > 0m ? null : new ValidationError(field, "must be greater than zero");

    private static bool IsCountryCodeShape(string? code) =>
        code is { Length: CountryCodeLength } && code.All(character => character is >= 'A' and <= 'Z');
}
