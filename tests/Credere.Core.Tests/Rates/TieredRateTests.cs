using Xunit;

namespace Credere.Core.Tests.Rates;

/// <summary>Reference data for whole-balance and marginal tiered interest.</summary>
/// <remarks>
/// The USD tier table and example balances come from Regulation DD, 12 CFR part 1030, Appendix A,
/// Part I.D, Tiering Methods A and B. The expected amounts apply Credere's simple-interest rule
/// to those balances and rates. Periods are drawn from Credere's existing day-count and rate fixtures.
/// Day-count fractions follow ISDA 2006 Definitions §4.16(b), (d)–(g).
/// </remarks>
public sealed class TieredRateTests
{
    // Upper limits include the stated balance. The final rate applies above $15,000 without a cap.
    // https://www.consumerfinance.gov/rules-policy/regulations/1030/a/
    internal const decimal FirstUpperLimit = 2_500m;
    internal const decimal SecondUpperLimit = 15_000m;
    internal const decimal FirstRatePercent = 5.25m;
    internal const decimal SecondRatePercent = 5.50m;
    internal const decimal FinalRatePercent = 5.75m;

    /// <summary>Gets the USD balance, basis, dates, and expected interest in both tiering modes.</summary>
    // Apply the selected rate to the full balance in OnCurrentBalance mode. In Marginal mode, the
    // annual products for the first $2,500 and the next $12,500 are $131.25 and $687.50, respectively.
    // Multiply by the year fraction and round the total once to cents using MidpointRounding.ToEven.
    public static TheoryData<decimal, string, string, string, decimal, decimal> ReferenceVectors => new()
    {
        { 1_000m, "ACT/365 Fixed", "2023-01-01", "2024-01-01", 52.50m, 52.50m }, // 1,000 × 5.25%.
        { 2_500m, "ACT/365 Fixed", "2023-01-01", "2024-01-01", 131.25m, 131.25m }, // First limit is inclusive.
        { 2_500.01m, "ACT/365 Fixed", "2023-01-01", "2024-01-01", 137.50m, 131.25m }, // 2,500.01 × 5.50%; 131.25 + 0.01 × 5.50%.
        { 8_000m, "ACT/365 Fixed", "2023-01-01", "2024-01-01", 440.00m, 433.75m }, // 8,000 × 5.50%; 131.25 + 5,500 × 5.50%.
        { 15_000m, "ACT/365 Fixed", "2023-01-01", "2024-01-01", 825.00m, 818.75m }, // 15,000 × 5.50%; 131.25 + 687.50.
        { 15_000.01m, "ACT/365 Fixed", "2023-01-01", "2024-01-01", 862.50m, 818.75m }, // 15,000.01 × 5.75%; 818.75 + 0.01 × 5.75%.
        { 20_000m, "ACT/365 Fixed", "2023-01-01", "2024-01-01", 1_150.00m, 1_106.25m }, // 20,000 × 5.75%; 818.75 + 5,000 × 5.75%.

        { 15_000m, "ACT/360", "2024-01-01", "2024-01-21", 45.83m, 45.49m }, // 825 × 20/360; 818.75 × 20/360. Per-tier rounding would give 45.48.
        { 20_000m, "ACT/ACT (ISDA)", "2024-02-29", "2024-03-01", 3.14m, 3.02m }, // 1,150/366; 1,106.25/366. Per-tier rounding would give 3.03.
        { 20_000m, "30/360 ISDA", "2024-01-29", "2024-03-31", 198.06m, 190.52m }, // 1,150 × 62/360; 1,106.25 × 62/360.
        { 20_000m, "30E/360", "2024-01-29", "2024-03-31", 194.86m, 187.45m }, // 1,150 × 61/360; 1,106.25 × 61/360.
        { 20_000m, "ACT/365 Fixed", "2023-01-01", "2023-01-01", 0.00m, 0.00m }, // Both modes have zero interest for an empty interval.
    };
}
