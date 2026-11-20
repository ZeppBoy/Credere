using System.Numerics;

namespace Credere.Core.DayCounts;

/// <summary>
/// A year fraction as an exact ratio of two integers, so a repeating fraction such as 20/360 is not rounded to the
/// 28 digits of a <see cref="decimal"/> (docs/domain/rates.md, "IInterestCalculator").
/// </summary>
internal readonly record struct ExactYearFraction
{
    /// <summary>Creates a fraction from its numerator and a positive denominator.</summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator, greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="denominator"/> is not positive, which is a programmer error.</exception>
    public ExactYearFraction(BigInteger numerator, BigInteger denominator)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(denominator, BigInteger.Zero);

        Numerator = numerator;
        Denominator = denominator;
    }

    /// <summary>Gets the numerator.</summary>
    public BigInteger Numerator { get; }

    /// <summary>Gets the denominator, greater than zero for every fraction not created with <see langword="default"/>.</summary>
    public BigInteger Denominator { get; }

    /// <summary>
    /// Returns the exact value of a <see cref="decimal"/>: its 96-bit coefficient over a power of ten. A decimal is a
    /// finite fraction, so nothing is lost.
    /// </summary>
    /// <param name="value">The decimal value.</param>
    /// <returns>The same value as an exact fraction.</returns>
    public static ExactYearFraction FromDecimal(decimal value)
    {
        const int scaleShift = 16;
        const int scaleMask = 0xFF;

        var bits = decimal.GetBits(value);
        var coefficient = (new BigInteger((uint)bits[2]) << 64) | (new BigInteger((uint)bits[1]) << 32) | (uint)bits[0];
        var scale = (bits[3] >> scaleShift) & scaleMask;

        return new ExactYearFraction(
            value < 0m ? -coefficient : coefficient,
            BigInteger.Pow(new BigInteger(10), scale));
    }
}
