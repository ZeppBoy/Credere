using Credere.Core.DayCounts;
using Credere.Core.Results;
using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Core.Rates;

/// <summary>Calculates simple interest using a base rate and margin.</summary>
/// <remarks>
/// Uses the non-compounded floating amount formula in ISDA 2006 Definitions §6.1(a), with day-count fractions
/// from §4.16. In M1 the fixed reset supplies one base rate for the whole interval. The base and margin
/// contributions are calculated exactly and rounded together to cents.
/// </remarks>
public sealed record FloatingRate : InterestRate
{
    private FloatingRate(ResetSchedule resetSchedule, Percentage margin, Percentage nominalRate, DayCountConvention convention)
    {
        ResetSchedule = resetSchedule;
        Margin = margin;
        NominalRate = nominalRate;
        DayCountConvention = convention;
    }

    /// <summary>Gets the base rate supplied by the reset schedule.</summary>
    public Percentage BaseRate => ResetSchedule.BaseRate;

    /// <summary>Gets the margin added to the base rate.</summary>
    public Percentage Margin { get; }

    /// <summary>Gets the schedule supplying the base rate.</summary>
    public ResetSchedule ResetSchedule { get; }

    /// <inheritdoc />
    public override Percentage NominalRate { get; }

    /// <inheritdoc />
    public override DayCountConvention DayCountConvention { get; }

    /// <summary>Creates a floating rate with one fixed reset.</summary>
    /// <param name="baseRate">The base rate.</param>
    /// <param name="margin">The spread added to the base rate.</param>
    /// <param name="dayCountConvention">The convention used to measure the interest period.</param>
    /// <returns>The rate, or a validation error if the nominal rate is outside the decimal range.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static Result<FloatingRate, DomainError> Create(
        Percentage baseRate, Percentage margin, DayCountConvention dayCountConvention)
    {
        ArgumentNullException.ThrowIfNull(baseRate);
        ArgumentNullException.ThrowIfNull(margin);
        ArgumentNullException.ThrowIfNull(dayCountConvention);

        var larger = Math.Max(baseRate.Value, margin.Value);
        var smaller = Math.Min(baseRate.Value, margin.Value);
        // Subtract the larger rate first so the range check preserves a small contribution beside decimal.MaxValue.
        if (smaller > decimal.MaxValue - larger)
        {
            return Result<FloatingRate, DomainError>.Failure(new ValidationError("nominalRate", "the base rate plus margin exceeds the decimal range"));
        }

        return Result<FloatingRate, DomainError>.Success(new FloatingRate(
            ResetSchedule.Fixed(baseRate), margin, baseRate + margin, dayCountConvention));
    }

    /// <inheritdoc />
    internal override Money CalculateInterest(Money balance, LocalDate periodStart, LocalDate periodEnd)
    {
        var fraction = DayCountConvention.ExactFraction(periodStart, periodEnd);
        var interest = ExactInterest.Of(balance, BaseRate, fraction) + ExactInterest.Of(balance, Margin, fraction);

        return interest.ToMoney(balance.Currency);
    }
}
