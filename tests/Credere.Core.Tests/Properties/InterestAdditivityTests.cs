using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.ValueObjects;
using FsCheck;
using FsCheck.Xunit;
using NodaTime;
using Xunit;

namespace Credere.Core.Tests.Properties;

/// <summary>
/// Checks the simple-interest sum in AGENTS.md §8. The additive fractions are defined by ISDA
/// 2006 Definitions §4.16(b), (d), (e) and (g). Section 5.1(b) gives the fixed-interest formula.
/// Credere rounds each calculation to the nearest cent, ties to even (docs/domain/value-objects.md, “Money”).
/// </summary>
public sealed class InterestAdditivityTests
{
    private const decimal HalfCent = 0.005m;
    private static readonly InterestCalculator Calculator = new();

    private static readonly DayCountConvention[] AdditiveConventions =
    [
        new ActActIsda(), new Act365Fixed(), new Act360(), new ThirtyE360(),
    ];

    [Property]
    public void Calculate_partitionOfAConstantBalance_matchesWholePeriodWithinRounding(
        uint principalCents, ushort rate, ushort startOffset, NonEmptyArray<ushort> lengths)
    {
        var balance = Euro(principalCents / 100m);
        var nominal = new Percentage(rate % 10_001 / 100m);
        var start = new LocalDate(2026, 1, 1).PlusDays(startOffset % 1_461);
        var dates = new List<LocalDate> { start };
        foreach (var length in lengths.Get)
        {
            dates.Add(dates[^1].PlusDays(1 + (length % 730)));
        }

        foreach (var convention in AdditiveConventions)
        {
            var fixedRate = FixedRate.Create(nominal, convention).Value;
            var sum = SumPeriodInterest(fixedRate, balance, dates);
            var whole = Calculator.Calculate(fixedRate, balance, dates[0], dates[^1]);
            Assert.Equal(balance.Currency, whole.Currency);

            // There is at most half a cent of rounding error for each subperiod and for the whole interval.
            var tolerance = dates.Count * HalfCent;
            Assert.True(Math.Abs(sum - whole.Amount) <= tolerance,
                FormattableString.Invariant($"{convention.Name}: sum {sum}, whole {whole.Amount}, tolerance {tolerance}"));
        }
    }

    [Fact]
    public void Calculate_threeDailyHalfCentTies_canReachTheRoundingBound()
    {
        // 180 × 1% × 1/360 = 0.005, which rounds to zero. The combined 0.015 rounds to 0.02.
        // This is the formula-derived boundary of four half-cent errors (the three parts and the total).
        var balance = Euro(180m);
        var rate = FixedRate.Create(new Percentage(1m), new Act360()).Value;
        var start = new LocalDate(2026, 1, 15);
        LocalDate[] dates = [start, start.PlusDays(1), start.PlusDays(2), start.PlusDays(3)];

        Assert.Equal(0m, SumPeriodInterest(rate, balance, dates));
        Assert.Equal(Euro(0.02m), Calculator.Calculate(rate, balance, dates[0], dates[^1]));
    }

    private static decimal SumPeriodInterest(FixedRate rate, Money balance, IReadOnlyList<LocalDate> dates)
    {
        var sum = 0m;
        for (var index = 1; index < dates.Count; index++)
        {
            var interest = Calculator.Calculate(rate, balance, dates[index - 1], dates[index]);
            Assert.Equal(balance.Currency, interest.Currency);
            sum += interest.Amount;
        }

        return sum;
    }

    private static Money Euro(decimal amount) => Money.Create(amount, "EUR").Value;
}
