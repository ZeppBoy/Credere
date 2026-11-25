using System.Numerics;

namespace Credere.Core.Rates;

/// <summary>
/// Converts a count of cents, already rounded by the caller, to the <see cref="decimal"/> that holds it exactly
/// (docs/domain/rates.md, "IInterestCalculator"). It is shared by <see cref="ExactInterest"/> and the schedule
/// generators, so there is one copy of the conversion.
/// </summary>
internal static class CentsToDecimal
{
    private const int Places = 2;
    private const int BitsPerWord = 32;
    private const uint WordMask = uint.MaxValue;

    private static readonly BigInteger MaxCoefficient = new(decimal.MaxValue);

    /// <summary>
    /// Converts <paramref name="cents"/> using the fewest decimal places (0, 1 or 2) that hold it, so that a whole-unit
    /// amount such as <see cref="decimal.MaxValue"/> still fits the 96-bit coefficient. Dividing the cast cents by 100
    /// would overflow there.
    /// </summary>
    /// <param name="cents">The non-negative magnitude, in cents.</param>
    /// <param name="negative">Whether the amount is negative. A zero amount is never negative.</param>
    /// <returns>The exact amount.</returns>
    /// <exception cref="OverflowException">The coefficient does not fit the 96 bits of a <see cref="decimal"/>.</exception>
    public static decimal ToDecimal(BigInteger cents, bool negative)
    {
        var scale = Places;
        var coefficient = cents;
        while (scale > 0 && (coefficient % 10).IsZero)
        {
            coefficient /= 10;
            scale--;
        }

        if (coefficient > MaxCoefficient)
        {
            throw new OverflowException("The amount cannot be represented exactly as a decimal.");
        }

        return new decimal(
            (int)(uint)(coefficient & WordMask),
            (int)(uint)((coefficient >> BitsPerWord) & WordMask),
            (int)(uint)((coefficient >> (2 * BitsPerWord)) & WordMask),
            negative && !coefficient.IsZero,
            (byte)scale);
    }
}
