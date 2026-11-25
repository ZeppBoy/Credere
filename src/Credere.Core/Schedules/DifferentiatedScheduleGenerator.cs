using System.Globalization;
using System.Numerics;
using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.ValueObjects;

namespace Credere.Core.Schedules;

/// <summary>Generates a schedule with equal principal due on each row before maturity.</summary>
/// <remarks>
/// The California State Board of Equalization, Assessors' Handbook §503, Problem 11, p. 46, illustrates
/// constant principal and interest on the opening balance. Credere's monthly and quarterly variant and its
/// final-row residual are defined in docs/domain/schedules.md, §2. Principal per regular row is rounded
/// once to cents, ties to even (docs/domain/value-objects.md, Money). Period interest comes from
/// <see cref="IInterestCalculator"/> and uses the day-count convention on the request's rate.
/// </remarks>
public sealed class DifferentiatedScheduleGenerator : IScheduleGenerator
{
    private const int CentsPerUnit = 100;

    private readonly IInterestCalculator interestCalculator;

    /// <summary>Initializes a differentiated schedule generator.</summary>
    /// <param name="interestCalculator">The shared calculator for period interest.</param>
    public DifferentiatedScheduleGenerator(IInterestCalculator interestCalculator)
    {
        ArgumentNullException.ThrowIfNull(interestCalculator);
        this.interestCalculator = interestCalculator;
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

        try
        {
            var currency = request.Principal.Currency;
            var zero = Money.Zero(currency);
            var rows = new List<ScheduleRow>(rowCount);
            var opening = request.Principal;
            var openingCents = ToCents(opening);
            var regularCents = RoundPrincipal(openingCents, rowCount);
            var regularPrincipal = FromCents(regularCents, currency);

            for (var index = 0; index < rowCount; index++)
            {
                var period = periods[index];
                var isLast = index == rowCount - 1;
                if (!isLast && regularCents >= openingCents)
                {
                    // Stop before handing a depleted balance to the next interest calculation.
                    return InvalidPrincipal(
                        $"row {(index + 1).ToString(CultureInfo.InvariantCulture)}: principal due must be less than the opening balance before maturity");
                }

                var interest = interestCalculator.Calculate(request.Rate, opening, period.Start, period.End);
                var principalDue = isLast ? opening : regularPrincipal;
                var principalDueCents = isLast ? openingCents : regularCents;
                var closingCents = openingCents - principalDueCents;
                var closing = isLast ? zero : FromCents(closingCents, currency);
                var total = FromCents(principalDueCents + ToCents(interest), currency);
                rows.Add(new ScheduleRow(index + 1, period.End, opening, zero, interest, principalDue, total, closing));
                opening = closing;
                openingCents = closingCents;
            }

            return AmortizationSchedule.Create(request.Principal, rows);
        }
        catch (OverflowException)
        {
            return InvalidPrincipal("a scheduled amount cannot be represented exactly as money");
        }
    }

    private static BigInteger RoundPrincipal(BigInteger principalCents, int rowCount)
    {
        var quotient = BigInteger.DivRem(principalCents, rowCount, out var remainder);
        var twiceRemainder = remainder * 2;
        return twiceRemainder > rowCount || (twiceRemainder == rowCount && !quotient.IsEven)
            ? quotient + BigInteger.One
            : quotient;
    }

    private static BigInteger ToCents(Money amount)
    {
        var exact = ExactYearFraction.FromDecimal(amount.Amount);
        return exact.Numerator * CentsPerUnit / exact.Denominator;
    }

    private static Money FromCents(BigInteger cents, string currency)
    {
        var units = BigInteger.DivRem(cents, CentsPerUnit, out var remainder);
        var money = Money.Create((decimal)units + (decimal)remainder / CentsPerUnit, currency).Value;
        if (ToCents(money) != cents)
        {
            throw new OverflowException("The exact number of cents does not fit in a decimal amount.");
        }

        return money;
    }

    private static Result<AmortizationSchedule, DomainError> InvalidPrincipal(string reason) =>
        Result<AmortizationSchedule, DomainError>.Failure(new ValidationError("principal", reason));
}
