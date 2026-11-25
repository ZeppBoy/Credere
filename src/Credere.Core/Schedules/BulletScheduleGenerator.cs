using System.Numerics;
using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.ValueObjects;

namespace Credere.Core.Schedules;

/// <summary>Generates a schedule that repays the opening balance at maturity.</summary>
/// <remarks>
/// Interest-only cash flows follow the California State Board of Equalization, Assessors' Handbook §503,
/// Problem 7, p. 41. The compounding method follows §505, chapter 2, Future Worth of 1, p. 2.
/// Credere defines the payment dates, per-period rounding and maturity treatment in docs/domain/schedules.md, §3.
/// </remarks>
public sealed class BulletScheduleGenerator : IScheduleGenerator
{
    private readonly IInterestCalculator interestCalculator;
    private readonly bool capitaliseInterest;

    /// <summary>Initializes a bullet schedule generator for one repayment mode.</summary>
    /// <param name="interestCalculator">The shared calculator that rounds the interest for each period.</param>
    /// <param name="capitaliseInterest">
    /// Whether to add each period's interest to the balance before maturity. Otherwise interest is paid each period.
    /// The interest of the final period is paid in cash in both modes.
    /// </param>
    public BulletScheduleGenerator(IInterestCalculator interestCalculator, bool capitaliseInterest)
    {
        ArgumentNullException.ThrowIfNull(interestCalculator);
        this.interestCalculator = interestCalculator;
        this.capitaliseInterest = capitaliseInterest;
    }

    /// <inheritdoc />
    public Result<AmortizationSchedule, DomainError> Generate(ScheduleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Principal.Amount < 0m)
        {
            return InvalidPrincipal("must be non-negative");
        }

        var rowCount = SchedulePeriods.RowCount(request.Term, request.PaymentFrequency);
        var periods = SchedulePeriods.Periods(
            request.DisbursementDate, request.FirstPaymentDate, request.PaymentFrequency, rowCount);
        var opening = request.Principal;
        var zero = Money.Zero(opening.Currency);
        var rows = new List<ScheduleRow>(rowCount);

        try
        {
            for (var index = 0; index < rowCount; index++)
            {
                var period = periods[index];
                var interest = interestCalculator.Calculate(request.Rate, opening, period.Start, period.End);

                if (index == rowCount - 1)
                {
                    var payment = AddExactly(opening, interest);
                    rows.Add(new ScheduleRow(index + 1, period.End, opening, zero, interest, opening, payment, zero));
                }
                else if (capitaliseInterest)
                {
                    var closing = AddExactly(opening, interest);
                    rows.Add(new ScheduleRow(index + 1, period.End, opening, interest, zero, zero, zero, closing));
                    opening = closing;
                }
                else
                {
                    rows.Add(new ScheduleRow(index + 1, period.End, opening, zero, interest, zero, interest, opening));
                }
            }

            return AmortizationSchedule.Create(request.Principal, rows);
        }
        catch (OverflowException)
        {
            return InvalidPrincipal("a scheduled amount cannot be represented exactly as money");
        }
    }

    private static Money AddExactly(Money left, Money right)
    {
        var sum = left + right;
        if (ToCents(sum) != ToCents(left) + ToCents(right))
        {
            throw new OverflowException("The exact sum does not fit in a decimal amount.");
        }

        return sum;
    }

    private static BigInteger ToCents(Money money)
    {
        const int centsPerUnit = 100;
        var amount = ExactYearFraction.FromDecimal(money.Amount);
        return amount.Numerator * centsPerUnit / amount.Denominator;
    }

    private static Result<AmortizationSchedule, DomainError> InvalidPrincipal(string reason) =>
        Result<AmortizationSchedule, DomainError>.Failure(new ValidationError("principal", reason));
}
