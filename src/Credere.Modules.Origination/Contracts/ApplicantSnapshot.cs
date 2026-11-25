using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Modules.Origination.Contracts;

/// <summary>Captures the applicant information supplied for underwriting. Contains personal data.</summary>
/// <param name="ApplicantId">The applicant's reference.</param>
/// <param name="FullName">The applicant's full name.</param>
/// <param name="DateOfBirth">The applicant's date of birth.</param>
/// <param name="CountryCode">The applicant's ISO 3166-1 alpha-2 country code.</param>
/// <param name="MonthlyIncome">The applicant's monthly income.</param>
/// <param name="MonthlyDebtPayments">The applicant's monthly debt payments.</param>
public sealed record ApplicantSnapshot(
    Guid ApplicantId,
    string FullName,
    LocalDate DateOfBirth,
    string CountryCode,
    Money MonthlyIncome,
    Money MonthlyDebtPayments);
