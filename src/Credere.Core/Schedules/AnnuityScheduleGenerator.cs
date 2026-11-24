using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.ValueObjects;

namespace Credere.Core.Schedules;

/// <summary>
/// Generates an annuity schedule: an equal <see cref="ScheduleRow.TotalPayment"/> on every row except the last, which
/// pays its own interest and the remaining balance (docs/domain/schedules.md, "1. Annuity").
/// </summary>
/// <remarks>
/// <para>
/// Instalment: <c>P × r × (1+r)^n / ((1+r)^n − 1)</c> with <c>r = nominalRate / periodsPerYear</c>, rounded once to cents
/// half-even when it becomes a <see cref="Money"/>. It is computed in the equivalent form
/// <c>P × r / (1 − (1+r)^−n)</c>, because <c>(1+r)^n</c> overflows a <see cref="decimal"/> for a long term at a high rate,
/// while <c>(1+r)^−n</c> only shrinks. A zero rate uses <c>P / n</c>.
/// </para>
/// <para>
/// Interest: every row's interest is <see cref="IInterestCalculator.Calculate"/> on the opening balance over the row's
/// period, so it is the number Servicing accrues for that period under the rate's own day-count convention. The
/// instalment uses the nominal periodic rate and the interest uses the day count, so the two differ whenever a period's
/// day-count fraction is not exactly <c>1 / periodsPerYear</c>; the last row absorbs the difference and the rounding.
/// </para>
/// <para>
/// Sources: docs/domain/schedules.md, "1. Annuity" and "Rounding and residuals" (the residual placement is a Credere design
/// decision, with no regulatory source); the reference schedules are <c>AnnuityScheduleGeneratorVectors</c> in the test
/// project, recomputed independently by a reviewer from another vendor and approved by the owner on PR #51.
/// </para>
/// </remarks>
public sealed class AnnuityScheduleGenerator
{
    private readonly IInterestCalculator interestCalculator;

    /// <summary>Initializes a new instance of the <see cref="AnnuityScheduleGenerator"/> class.</summary>
    /// <param name="interestCalculator">The single entry point for interest.</param>
    public AnnuityScheduleGenerator(IInterestCalculator interestCalculator)
    {
        ArgumentNullException.ThrowIfNull(interestCalculator);

        this.interestCalculator = interestCalculator;
    }

    /// <summary>Generates the schedule for a request.</summary>
    /// <param name="request">A validated request.</param>
    /// <returns>The schedule, or a <see cref="ValidationError"/> when the request cannot be scheduled.</returns>
    public Result<AmortizationSchedule, DomainError> Generate(ScheduleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rowCount = SchedulePeriods.RowCount(request.Term, request.PaymentFrequency);
        var periods = SchedulePeriods.Periods(
            request.DisbursementDate, request.FirstPaymentDate, request.PaymentFrequency, rowCount);

        try
        {
            var instalment = Instalment(request, rowCount);
            var rows = new List<ScheduleRow>(rowCount);
            var opening = request.Principal;

            for (var index = 0; index < rowCount; index++)
            {
                var interest = interestCalculator.Calculate(request.Rate, opening, periods[index].Start, periods[index].End);
                var isLast = index == rowCount - 1;
                var principalDue = isLast ? opening : instalment - interest;
                var total = isLast ? interest + opening : instalment;
                var closing = opening - principalDue;

                rows.Add(new ScheduleRow(index + 1, periods[index].End, opening, interest, principalDue, total, closing));
                opening = closing;
            }

            return AmortizationSchedule.Create(request.Principal, rows);
        }
        catch (OverflowException)
        {
            // An amount near decimal.MaxValue: Money cannot hold the instalment, a balance or an interest amount.
            return Result<AmortizationSchedule, DomainError>.Failure(
                new ValidationError("principal", "too large to schedule: an amount overflows"));
        }
    }

    private static Money Instalment(ScheduleRequest request, int rowCount)
    {
        var periodsPerYear = SchedulePeriods.PeriodsPerYear(request.PaymentFrequency);
        var periodicRate = request.Rate.NominalRate.Fraction / periodsPerYear;

        if (periodicRate == 0m)
        {
            return request.Principal / rowCount;
        }

        // P × r / (1 − (1+r)^−n), by repeated multiplication so that n stays an integer power in decimal.
        var discount = 1m / (1m + periodicRate);
        var discountPower = 1m;
        for (var period = 0; period < rowCount; period++)
        {
            discountPower *= discount;
        }

        var amount = request.Principal.Amount * periodicRate / (1m - discountPower);

        return Money.Create(amount, request.Principal.Currency).Value;
    }
}
