using Credere.Core.Aprc;
using Credere.Core.Results;
using Credere.Core.ValueObjects;
using NodaTime;
using Xunit;

namespace Credere.Core.Tests.Aprc;

/// <summary>Tests the public APRC calculator against Directive 98/7/EC, Annex II, Part B.</summary>
public sealed class AprcCalculatorTests
{
    private static readonly LocalDate DrawdownDate = new(2026, 1, 15);
    private readonly AprcCalculator _calculator = new();

    /// <summary>Gets the published example, amounts, equal-month offsets, rate, precision, and disclosed percentages.</summary>
    // Directive 98/7/EC, Annex II, Part B, OJ L 101, pp. 21–23. Fractions use the M1-18 reference vectors.
    // https://eur-lex.europa.eu/eli/dir/1998/7/oj/eng
    public static TheoryData<string, decimal, decimal[], int[], decimal, decimal, decimal, decimal> ReferenceExamples => new()
    {
        { "B1", 1_000m, [1_200m], [18], 0.129243m, 0.000001m, 12.92m, 12.9m }, // p. 21, first example.
        { "B2", 950m, [1_200m], [18], 0.168526m, 0.000001m, 16.85m, 16.9m }, // p. 22, second example.
        { "B3", 1_000m, [600m, 600m], [12, 24], 0.13066m, 0.00001m, 13.07m, 13.1m }, // p. 22, third example.
        { "B4", 1_000m, [272m, 272m, 544m], [3, 6, 12], 0.13185m, 0.00001m, 13.19m, 13.2m }, // pp. 22–23, fourth example.
    };

    [Theory]
    [MemberData(nameof(ReferenceExamples))]
    public void Calculate_AnnexIIPartBExample_MatchesPrintedRateAndDisclosure(
        string example,
        decimal drawdown,
        decimal[] amounts,
        int[] months,
        decimal printedRate,
        decimal rateTolerance,
        decimal disclosedPercent,
        decimal disclosedPercentAtOnePlace)
    {
        var payments = amounts.Zip(months, (amount, offset) => Flow(amount, offset)).ToArray();

        var result = _calculator.Calculate(new AprcRequest([Flow(drawdown)], payments));

        Assert.True(result.IsSuccess, $"Annex II example {example} must produce an APRC.");
        Assert.InRange(result.Value.Rate, printedRate - rateTolerance, printedRate + rateTolerance);
        Assert.Equal(disclosedPercent, result.Value.Disclosed.Value);
        Assert.Equal(disclosedPercentAtOnePlace, Math.Round(result.Value.Disclosed.Value, 1, MidpointRounding.AwayFromZero));
        Assert.InRange(result.Value.Iterations, 1, 200);
        Assert.InRange(Math.Abs(result.Value.FinalResidual), 0m, amounts.Sum() * 0.0000000001m);
    }

