using Xunit;

namespace Credere.Core.Tests.Rates;

/// <summary>Reference data for FloatingRate with one fixed reset rate.</summary>
/// <remarks>
/// Cases are formula-derived from ISDA 2006 Definitions §6.1(a), using the non-compounded floating amount
/// and the day-count fractions in §4.16(b), (d)–(g). Credere uses one reset rate for the full calculation period
/// in M1. The balance, date intervals and rates are drawn from the fixed-rate references and value-object cases.
/// </remarks>
public sealed class FloatingRateTests
{
    /// <summary>Gets the base and margin in percent units, convention name, balance, dates, and expected interest.</summary>
    // ISDA 2006 Definitions §6.1(a) and §4.16: https://www.isda.org/a/smMDE/Blackline-2000-v-2006-ISDA-Definitions.pdf
    // ISDA also reproduces §6.1(a) in its compounding memo §3.2: https://www.isda.org/a/2KiDE/isda-compounding-memo.pdf
    // Results are EUR amounts, rounded half-even to cents once after applying the base rate plus margin.
    public static TheoryData<decimal, decimal, string, decimal, string, string, decimal> ReferenceVectors => new()
    {
        { 3.75m, 1.5m, "ACT/360", 100_000m, "2024-01-01", "2024-02-01", 452.08m }, // 100,000 × 0.0525 × 31/360.
        { 3.75m, 1.5m, "ACT/365 Fixed", 100_000m, "2024-01-01", "2024-02-01", 445.89m }, // 100,000 × 0.0525 × 31/365.
        { 3.75m, 1.5m, "ACT/ACT (ISDA)", 100_000m, "2024-01-01", "2024-02-01", 444.67m }, // 100,000 × 0.0525 × 31/366.
        { 4.5m, 1.5m, "30/360 ISDA", 50_000m, "2024-01-29", "2024-03-31", 516.67m }, // 50,000 × 0.06 × 62/360.
        { 4.5m, 1.5m, "30E/360", 50_000m, "2024-01-29", "2024-03-31", 508.33m }, // 50,000 × 0.06 × 61/360.
        { 0m, 5.25m, "ACT/360", 100_000m, "2024-01-01", "2024-02-01", 452.08m }, // A zero base leaves the full margin.
        { 5.25m, 0m, "ACT/360", 100_000m, "2024-01-01", "2024-02-01", 452.08m }, // A zero margin leaves the full base.
        { 0m, 0m, "ACT/360", 100_000m, "2024-01-01", "2024-02-01", 0.00m }, // Zero nominal rate.
        { 1m, 0.5m, "ACT/365 Fixed", 73m, "2024-01-01", "2024-01-06", 0.02m }, // 73 × 0.015 × 5/365 is exactly 0.015.
        // Split the rate from the exact-rounding counterexample in PR #40 into base and margin.
        // 100 × (5 + 1e-23)/100 × 9/360 is 0.12500000000000000000000025 and rounds up.
        { 5m, 0.00000000000000000000001m, "ACT/360", 100m, "2024-01-01", "2024-01-10", 0.13m },
    };
}
