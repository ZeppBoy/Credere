using Credere.Modules.Servicing.Contracts;
using NodaTime;

namespace Credere.Modules.Servicing.Infrastructure;

/// <summary>One recorded payment or payoff, with its allocation through the waterfall and the excess.</summary>
internal sealed class PaymentLedgerRow
{
    /// <summary>Gets or sets the payment id, which is the key.</summary>
    public PaymentId PaymentId { get; set; }

    /// <summary>Gets or sets the loan the payment was applied to.</summary>
    public LoanId LoanId { get; set; }

    /// <summary>Gets or sets the date the payment takes effect on.</summary>
    public LocalDate EffectiveDate { get; set; }

    /// <summary>Gets or sets the currency of the amounts.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount paid.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the part applied to fees.</summary>
    public decimal FeesAllocated { get; set; }

    /// <summary>Gets or sets the part applied to past-due interest.</summary>
    public decimal PastDueInterestAllocated { get; set; }

    /// <summary>Gets or sets the part applied to current interest.</summary>
    public decimal CurrentInterestAllocated { get; set; }

    /// <summary>Gets or sets the part applied to principal.</summary>
    public decimal PrincipalAllocated { get; set; }

    /// <summary>Gets or sets the part no balance could take, recorded for return.</summary>
    public decimal Excess { get; set; }

    /// <summary>Gets or sets the id of the command that recorded the payment.</summary>
    public Guid CommandId { get; set; }

    /// <summary>Gets or sets when the row was written.</summary>
    public Instant RecordedAt { get; set; }
}
