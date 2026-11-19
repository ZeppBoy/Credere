using Xunit;

namespace Credere.Core.Tests.Aprc;

/// <summary>Reference data for the APRC solver.</summary>
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
}
