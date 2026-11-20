using System.Numerics;
using Credere.Core.DayCounts;
using Credere.Core.ValueObjects;

namespace Credere.Core.Rates;

/// <summary>
/// Interest carried as an exact ratio of integers, in currency units, until it is rounded once to cents
/// (docs/domain/rates.md, "IInterestCalculator").
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="decimal"/> holds 28 digits, so a repeating day-count fraction such as <c>20/360</c> is rounded, and
/// an exact half-cent product (1,950.00 × 0.06% × 20/360 = 0.065) lands a hair above or below the tie. Carrying the
/// product as a ratio keeps it exact: <see cref="ToMoney"/> compares the exact remainder with half a cent and uses
/// the parity of the cent only on equality, so a value that differs from a tie by any amount, however small, rounds
/// to the nearer cent.
/// </para>
/// <para>
/// Rounding rule: round half to even on the magnitude, sign kept (docs/domain/value-objects.md, "Money";
/// IEEE 754-2019 §4.3.1 roundTiesToEven; <see cref="MidpointRounding.ToEven"/>). Sums of exact interests, such as the
/// segments of a floating or tiered rate, stay exact until the single <see cref="ToMoney"/>.
/// </para>
/// </remarks>
internal readonly struct ExactInterest
{
    private const int CentsPerUnit = 100;
    private const int PercentDivisor = 100;

    private readonly BigInteger numerator;
    private readonly BigInteger denominator;

    private ExactInterest(BigInteger numerator, BigInteger denominator)
    {
        this.numerator = numerator;
        this.denominator = denominator;
    }

    /// <summary>Gets zero interest. A default value is also zero.</summary>
    public static ExactInterest Zero => default;

    private BigInteger Denominator => denominator.IsZero ? BigInteger.One : denominator;

    /// <summary>
    /// Creates the interest <c>balance × (nominalRatePercent / 100) × yearFraction</c> exactly.
    /// </summary>
    /// <param name="balance">The balance; its amount is a finite decimal.</param>
    /// <param name="nominalRate">The nominal rate in percent units; its value is a finite decimal.</param>
    /// <param name="yearFraction">The exact year fraction of the period.</param>
    /// <returns>The exact interest.</returns>
    public static ExactInterest Of(Money balance, Percentage nominalRate, ExactYearFraction yearFraction)
    {
        ArgumentNullException.ThrowIfNull(balance);
        ArgumentNullException.ThrowIfNull(nominalRate);

        // A decimal is a coefficient over a power of ten, so both factors are exact ratios.
        var amount = ExactYearFraction.FromDecimal(balance.Amount);
        var rate = ExactYearFraction.FromDecimal(nominalRate.Value);

        return new ExactInterest(
            amount.Numerator * rate.Numerator * yearFraction.Numerator,
            amount.Denominator * rate.Denominator * PercentDivisor * yearFraction.Denominator);
    }

    /// <summary>Adds two exact interests without rounding.</summary>
    /// <param name="left">The first interest.</param>
    /// <param name="right">The second interest.</param>
    /// <returns>The exact sum.</returns>
    public static ExactInterest operator +(ExactInterest left, ExactInterest right)
    {
        var sum = left.numerator * right.Denominator + right.numerator * left.Denominator;
        var product = left.Denominator * right.Denominator;

        // The product is positive, so the divisor is at least 1. Reducing keeps a long sum of segments small.
        var divisor = BigInteger.GreatestCommonDivisor(sum, product);

        return new ExactInterest(sum / divisor, product / divisor);
    }

    /// <summary>Rounds once, half to even, to cents, and creates the <see cref="Money"/>.</summary>
    /// <param name="currency">The currency of the balance the interest was calculated on.</param>
    /// <returns>The rounded interest.</returns>
    /// <exception cref="OverflowException">The rounded amount is outside the range of <see cref="decimal"/>, which <see cref="Money"/> cannot hold.</exception>
    public Money ToMoney(string currency)
    {
        var cents = BigInteger.DivRem(BigInteger.Abs(numerator) * CentsPerUnit, Denominator, out var remainder);

        // Compare the exact remainder with half the denominator; use the parity of the cent only on an exact tie.
        var twiceRemainder = remainder * 2;
        if (twiceRemainder > Denominator || (twiceRemainder == Denominator && !cents.IsEven))
        {
            cents += BigInteger.One;
        }

        // Split into whole units and cents, so a result of decimal.MaxValue is not first multiplied by 100.
        var units = BigInteger.DivRem(cents, CentsPerUnit, out var centsPart);
        var amount = (decimal)units + ((decimal)centsPart / CentsPerUnit);

        // The balance's currency was validated when the balance was created, so this cannot fail.
        return Money.Create(numerator.Sign < 0 ? -amount : amount, currency).Value;
    }
}