    [Fact]
    public void Calculate_ExactMidpoint_RoundsDisclosureUp()
    {
        // At 3.125%, 528 / (33/32) and 544.50 / (33/32)^2 each contribute 512 in present value.
        var result = _calculator.Calculate(new AprcRequest([Flow(1_074m)], [Flow(50m), Flow(528m, 12), Flow(544.50m, 24)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(0.03125m, result.Value.Rate);
        Assert.Equal(3.13m, result.Value.Disclosed.Value);
    }

    [Fact]
    public void Calculate_BisectionEstimateBelowExactMidpoint_RoundsDisclosureUp()
    {
        // Annex III, Part I, remark (d). The exact one-year rate is (1,129.35 / 1,000) − 1 = 12.935%.
        var result = _calculator.Calculate(new AprcRequest([Flow(1_000m)], [Flow(1_129.35m, 12)]));

        Assert.True(result.IsSuccess);
        Assert.InRange(result.Value.Rate, 0.1293499999m, 0.1293500001m);
        Assert.Equal(12.94m, result.Value.Disclosed.Value);
    }

    [Fact]
    public void Calculate_ResidualToleranceLeavesSeveralDisclosureCutoffs_UsesTheCashFlows()
    {
        // The immediate payment leaves 1,000 to discount. The exact rate is still 12.935%.
        const decimal drawdown = 1_000_000_000_000m;
        var result = _calculator.Calculate(new AprcRequest([Flow(drawdown)], [Flow(drawdown - 1_000m), Flow(1_129.35m, 12)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(12.94m, result.Value.Disclosed.Value);
    }

    [Theory]
    [InlineData(-1, 1293)]
    [InlineData(0, 1294)]
    [InlineData(1, 1294)]
    public void Calculate_YearlyPaymentNearMidpoint_DisclosesCorrectSide(int centsFromTie, int disclosedHundredths)
    {
        // Scale the 12.935% case by 10^6, then vary one cent to separate rates only 1e-11 apart.
        const decimal drawdown = 1_000_000_000m;
        var payment = drawdown * 1.12935m + centsFromTie / 100m;
        var result = _calculator.Calculate(new AprcRequest([Flow(drawdown)], [Flow(payment, 12)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(disclosedHundredths / 100m, result.Value.Disclosed.Value);
    }

    [Theory]
    [InlineData(68, 1293)]
    [InlineData(69, 1294)]
    public void Calculate_FractionalYearPaymentNearMidpoint_DisclosesCorrectSide(int cents, int disclosedHundredths)
    {
        // Annex III present-value equation: 10^9 × (1 + 0.12935)^(18/12) lies strictly between the two payments.
        var result = _calculator.Calculate(new AprcRequest([Flow(1_000_000_000m)], [Flow(1_200_170_186m + cents / 100m, 18)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(disclosedHundredths / 100m, result.Value.Disclosed.Value);
    }

    [Fact]
    public void Calculate_RepaymentEqualsDrawdown_ReturnsZeroRate()
    {
        var result = _calculator.Calculate(new AprcRequest([Flow(1_000m)], [Flow(1_000m, 12)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(new AprcResult(0m, Percentage.Zero, 0, 0m), result.Value);
    }

    [Fact]
    public void Calculate_UpfrontChargeAndUnorderedPayments_IncreasesRate()
    {
        var drawdown = Flow(1_000m);
        var firstPayment = Flow(600m, 12);
        var lastPayment = Flow(600m, 24);
        var withoutCharge = _calculator.Calculate(new AprcRequest([drawdown], [firstPayment, lastPayment]));
        var withCharge = _calculator.Calculate(new AprcRequest([drawdown], [lastPayment, firstPayment, Flow(50m)]));

        Assert.True(withoutCharge.IsSuccess);
        Assert.True(withCharge.IsSuccess);
        Assert.True(withCharge.Value.Rate > withoutCharge.Value.Rate);
    }

    [Fact]
    public void Calculate_NoDrawdown_ReturnsValidationError() =>
        AssertValidationError(new AprcRequest([], [Flow(1_200m, 18)]));

    [Fact]
    public void Calculate_MultipleDrawdowns_ReturnsValidationError() =>
        AssertValidationError(new AprcRequest([Flow(950m), Flow(50m, 3)], [Flow(1_200m, 18)]));

    [Fact]
    public void Calculate_NonPositiveDrawdown_ReturnsValidationError() =>
        AssertValidationError(new AprcRequest([Flow(0m)], [Flow(1_200m, 18)]));

    [Fact]
    public void Calculate_NoBorrowerPayments_ReturnsValidationError() =>
        AssertValidationError(new AprcRequest([Flow(1_000m)], []));

    [Fact]
    public void Calculate_MixedCurrencies_ReturnsValidationError() =>
        AssertValidationError(new AprcRequest([Flow(1_000m)], [Flow(1_200m, 18, "GBP")]));

    [Fact]
    public void Calculate_PaymentBeforeDrawdown_ReturnsValidationError() =>
        AssertValidationError(new AprcRequest([Flow(1_000m)], [Flow(1_200m, -18)]));

    [Fact]
    public void Calculate_NegativePayment_ReturnsValidationError() =>
        AssertValidationError(new AprcRequest([Flow(1_000m)], [Flow(-50m), Flow(1_200m, 18)]));

    [Fact]
    public void Calculate_PaymentsTotalLessThanDrawdown_ReturnsValidationError() =>
        AssertValidationError(new AprcRequest([Flow(1_000m)], [Flow(950m, 12)]));

    [Fact]
    public void Calculate_RootAboveMaximum_ReturnsValidationError() =>
        AssertValidationError(new AprcRequest([Flow(1_000m)], [Flow(102_000m, 12)]));

    private static CashFlow Flow(decimal amount, int months = 0, string currency = "EUR") =>
        new(Money.Create(amount, currency).Value, DrawdownDate.PlusMonths(months));

    private void AssertValidationError(AprcRequest request)
    {
        var result = _calculator.Calculate(request);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationError>(result.Error);
    }
}
