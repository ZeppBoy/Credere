using System.Numerics;
using Credere.Core.Results;

namespace Credere.Core.Aprc;

/// <summary>
/// Solves the present-value equation in Directive (EU) 2023/2225, Annex III, Part I,
/// for one initial drawdown. Reference examples are in Directive 98/7/EC, Annex II, Parts A and B.
/// </summary>
internal static class AprcSolver
{
    private const decimal MaximumRate = 100m;
    private const decimal RelativeResidualTolerance = 0.000000000001m;
    private const decimal RateTolerance = 0.0000000001m;
    private const int MaximumIterations = 200;
    private const int DisclosureCentsPerRateUnit = 10_000;

    internal readonly record struct Solution(decimal Rate, int Iterations, decimal FinalResidual, decimal DisclosedPercent);

    /// <summary>
    /// Finds the non-negative APRC for a validated positive drawdown and non-negative payments in the same currency.
    /// Payment times are non-negative year fractions measured from the drawdown. The residual is in currency units.
    /// </summary>
    internal static Result<Solution, DomainError> Solve(
        decimal drawdown,
        IReadOnlyList<(decimal Amount, decimal Years)> payments)
    {
        var largestAmount = drawdown;
        foreach (var payment in payments)
        {
            largestAmount = Math.Max(largestAmount, payment.Amount);
        }

        // Use a common power of ten if the sum of amounts could exceed decimal's range.
        var amountScale = 1m;
        var safeAmount = decimal.MaxValue / (payments.Count + 1m);
        while (largestAmount / amountScale > safeAmount)
        {
            amountScale *= 10m;
        }

        var scaledDrawdown = drawdown / amountScale;
        var paymentTotal = 0m;
        var immediateTotal = 0m;
        foreach (var (amount, years) in payments)
        {
            var scaledAmount = amount / amountScale;
            paymentTotal += scaledAmount;
            if (years == 0m)
            {
                immediateTotal += scaledAmount;
            }
        }

        if (paymentTotal < scaledDrawdown)
        {
            return Result<Solution, DomainError>.Failure(
                new ValidationError(nameof(payments), "total payments must cover the drawdown"));
        }

        if (paymentTotal == scaledDrawdown)
        {
            return Result<Solution, DomainError>.Success(new Solution(0m, 0, 0m, 0m));
        }

        var remainingDrawdown = scaledDrawdown - immediateTotal;
        if (remainingDrawdown <= 0m)
        {
            return NoRoot();
        }

        var residualTolerance = RelativeResidualTolerance * paymentTotal;
        var lowerRate = 0m;
        var upperRate = 1m;
        var upperResidual = PresentValueResidual(remainingDrawdown, payments, amountScale, upperRate);
        while (upperResidual < 0m && upperRate < MaximumRate)
        {
            upperRate = Math.Min(2m * upperRate, MaximumRate);
            upperResidual = PresentValueResidual(remainingDrawdown, payments, amountScale, upperRate);
        }

        // Discount-factor precision can leave a small residual when the present values meet at the upper limit.
        var upperLimitTolerance = RelativeResidualTolerance * remainingDrawdown;
        if (upperResidual == 0m || (upperRate == MaximumRate && Math.Abs(upperResidual) < upperLimitTolerance))
        {
            return Result<Solution, DomainError>.Success(new Solution(
                upperRate, 0, upperResidual * amountScale, RoundedDisclosureCents(upperRate) / 100m));
        }

        if (upperResidual < 0m)
        {
            return NoRoot();
        }

        for (var iteration = 1; ; iteration++)
        {
            var width = upperRate - lowerRate;
            var rate = lowerRate + width / 2m;
            var residual = PresentValueResidual(remainingDrawdown, payments, amountScale, rate);
            if (Math.Abs(residual) < residualTolerance || width < RateTolerance || iteration == MaximumIterations)
            {
                var disclosedPercent = DisclosedPercent(drawdown, payments, remainingDrawdown, amountScale, lowerRate, upperRate);
                return Result<Solution, DomainError>.Success(new Solution(rate, iteration, residual * amountScale, disclosedPercent));
            }

            if (residual < 0m)
            {
                lowerRate = rate;
            }
            else
            {
                upperRate = rate;
            }
        }
    }

