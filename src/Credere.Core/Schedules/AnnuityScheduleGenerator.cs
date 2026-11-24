using System.Numerics;
using Credere.Core.DayCounts;
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
/// Instalment: <c>P × r × (1+r)^n / ((1+r)^n − 1)</c> with <c>r = nominalRate / periodsPerYear</c>. It is evaluated as an
/// exact ratio of integers and rounded to cents once, half-even, when it becomes a <see cref="Money"/>. A decimal
/// evaluation of the power would put an exact half-cent tie (EUR 100.50 at 12% over two months is exactly 51.005) a hair
/// above or below it, and round the wrong way. A zero rate uses <c>P / n</c>.
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
    private const int CentsPerUnit = 100;
    private const int PercentDivisor = 100;

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

        // Everything is an exact ratio of integers until the single rounding: P = pn / pd and r = rn / rd, where
        // r = nominalRate% / 100 / periodsPerYear. Then P × r × (1+r)^n / ((1+r)^n − 1) is
        // pn × rn × (rd + rn)^n / (pd × rd × ((rd + rn)^n − rd^n)).
        var principal = ExactYearFraction.FromDecimal(request.Principal.Amount);
        var percent = ExactYearFraction.FromDecimal(request.Rate.NominalRate.Value);
        var rateNumerator = percent.Numerator;
        var rateDenominator = percent.Denominator * PercentDivisor * periodsPerYear;

        if (rateNumerator.IsZero)
        {
            return ToMoney(principal.Numerator, principal.Denominator * rowCount, request.Principal.Currency);
        }

        var growth = BigInteger.Pow(rateDenominator + rateNumerator, rowCount);
        var baseline = BigInteger.Pow(rateDenominator, rowCount);

        return ToMoney(
            principal.Numerator * rateNumerator * growth,
            principal.Denominator * rateDenominator * (growth - baseline),
            request.Principal.Currency);
    }

    // Rounds numerator / denominator to cents once, half to even on the magnitude (IEEE 754-2019 §4.3.1 roundTiesToEven;
    // docs/domain/value-objects.md, "Money"). An exact tie goes to the even cent, so 51.005 becomes 51.00 and 2.535 becomes
    // 2.54. The explicit conversion throws OverflowException when the cents do not fit a decimal.
    private static Money ToMoney(BigInteger numerator, BigInteger denominator, string currency)
    {
        if (denominator.Sign < 0)
        {
            (numerator, denominator) = (-numerator, -denominator);
        }

        var cents = BigInteger.DivRem(BigInteger.Abs(numerator) * CentsPerUnit, denominator, out var remainder);
        var twiceRemainder = remainder * 2;
        if (twiceRemainder > denominator || (twiceRemainder == denominator && !cents.IsEven))
        {
            cents += BigInteger.One;
        }

        var amount = (decimal)cents / CentsPerUnit;

        return Money.Create(numerator.Sign < 0 ? -amount : amount, currency).Value;
    }
}
