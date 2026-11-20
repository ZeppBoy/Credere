using System.Reflection;
using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.ValueObjects;
using NodaTime;
using Xunit;

namespace Credere.Core.Tests.Rates;

/// <summary>Tests <see cref="InterestCalculator"/> as the single entry point for interest.</summary>
public sealed class InterestCalculatorTests
{
    private static readonly InterestCalculator Calculator = new();
    private static readonly LocalDate Start = new(2024, 1, 1);

    private static FixedRate Rate(decimal percent = 5m, DayCountConvention? convention = null) =>
        FixedRate.Create(new Percentage(percent), convention ?? new Act360()).Value;

    private static Money Eur(decimal amount) => Money.Create(amount, "EUR").Value;

    [Fact]
    public void Calculator_isTheInterfaceImplementation()
    {
        Assert.Equal(
            Eur(430.56m),
            CalculateThroughTheInterface(Calculator, Rate(), Eur(100_000m), new LocalDate(2024, 2, 1)));
    }

    // The test is about the interface contract, so the parameter stays the interface type (CA1859 would narrow it).
#pragma warning disable CA1859
    private static Money CalculateThroughTheInterface(
        IInterestCalculator calculator, InterestRate rate, Money balance, LocalDate end) =>
        calculator.Calculate(rate, balance, Start, end);
#pragma warning restore CA1859

    [Fact]
    public void Calculate_usesTheDayCountConventionOfTheRate()
    {
        var end = new LocalDate(2024, 2, 1);

        var act360 = Calculator.Calculate(Rate(5m, new Act360()), Eur(100_000m), Start, end);
        var act365 = Calculator.Calculate(Rate(5m, new Act365Fixed()), Eur(100_000m), Start, end);

        Assert.Equal(430.56m, act360.Amount);
        Assert.Equal(424.66m, act365.Amount);
    }

    [Fact]
    public void Calculate_emptyPeriod_isZero()
    {
        Assert.Equal(0m, Calculator.Calculate(Rate(), Eur(100m), Start, Start).Amount);
    }

    [Fact]
    public void Calculate_periodEndBeforeStart_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Calculator.Calculate(Rate(), Eur(100m), Start, Start.PlusDays(-1)));
    }

    private sealed record LenientConvention : DayCountConvention
    {
        public override string Name => "LENIENT";

        public override decimal Fraction(LocalDate start, LocalDate end) => 1m;
    }

    [Fact]
    public void Calculate_periodEndBeforeStart_throwsEvenIfTheConventionDoesNot()
    {
        // The calculator guards the period itself, so a convention that accepts reversed dates can't hide the error.
        var rate = Rate(5m, new LenientConvention());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => Calculator.Calculate(rate, Eur(100m), Start, Start.PlusDays(-1)));
    }

    [Fact]
    public void Calculate_nullArguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => Calculator.Calculate(null!, Eur(1m), Start, Start));
        Assert.Throws<ArgumentNullException>(() => Calculator.Calculate(Rate(), null!, Start, Start));
    }

    [Fact]
    public void Calculate_roundsOnce_toTwoPlacesHalfEven()
    {
        // 100.00 x 5% x 9/360 = 0.125 exactly: a tie that goes to the even cent, 0.12.
        Assert.Equal(0.12m, Calculator.Calculate(Rate(), Eur(100m), Start, Start.PlusDays(9)).Amount);
    }

    [Fact]
    public void InterestRate_formulaMember_isInternalAndAbstract_soTheCalculatorIsTheOnlyCaller()
    {
        var formula = typeof(InterestRate).GetMethod(
            "CalculateInterest", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(formula);
        Assert.True(formula.IsAbstract);
        Assert.True(formula.IsAssembly);
        Assert.Null(typeof(InterestRate).GetMethod("CalculateInterest", BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void InterestRate_exposesTheNominalRateAndConvention_onTheBaseType()
    {
        InterestRate rate = Rate(7.5m, new ThirtyE360());

        Assert.Equal(new Percentage(7.5m), rate.NominalRate);
        Assert.Equal(new ThirtyE360(), rate.DayCountConvention);
    }

    [Fact]
    public void Calculator_isSealedAndStateless()
    {
        Assert.True(typeof(InterestCalculator).IsSealed);
        Assert.Empty(typeof(InterestCalculator).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
    }
}
