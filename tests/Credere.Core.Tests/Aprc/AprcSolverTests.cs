using Credere.Core.Aprc;
using Credere.Core.Results;
using Xunit;

namespace Credere.Core.Tests.Aprc;

/// <summary>Tests the APRC solver against the worked examples in Directive 98/7/EC, Annex II.</summary>
public sealed class AprcSolverTests
{
    /// <summary>
    /// Gets the example ID, initial drawdown, payment amounts, payment times in years, printed rate,
    /// and one unit of the last printed rate digit for each worked example.
    /// </summary>
    // Directive 98/7/EC, Annex II, Parts A and B, OJ L 101, 1 April 1998, pp. 20–23.
    // https://eur-lex.europa.eu/eli/dir/1998/7/oj/eng
    // Part A uses the offsets in the published calendar equations. Part B uses equal months.
    // The tolerance accommodates examples such as A3, where the printed rate was truncated.
    public static TheoryData<string, decimal, decimal[], decimal[], decimal, decimal> ReferenceVectors => new()
    {
        { "A1", 1_000m, [1_200m], [546m / 365m], 0.1296204m, 0.0000001m }, // p. 20, first example.
        { "A2", 950m, [1_200m], [546m / 365m], 0.169026m, 0.000001m }, // p. 20, second example.
        { "A3", 1_000m, [600m, 600m], [1m, 730m / 365m], 0.1306623m, 0.0000001m }, // pp. 20–21, third example.
        { "A4", 1_000m, [272m, 272m, 544m], [90m / 365m, 181m / 365m, 1m], 0.13226m, 0.00001m }, // p. 21, fourth example.
        { "B1", 1_000m, [1_200m], [18m / 12m], 0.129243m, 0.000001m }, // p. 21, first example.
        { "B2", 950m, [1_200m], [18m / 12m], 0.168526m, 0.000001m }, // p. 22, second example.
        { "B3", 1_000m, [600m, 600m], [12m / 12m, 24m / 12m], 0.13066m, 0.00001m }, // p. 22, third example.
        { "B4", 1_000m, [272m, 272m, 544m], [3m / 12m, 6m / 12m, 12m / 12m], 0.13185m, 0.00001m }, // pp. 22–23, fourth example.
    };

    [Theory]
    [MemberData(nameof(ReferenceVectors))]
    public void Solve_AnnexIIExample_MatchesPrintedRate(
        string example,
        decimal drawdown,
        decimal[] amounts,
        decimal[] times,
        decimal printedRate,
        decimal tolerance)
    {
        var result = AprcSolver.Solve(drawdown, amounts.Zip(times).ToArray());

        Assert.True(result.IsSuccess, $"Annex II example {example} must have a non-negative APRC.");
        Assert.InRange(result.Value.Rate, printedRate - tolerance, printedRate + tolerance);
        Assert.InRange(result.Value.Iterations, 1, 200);
        Assert.InRange(Math.Abs(result.Value.FinalResidual), 0m, amounts.Sum() * 0.0000000001m);
    }

    [Fact]
    public void Solve_PaymentsTotalDrawdown_ReturnsZeroRate()
    {
        var result = AprcSolver.Solve(1_000m, [(600m, 1m), (400m, 2m)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(new AprcSolver.Solution(0m, 0, 0m, 0m), result.Value);
    }

    [Fact]
    public void Solve_RootAboveInitialBracket_WidensBracket()
    {
        // With one payment after a year, Annex III gives X = payment / drawdown − 1.
        var result = AprcSolver.Solve(1_000m, [(4_000m, 1m)]);

        Assert.True(result.IsSuccess);
        Assert.InRange(result.Value.Rate, 2.9999999999m, 3.0000000001m);
    }

    [Fact]
    public void Solve_RootAtMaximumRate_ReturnsMaximum()
    {
        // Annex III: a one-year payment of 101 times the drawdown gives X = 100.
        var result = AprcSolver.Solve(1_000m, [(101_000m, 1m)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Value.Rate);
    }

    [Fact]
    public void Solve_CombinedPaymentsExceedDecimalRange_FindsRoot()
    {
        // Two one-year payments, each equal to the drawdown, give X = 1.
        var result = AprcSolver.Solve(decimal.MaxValue, [(decimal.MaxValue, 1m), (decimal.MaxValue, 1m)]);

        Assert.True(result.IsSuccess);
        Assert.InRange(result.Value.Rate, 0.9999999999m, 1m);
    }

    [Theory]
    [InlineData(950, 1)] // Total payments fall below the drawdown.
    [InlineData(102_000, 1)] // A payment after one year gives X = 101.
    public void Solve_NoRootInSupportedRange_ReturnsValidationError(int amount, int years)
    {
        var result = AprcSolver.Solve(1_000m, [(amount, years)]);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public void Solve_OnlyImmediatePaymentExceedsDrawdown_ReturnsValidationError()
    {
        // All cash flows are at time zero, so discounting cannot close even a one-cent gap.
        const decimal drawdown = 1_000_000_000_000m;
        var result = AprcSolver.Solve(drawdown, [(drawdown + 0.01m, 0m)]);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public void Solve_ImmediatePaymentCoversDrawdownAndLaterPaymentRemains_ReturnsValidationError()
    {
        var result = AprcSolver.Solve(1_000m, [(1_000m, 0m), (600m, 50m)]);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationError>(result.Error);
    }
}
