using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Application.Queries.QuotePayoff;
using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Domain;

/// <summary>
/// Calculates and checks a dated payoff under D5 (ADR-0011 and docs/modules/servicing.md,
/// "Payoff quotes", reference cases PO-01 through PO-11).
/// </summary>
internal static class PayoffCalculation
{
    /// <summary>Quotes all balances outstanding on the payoff date, assuming no intervening payment.</summary>
    internal static Result<PayoffQuote, DomainError> Quote(
        QuotePayoff query,
        Money principal,
        Money feesOutstanding,
        InterestAccrual.Position position,
        FixedRate rate,
        AmortizationSchedule schedule,
        IInterestCalculator calculator)
    {
        if (query.PayoffDate < position.AccruedThroughDate)
        {
            return Result<PayoffQuote, DomainError>.Failure(
                new ValidationError("payoffDate", "must be on or after the posted accrual date"));
        }

        var projected = InterestAccrual.Project(position, query.PayoffDate, principal, rate, schedule, calculator).Position;
        var outstandingInterest = InterestAccrual.AddExactly(projected.PastDueInterest, projected.CurrentInterest);
        var total = InterestAccrual.AddExactly(InterestAccrual.AddExactly(principal, outstandingInterest), feesOutstanding);

        return Result<PayoffQuote, DomainError>.Success(new PayoffQuote(
            query.Id, query.PayoffDate, principal, projected.PastDueInterest, projected.CurrentInterest, feesOutstanding, total));
    }

    /// <summary>Checks the original total against a quote recomputed from the current state for the same date.</summary>
    internal static Result<Unit, DomainError> ValidateQuotedTotal(PayoffQuote recomputedQuote, Money quotedTotal) =>
        recomputedQuote.Total == quotedTotal
            ? Result<Unit, DomainError>.Success(Unit.Value)
            : Result<Unit, DomainError>.Failure(new PayoffQuoteStale(quotedTotal, recomputedQuote.Total));
}
