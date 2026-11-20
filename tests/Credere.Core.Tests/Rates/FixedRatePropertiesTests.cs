using System.Numerics;
using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.ValueObjects;
using FsCheck.Xunit;
using NodaTime;

namespace Credere.Core.Tests.Rates;

/// <summary>Checks the invariants of docs/domain/rates.md, "Testing", for <see cref="FixedRate"/>.</summary>
public sealed class FixedRatePropertiesTests
{
    private static readonly InterestCalculator Calculator = new();
    private static readonly LocalDate Origin = new(2000, 1, 1);

    // Whole cents up to 10^12, rates in hundredths of a percent up to 100%, periods up to ten years.
    private static Money Balance(long cents) => Money.Create(Math.Abs(cents % 1_000_000_000_000L) / 100m, "EUR").Value;

    private static FixedRate Rate(ushort hundredthsOfPercent, DayCountConvention convention) =>
        FixedRate.Create(new Percentage(hundredthsOfPercent % 10_001 / 100m), convention).Value;

    private static LocalDate Day(ushort offset) => Origin.PlusDays(offset);

    [Property]
    public bool ZeroBalance_givesZeroInterest(ushort rate, ushort offset, ushort length)
    {
        var interest = Calculator.Calculate(
            Rate(rate, new Act365Fixed()), Money.Zero("EUR"), Day(offset), Day(offset).PlusDays(length));

        return interest.Amount == 0m;
    }

    [Property]
    public bool EmptyPeriod_givesZeroInterest(long cents, ushort rate, ushort offset)
    {
        var interest = Calculator.Calculate(Rate(rate, new ActActIsda()), Balance(cents), Day(offset), Day(offset));

        return interest.Amount == 0m;
    }

    [Property]
    public bool ZeroRate_givesZeroInterest(long cents, ushort offset, ushort length)
    {
        var interest = Calculator.Calculate(
            Rate(0, new Act360()), Balance(cents), Day(offset), Day(offset).PlusDays(length));

        return interest.Amount == 0m;
    }

    [Property]
    public bool Interest_isLinearInBalance_withinOneCent(long cents, ushort rate, ushort offset, ushort length)
    {
        // Each side rounds once, so doubling the balance can differ from doubling the interest by a cent.
        var fixedRate = Rate(rate, new Act365Fixed());
        var (start, end) = (Day(offset), Day(offset).PlusDays(length % 3651));
        var single = Calculator.Calculate(fixedRate, Balance(cents), start, end);
        var doubled = Calculator.Calculate(fixedRate, Balance(cents) * 2m, start, end);

        return Math.Abs(doubled.Amount - (single * 2m).Amount) <= 0.01m;
    }

    [Property]
    public bool Act360_matchesExactIntegerArithmetic(long cents, ushort rate, ushort offset, ushort length) =>
        MatchesOracle(new Act360(), 360, cents, rate, offset, length);

    [Property]
    public bool Act365Fixed_matchesExactIntegerArithmetic(long cents, ushort rate, ushort offset, ushort length) =>
        MatchesOracle(new Act365Fixed(), 365, cents, rate, offset, length);

    // Oracle: cents = B x (R / 10000) x (days / denominator), in exact integer arithmetic, rounded half-even once.
    // B is the balance in cents and R the rate in hundredths of a percent. It shares no code with the implementation.
    private static bool MatchesOracle(
        DayCountConvention convention, int denominator, long cents, ushort rate, ushort offset, ushort length)
    {
        var days = length % 3651;
        var (start, end) = (Day(offset), Day(offset).PlusDays(days));
        var balance = Balance(cents);
        var hundredths = rate % 10_001;

        var interest = Calculator.Calculate(Rate(rate, convention), balance, start, end);

        var balanceCents = new BigInteger(balance.Amount * 100m);
        var numerator = balanceCents * hundredths * days;
        var divisor = new BigInteger(10_000) * denominator;
        var quotient = BigInteger.DivRem(numerator, divisor, out var remainder);
        var twiceRemainder = remainder * 2;
        if (twiceRemainder > divisor || (twiceRemainder == divisor && !quotient.IsEven))
        {
            quotient += 1;
        }

        return interest.Amount * 100m == (decimal)quotient;
    }
}
