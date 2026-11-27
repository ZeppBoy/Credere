using Credere.Modules.Origination.Contracts;
using NodaTime;

namespace Credere.Modules.Origination.Infrastructure;

/// <summary>
/// The current-state row of one application. It is a projection of the aggregate that queries read directly and that
/// carries the <see cref="RowVersion"/> for optimistic concurrency. The history table holds the source of truth
/// (ADR-0004).
/// </summary>
/// <remarks>
/// Each <c>Money</c> is a decimal amount and a currency column. Amounts are <c>decimal(38,2)</c> and the nominal rate
/// is <c>decimal(38,28)</c>, wide enough that no value the domain accepts is rounded on its way to the table.
/// </remarks>
internal sealed class LoanApplicationRow
{
    /// <summary>Gets or sets the application id.</summary>
    public LoanApplicationId Id { get; set; }

    /// <summary>Gets or sets the applicant id.</summary>
    public Guid ApplicantId { get; set; }

    /// <summary>Gets or sets the applicant's full name. Personal data.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Gets or sets the applicant's date of birth. Personal data.</summary>
    public LocalDate DateOfBirth { get; set; }

    /// <summary>Gets or sets the ISO 3166-1 alpha-2 country code.</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the monthly income amount.</summary>
    public decimal MonthlyIncomeAmount { get; set; }

    /// <summary>Gets or sets the monthly income currency.</summary>
    public string MonthlyIncomeCurrency { get; set; } = string.Empty;

    /// <summary>Gets or sets the monthly debt payments amount.</summary>
    public decimal MonthlyDebtPaymentsAmount { get; set; }

    /// <summary>Gets or sets the monthly debt payments currency.</summary>
    public string MonthlyDebtPaymentsCurrency { get; set; } = string.Empty;

    /// <summary>Gets or sets the product code.</summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the requested principal amount.</summary>
    public decimal RequestedPrincipalAmount { get; set; }

    /// <summary>Gets or sets the requested principal currency.</summary>
    public string RequestedPrincipalCurrency { get; set; } = string.Empty;

    /// <summary>Gets or sets the requested term in months.</summary>
    public int RequestedTermMonths { get; set; }

    /// <summary>Gets or sets when the application was submitted.</summary>
    public Instant SubmittedAt { get; set; }

    /// <summary>Gets or sets the lifecycle state.</summary>
    public LoanApplicationState State { get; set; }

    /// <summary>Gets or sets the version of the decided terms.</summary>
    public int TermsVersion { get; set; }

    /// <summary>Gets or sets the decided principal amount, or <see langword="null"/> before an approval.</summary>
    public decimal? DecidedPrincipalAmount { get; set; }

    /// <summary>Gets or sets the decided principal currency, or <see langword="null"/> before an approval.</summary>
    public string? DecidedPrincipalCurrency { get; set; }

    /// <summary>Gets or sets the decided nominal rate in percent, or <see langword="null"/> before an approval.</summary>
    public decimal? DecidedNominalRate { get; set; }

    /// <summary>Gets or sets the decided term in months, or <see langword="null"/> before an approval.</summary>
    public int? DecidedTermMonths { get; set; }

    /// <summary>Gets or sets the outcome of the latest decision, or <see langword="null"/> before the first one.</summary>
    public DecisionOutcome? LastOutcome { get; set; }

    /// <summary>Gets or sets the rationale of the latest decision.</summary>
    public string? LastRationale { get; set; }

    /// <summary>Gets or sets when the latest decision was made.</summary>
    public Instant? LastDecidedAt { get; set; }

    /// <summary>Gets or sets the stream version of the latest history row, the count of events applied.</summary>
    public int StreamVersion { get; set; }

    /// <summary>Gets or sets the concurrency token the database maintains.</summary>
    public byte[] RowVersion { get; set; } = [];
}