    private static Result<Solution, DomainError> NoRoot() => Result<Solution, DomainError>.Failure(
        new ValidationError("payments", "no non-negative APRC was found at or below 10,000%"));

    private static decimal DisclosedPercent(
        decimal drawdown,
        IReadOnlyList<(decimal Amount, decimal Years)> payments,
        decimal remainingDrawdown,
        decimal amountScale,
        decimal lowerRate,
        decimal upperRate)
    {
        var lower = RoundedDisclosureCents(lowerRate);
        var upper = RoundedDisclosureCents(upperRate);
        if (lower == upper)
        {
            return lower / 100m;
        }

        var wholeYears = payments.All(static payment => payment.Amount == 0m || decimal.Truncate(payment.Years) == payment.Years);

        // The residual increases with the rate. At zero the exact rate equals the cutoff, which rounds up.
        while (lower < upper)
        {
            var cents = lower + (upper - lower) / 2;
            var cutoffRate = (cents + 0.5m) / DisclosureCentsPerRateUnit;
            var roundsUp = wholeYears
                ? ComparePresentValuesAtWholeYears(drawdown, payments, cents) <= 0
                : PresentValueResidual(remainingDrawdown, payments, amountScale, cutoffRate) <= 0m;

            if (roundsUp)
            {
                lower = cents + 1;
            }
            else
            {
                upper = cents;
            }
        }

        return lower / 100m;
    }

    private static int RoundedDisclosureCents(decimal rate) =>
        (int)Math.Round(rate * DisclosureCentsPerRateUnit, 0, MidpointRounding.AwayFromZero);

    private static int ComparePresentValuesAtWholeYears(
        decimal drawdown,
        IReadOnlyList<(decimal Amount, decimal Years)> payments,
        int lowerDisclosureCents)
    {
        // The cutoff's discount base (1 + X) is a rational number. Clear all denominators before comparing.
        const int baseDenominator = 2 * DisclosureCentsPerRateUnit;
        var baseNumerator = baseDenominator + 2 * lowerDisclosureCents + 1;
        var lastYear = (int)payments.Max(static payment => payment.Amount == 0m ? 0m : payment.Years);
        var drawdownValue = ExactUnits(drawdown) * BigInteger.Pow(baseNumerator, lastYear);
        var paymentValue = BigInteger.Zero;
        foreach (var (amount, years) in payments)
        {
            if (amount == 0m)
            {
                continue;
            }

            var year = (int)years;
            paymentValue += ExactUnits(amount) * BigInteger.Pow(baseDenominator, year) * BigInteger.Pow(baseNumerator, lastYear - year);
        }

        return drawdownValue.CompareTo(paymentValue);
    }

    private static BigInteger ExactUnits(decimal amount)
    {
        // Cash flows are non-negative. This puts every decimal coefficient on the same 28-place scale.
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(amount, bits);
        var coefficient = (BigInteger)(uint)bits[0] + ((BigInteger)(uint)bits[1] << 32) + ((BigInteger)(uint)bits[2] << 64);
        var scale = (bits[3] >> 16) & 0xff;
        return coefficient * BigInteger.Pow(10, 28 - scale);
    }

    private static decimal PresentValueResidual(
        decimal remainingDrawdown,
        IReadOnlyList<(decimal Amount, decimal Years)> payments,
        decimal amountScale,
        decimal rate)
    {
        var residual = remainingDrawdown;
        foreach (var (amount, years) in payments)
        {
            if (years == 0m)
            {
                continue;
            }

            var discountFactor = (decimal)Math.Pow((double)(1m + rate), -(double)years);
            residual -= (amount / amountScale) * discountFactor;
        }

        return residual;
    }
}
