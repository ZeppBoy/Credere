using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using NodaTime;

namespace Credere.Modules.Servicing.Domain;

/// <summary>
/// Calculates and checks a dated payoff under D5 (ADR-0011 and docs/modules/servicing.md,
/// "Payoff quotes", reference cases PO-01 through PO-11).
/// </summary>
internal static class PayoffCalculation
{
    /// <summary>The outstanding balances and the amount needed to close them.</summary>
    internal sealed record Amounts(Money Principal, Money PastDueInterest, Money CurrentInterest, Money Fees, Money Total);

    /// <summary>Quotes all balances outstanding on the payoff date, assuming no intervening payment.</summary>
    internal static Result<Amounts, DomainError> Quote(
        LocalDate payoffDate,
        Money principal,
        Money feesOutstanding,
        InterestAccrual.Position position,
        FixedRate rate,
        AmortizationSchedule schedule,
        IInterestCalculator calculator)
    {
        if (payoffDate < position.AccruedThroughDate)
        {
            return Result<Amounts, DomainError>.Failure(
                new ValidationError(nameof(payoffDate), "must be on or after the posted accrual date"));
        }

        var projected = InterestAccrual.Project(position, payoffDate, principal, rate, schedule, calculator).Position;
        var outstandingInterest = InterestAccrual.AddExactly(projected.PastDueInterest, projected.CurrentInterest);
        var total = InterestAccrual.AddExactly(InterestAccrual.AddExactly(principal, outstandingInterest), feesOutstanding);

        return Result<Amounts, DomainError>.Success(new Amounts(
            principal, projected.PastDueInterest, projected.CurrentInterest, feesOutstanding, total));
    }

    /// <summary>Checks the original total against a quote recomputed from the current state for the same date.</summary>
    internal static Result<Unit, DomainError> ValidateQuotedTotal(Money recomputedTotal, Money quotedTotal) =>
        recomputedTotal == quotedTotal
            ? Result<Unit, DomainError>.Success(Unit.Value)
            : Result<Unit, DomainError>.Failure(new PayoffQuoteStale(quotedTotal, recomputedTotal));
}
