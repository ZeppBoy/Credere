using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Core.Rates;

/// <summary>
/// The only implementation of <see cref="IInterestCalculator"/>: stateless, and delegating the formula to the
/// <see cref="InterestRate"/> subtype.
/// </summary>
public sealed class InterestCalculator : IInterestCalculator
{
    /// <inheritdoc />
    public Money Calculate(InterestRate rate, Money balance, LocalDate periodStart, LocalDate periodEnd)
    {
        ArgumentNullException.ThrowIfNull(rate);
        ArgumentNullException.ThrowIfNull(balance);
        ArgumentOutOfRangeException.ThrowIfLessThan(periodEnd, periodStart);

        return rate.CalculateInterest(balance, periodStart, periodEnd);
    }
}
