using Credere.Modules.Servicing.Contracts;
using NodaTime;

namespace Credere.Modules.Servicing.Infrastructure;

/// <summary>
/// One accrual that changed a balance. The key is <c>(LoanId, BusinessDate)</c>, so a date is posted once whatever
/// command delivered it, and a repeated run for the same date cannot add a second row.
/// </summary>
internal sealed class AccrualLedgerRow
{
    /// <summary>Gets or sets the loan the accrual belongs to.</summary>
    public LoanId LoanId { get; set; }

    /// <summary>Gets or sets the business date interest was accrued through.</summary>
    public LocalDate BusinessDate { get; set; }

    /// <summary>Gets or sets the date the accrued period started, the previous <c>AccruedThroughDate</c>.</summary>
    public LocalDate PeriodStart { get; set; }

    /// <summary>Gets or sets the currency of the amount.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the interest posted for the period.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the id of the command whose accrual this is.</summary>
    public Guid CommandId { get; set; }

    /// <summary>Gets or sets when the row was written.</summary>
    public Instant RecordedAt { get; set; }
}
