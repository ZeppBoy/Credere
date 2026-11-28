using Credere.Core.Schedules;
using Credere.Modules.Servicing.Contracts;
using NodaTime;

namespace Credere.Modules.Servicing.Infrastructure;

/// <summary>
/// The current-state row of one loan. It is a projection of the aggregate that queries read directly and that carries
/// the <see cref="RowVersion"/> for optimistic concurrency. The history table holds the source of truth (ADR-0004).
/// </summary>
/// <remarks>
/// A loan has one currency, so every amount is stored as exact text next to a single <see cref="Currency"/>
/// (<see cref="Converters.DecimalTextConverter"/>).
/// </remarks>
internal sealed class LoanRow
{
    /// <summary>Gets or sets the loan id.</summary>
    public LoanId Id { get; set; }

    /// <summary>Gets or sets the disbursement intent the loan was created from. Unique: an intent produces one loan.</summary>
    public DisbursementIntentId DisbursementIntentId { get; set; }

    /// <summary>Gets or sets the opaque reference of the accepted application.</summary>
    public Guid SourceReference { get; set; }

    /// <summary>Gets or sets the accepted terms version.</summary>
    public int TermsVersion { get; set; }

    /// <summary>Gets or sets the product key.</summary>
    public string Product { get; set; } = string.Empty;

    /// <summary>Gets or sets the currency of every amount on the row.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the principal at disbursement.</summary>
    public decimal Principal { get; set; }

    /// <summary>Gets or sets the principal still owed.</summary>
    public decimal RemainingPrincipal { get; set; }

    /// <summary>Gets or sets the fees still owed.</summary>
    public decimal FeesOutstanding { get; set; }

    /// <summary>Gets or sets the past-due interest still owed.</summary>
    public decimal PastDueInterest { get; set; }

    /// <summary>Gets or sets the current interest still owed.</summary>
    public decimal CurrentInterest { get; set; }

    /// <summary>Gets or sets the nominal rate in percent.</summary>
    public decimal NominalRate { get; set; }

    /// <summary>Gets or sets the name of the day-count convention.</summary>
    public string DayCountConvention { get; set; } = string.Empty;

    /// <summary>Gets or sets the term in months.</summary>
    public int TermMonths { get; set; }

    /// <summary>Gets or sets the payment frequency.</summary>
    public PaymentFrequency Frequency { get; set; }

    /// <summary>Gets or sets the repayment type.</summary>
    public RepaymentType Repayment { get; set; }

    /// <summary>Gets or sets the days past due that makes a loan past due.</summary>
    public int PastDueThreshold { get; set; }

    /// <summary>Gets or sets the days past due that makes a loan defaulted.</summary>
    public int DefaultThreshold { get; set; }

    /// <summary>Gets or sets the name of the business calendar.</summary>
    public string Calendar { get; set; } = string.Empty;

    /// <summary>Gets or sets the disbursement date.</summary>
    public LocalDate DisbursementDate { get; set; }

    /// <summary>Gets or sets the date of the first scheduled payment.</summary>
    public LocalDate FirstPaymentDate { get; set; }

    /// <summary>Gets or sets the date interest is posted through.</summary>
    public LocalDate AccruedThroughDate { get; set; }

    /// <summary>Gets or sets the start of the open accrual segment.</summary>
    public LocalDate AccrualSegmentStart { get; set; }

    /// <summary>Gets or sets the lifecycle state.</summary>
    public LoanState State { get; set; }

    /// <summary>Gets or sets the days past due, or <see langword="null"/> before the loan is aged.</summary>
    public int? DaysPastDue { get; set; }

    /// <summary>Gets or sets the stream version of the latest history row, the count of events applied.</summary>
    public long StreamVersion { get; set; }

    /// <summary>Gets or sets the concurrency token the database maintains.</summary>
    public byte[] RowVersion { get; set; } = [];
}
