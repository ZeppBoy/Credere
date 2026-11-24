using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using Credere.Core.DayCounts;
using Credere.Core.Results;
using Credere.Core.ValueObjects;
using NodaTime.Text;

namespace Credere.Core.Schedules;

/// <summary>
/// A validated amortization schedule: the rows of a loan from disbursement to a closing balance of exactly zero
/// (docs/domain/schedules.md, "Public contract"). Create one with <see cref="Create"/>.
/// </summary>
/// <remarks>
/// It holds a collection, and record equality compares collections by reference, so equality is implemented here:
/// two schedules are equal when their rows are equal one by one, in order (AGENTS.md §6).
/// </remarks>
public sealed record AmortizationSchedule
{
    private AmortizationSchedule(ReadOnlyCollection<ScheduleRow> rows)
    {
        Rows = rows;
    }

    /// <summary>Gets the rows, in payment order. The list cannot be changed.</summary>
    public IReadOnlyList<ScheduleRow> Rows { get; }

    /// <summary>Gets the principal: the opening balance of the first row.</summary>
    public Money Principal => Rows[0].OpeningBalance;

    /// <summary>Creates a schedule, checking every invariant.</summary>
    /// <param name="principal">The principal the schedule repays.</param>
    /// <param name="rows">The rows, in payment order. The list is copied.</param>
    /// <returns>
    /// The schedule, or a <see cref="ValidationError"/> identifying the first invariant violation.
    /// Early principal repayment is reported on <c>principal</c>. Other violations are reported on <c>rows</c>.
    /// The invariants are:
    /// <list type="bullet">
    /// <item><description>there is at least one row, and every amount is in the currency of <paramref name="principal"/>;</description></item>
    /// <item><description>the first opening balance equals the principal;</description></item>
    /// <item><description>the last closing balance is exactly zero;</description></item>
    /// <item><description>in each row before maturity, principal due is strictly less than the opening balance;</description></item>
    /// <item><description>opening balance plus capitalised interest minus principal due equals the closing balance;</description></item>
    /// <item><description>interest due plus principal due equals the total payment;</description></item>
    /// <item><description>each closing balance equals the next opening balance;</description></item>
    /// <item><description>the payment dates strictly increase.</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>, which is a programmer error.</exception>
    public static Result<AmortizationSchedule, DomainError> Create(Money principal, IReadOnlyList<ScheduleRow> rows)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(rows);

        var copy = rows.ToArray();
        var violation = FindViolation(principal, copy);

        return violation is null
            ? Result<AmortizationSchedule, DomainError>.Success(new AmortizationSchedule(Array.AsReadOnly(copy)))
            : Result<AmortizationSchedule, DomainError>.Failure(violation);
    }

    /// <inheritdoc />
    public bool Equals(AmortizationSchedule? other) =>
        other is not null && (ReferenceEquals(this, other) || Rows.SequenceEqual(other.Rows));

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var row in Rows)
        {
            hash.Add(row);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"{Rows.Count.ToString(CultureInfo.InvariantCulture)} rows, principal {Principal}, " +
        $"{LocalDatePattern.Iso.Format(Rows[0].PaymentDate)} to {LocalDatePattern.Iso.Format(Rows[^1].PaymentDate)}";

    private static ValidationError? FindViolation(Money principal, ScheduleRow[] rows)
    {
        if (rows.Length == 0)
        {
            return new ValidationError("rows", "must contain at least one row");
        }

        var zero = Money.Zero(principal.Currency);

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var number = (i + 1).ToString(CultureInfo.InvariantCulture);

            if (row is null)
            {
                return new ValidationError("rows", $"row {number} is null");
            }

            if (!AllInCurrency(row, principal.Currency))
            {
                return new ValidationError("rows", $"row {number} has an amount that is not in {principal.Currency}");
            }

            if (i == 0 && row.OpeningBalance != principal)
            {
                return new ValidationError("rows", "the opening balance of row 1 must equal the principal");
            }

            if (i < rows.Length - 1 && row.PrincipalDue >= row.OpeningBalance)
            {
                return new ValidationError("principal", $"row {number}: principal due must be less than the opening balance before maturity");
            }

            var principalDue = Cents(row.PrincipalDue);
            if (Cents(row.OpeningBalance) + Cents(row.CapitalisedInterest) - principalDue != Cents(row.ClosingBalance))
            {
                return new ValidationError("rows", $"row {number}: the opening balance plus capitalised interest minus the principal due must equal the closing balance");
            }

            if (Cents(row.InterestDue) + principalDue != Cents(row.TotalPayment))
            {
                return new ValidationError("rows", $"row {number}: the interest due plus the principal due must equal the total payment");
            }

            if (i > 0 && rows[i - 1].ClosingBalance != row.OpeningBalance)
            {
                return new ValidationError("rows", $"row {number}: the opening balance must equal the closing balance of row {i.ToString(CultureInfo.InvariantCulture)}");
            }

            if (i > 0 && row.PaymentDate <= rows[i - 1].PaymentDate)
            {
                return new ValidationError("rows", $"row {number}: the payment date must be after the date of row {i.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        return rows[^1].ClosingBalance != zero
            ? new ValidationError("rows", "the closing balance of the last row must be exactly zero")
            : null;
    }

    // The row equations are checked on exact integer cents. Money arithmetic rounds to 28 digits and throws on
    // overflow near decimal.MaxValue, so it could accept an unequal pair or throw instead of returning an error.
    private static BigInteger Cents(Money money)
    {
        const int centsPerUnit = 100;

        // A decimal is a coefficient over a power of ten, and an amount has at most two places.
        var exact = ExactYearFraction.FromDecimal(money.Amount);

        return exact.Numerator * centsPerUnit / exact.Denominator;
    }

    private static bool AllInCurrency(ScheduleRow row, string currency) =>
        row.OpeningBalance.Currency == currency
        && row.CapitalisedInterest.Currency == currency
        && row.InterestDue.Currency == currency
        && row.PrincipalDue.Currency == currency
        && row.TotalPayment.Currency == currency
        && row.ClosingBalance.Currency == currency;
}
