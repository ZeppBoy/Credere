using Credere.Core.DayCounts;
using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Core.Rates;

/// <summary>
/// The root of the interest-rate hierarchy. Each subtype is a distinct banking concept with its own interest
/// formula (docs/domain/rates.md, "InterestRate").
/// </summary>
/// <remarks>
/// The formula member is <see langword="internal"/>, so code outside <c>Credere.Core</c> can't bypass
/// <see cref="IInterestCalculator"/>, and, because it is abstract, the hierarchy can't be extended outside the
/// assembly either.
/// </remarks>
public abstract record InterestRate
{
    /// <summary>Gets the quoted rate: the headline percent a borrower sees for the product.</summary>
    public abstract Percentage NominalRate { get; }

    /// <summary>Gets the day-count convention that turns a period into a year fraction.</summary>
    public abstract DayCountConvention DayCountConvention { get; }

    /// <summary>Calculates the interest for a period. Called only by <see cref="InterestCalculator"/>.</summary>
    /// <param name="balance">The outstanding balance for the period.</param>
    /// <param name="periodStart">The first day of the period.</param>
    /// <param name="periodEnd">The day after the last day of the period.</param>
    /// <returns>The interest, rounded once when the <see cref="Money"/> is created.</returns>
    internal abstract Money CalculateInterest(Money balance, LocalDate periodStart, LocalDate periodEnd);

    /// <summary>
    /// Creates the interest <see cref="Money"/> from an unrounded <see cref="decimal"/>, rounding once, to two places
    /// with <see cref="MidpointRounding.ToEven"/>, and first removing the last-digit noise that a repeating day-count
    /// fraction leaves on an exact half-cent tie.
    /// </summary>
    /// <remarks>
    /// A fraction such as <c>20/360</c> is stored as <c>0.0555555555555555555555555556m</c>, 28 digits, rounded up.
    /// Multiplying it by an exact half-cent amount (1,950.00 × 0.06% × 20/360 = 0.065) gives
    /// <c>0.06500000000000000000000000052</c>, which a plain half-even rounding takes for "above the tie" and rounds
    /// up, although the exact value is a tie that goes to the even cent, 0.06. An exhaustive scan of 13.5 million
    /// exact ties under Actual/360 and Actual/365 Fixed found this in about 30% of them. A value within 1e-22 of the
    /// interest's magnitude (a million times the noise of a 28-digit fraction) of a half-cent is therefore taken to be
    /// that half-cent. Values that differ from a tie by more than this are rounded as they are.
    /// </remarks>
    /// <param name="interest">The unrounded interest.</param>
    /// <param name="currency">The currency of the balance the interest was calculated on.</param>
    /// <returns>The rounded interest.</returns>
    internal static Money ToMoney(decimal interest, string currency)
    {
        const decimal relativeNoise = 1e-22m;

        var halfCents = interest * 200m;
        var nearest = Math.Round(halfCents, 0, MidpointRounding.ToEven);
        var tolerance = relativeNoise * Math.Max(1m, Math.Abs(halfCents));
        var snapped = Math.Abs(halfCents - nearest) <= tolerance ? nearest / 200m : interest;

        // The balance's currency was validated when the balance was created, so this cannot fail.
        return Money.Create(snapped, currency).Value;
    }
}
