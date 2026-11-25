using Credere.Core.Rates;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Application.Commands.AccrueInterest;
using NodaTime;

namespace Credere.Modules.Servicing.Domain;

/// <summary>
/// Projects interest balances and a ledger posting under D5 (ADR-0011 and docs/modules/servicing.md,
/// "Interest accrual and posting", reference cases IA-01 through IA-15).
/// </summary>
internal static class InterestAccrual
{
    /// <summary>
    /// The posted date and outstanding interest balances. The open segment starts at disbursement
    /// and restarts at each scheduled payment date or effective principal reduction.
    /// </summary>
    internal sealed record Position(
        LocalDate AccruedThroughDate,
        LocalDate SegmentStart,
        Money CurrentInterest,
        Money PastDueInterest);

    /// <summary>The projected balances and the additional interest to post.</summary>
    internal sealed record Projection(Position Position, AccrualResult Posting);

    /// <summary>
    /// Projects accrual to <paramref name="businessDate"/> with no intervening payment.
    /// The principal is the balance at the posted date and has held since the open segment started.
    /// </summary>
    internal static Projection Project(
        Position position,
        LocalDate businessDate,
        Money principal,
        FixedRate rate,
        AmortizationSchedule schedule,
        IInterestCalculator calculator)
    {
        var zero = Money.Zero(principal.Currency);
        if (businessDate <= position.AccruedThroughDate)
        {
            return new Projection(position, new AccrualResult(position.AccruedThroughDate, zero, Changed: false));
        }

        var accruedThrough = position.AccruedThroughDate;
        var segmentStart = position.SegmentStart;
        var pastDueInterest = position.PastDueInterest;
        var currentInterest = position.CurrentInterest;
        var posted = zero;

        foreach (var row in schedule.Rows)
        {
            var paymentDate = row.PaymentDate;
            if (paymentDate < accruedThrough)
            {
                continue;
            }

            if (paymentDate > businessDate)
            {
                break;
            }

            var additionalInterest = SegmentIncrease(calculator, rate, principal, segmentStart, accruedThrough, paymentDate);
            posted = AddExactly(posted, additionalInterest);
            currentInterest = AddExactly(currentInterest, additionalInterest);
            segmentStart = paymentDate;
            accruedThrough = paymentDate;

            if (paymentDate < businessDate)
            {
                pastDueInterest = AddExactly(pastDueInterest, currentInterest);
                currentInterest = zero;
            }
        }

        var trailingInterest = SegmentIncrease(calculator, rate, principal, segmentStart, accruedThrough, businessDate);
        var nextPosition = new Position(
            businessDate, segmentStart, AddExactly(currentInterest, trailingInterest), pastDueInterest);
        var posting = new AccrualResult(businessDate, AddExactly(posted, trailingInterest), Changed: true);

        return new Projection(nextPosition, posting);
    }

    internal static Money AddExactly(Money left, Money right)
    {
        var sum = left + right;
        RequireFraction(sum, (left.Amount % 1m + right.Amount % 1m) % 1m);
        return sum;
    }

    private static Money SegmentIncrease(
        IInterestCalculator calculator,
        FixedRate rate,
        Money principal,
        LocalDate segmentStart,
        LocalDate accruedThrough,
        LocalDate end)
    {
        if (end == accruedThrough)
        {
            return Money.Zero(principal.Currency);
        }

        // The completed segments contribute equally to both cumulative values and cancel out.
        // For the segment in progress, subtract its already-rounded posted portion.
        var total = calculator.Calculate(rate, principal, segmentStart, end);
        if (segmentStart == accruedThrough)
        {
            return total;
        }

        var previous = calculator.Calculate(rate, principal, segmentStart, accruedThrough);
        var increase = total - previous;
        RequireFraction(increase, (total.Amount % 1m - previous.Amount % 1m + 1m) % 1m);
        return increase;
    }

    private static void RequireFraction(Money amount, decimal expectedFraction)
    {
        // A decimal near its precision limit can drop cents when added or subtracted.
        if (amount.Amount % 1m != expectedFraction)
        {
            throw new OverflowException("The servicing amount cannot be represented exactly.");
        }
    }
}
