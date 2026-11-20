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
    /// <returns>The interest, rounded once, half to even, through <see cref="ExactInterest.ToMoney"/>.</returns>
    internal abstract Money CalculateInterest(Money balance, LocalDate periodStart, LocalDate periodEnd);
}
