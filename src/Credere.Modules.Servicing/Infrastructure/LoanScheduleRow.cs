using Credere.Modules.Servicing.Contracts;
using NodaTime;

namespace Credere.Modules.Servicing.Infrastructure;

/// <summary>
/// One row of the amortization schedule snapshot taken at disbursement. M2 does not regenerate it after a prepayment,
/// so it is written once. The loan's currency applies to every amount.
/// </summary>
internal sealed class LoanScheduleRow
{
    /// <summary>Gets or sets the loan the schedule belongs to.</summary>
    public LoanId LoanId { get; set; }

    /// <summary>Gets or sets the position of the row in the schedule, as the generator numbers it.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the payment date.</summary>
    public LocalDate PaymentDate { get; set; }

    /// <summary>Gets or sets the currency of the amounts.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the balance at the start of the period.</summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>Gets or sets the interest capitalised into the balance.</summary>
    public decimal CapitalisedInterest { get; set; }

    /// <summary>Gets or sets the interest due.</summary>
    public decimal InterestDue { get; set; }

    /// <summary>Gets or sets the principal due.</summary>
    public decimal PrincipalDue { get; set; }

    /// <summary>Gets or sets the total payment due.</summary>
    public decimal TotalPayment { get; set; }

    /// <summary>Gets or sets the balance at the end of the period.</summary>
    public decimal ClosingBalance { get; set; }
}
