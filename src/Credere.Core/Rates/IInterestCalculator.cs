using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Core.Rates;

/// <summary>
/// The single entry point for interest: schedules, accrual and payoff all call it, and nothing else computes
/// interest (docs/domain/rates.md, "IInterestCalculator").
/// </summary>
public interface IInterestCalculator
{
    /// <summary>
    /// Calculates the interest on <paramref name="balance"/> for the period from <paramref name="periodStart"/>
    /// (inclusive) to <paramref name="periodEnd"/> (exclusive) under <paramref name="rate"/>.
    /// </summary>
    /// <param name="rate">The rate and the day-count convention that apply.</param>
    /// <param name="balance">The outstanding balance for the period, not the original principal.</param>
    /// <param name="periodStart">The first day of the period.</param>
    /// <param name="periodEnd">The day after the last day of the period.</param>
    /// <returns>
    /// The interest, in the currency of <paramref name="balance"/>. It is rounded once, to two places with
    /// <see cref="MidpointRounding.ToEven"/>, when this <see cref="Money"/> is created.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="periodEnd"/> precedes <paramref name="periodStart"/>.</exception>
    Money Calculate(InterestRate rate, Money balance, LocalDate periodStart, LocalDate periodEnd);
}
