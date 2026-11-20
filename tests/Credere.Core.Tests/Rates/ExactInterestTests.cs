using Xunit;

namespace Credere.Core.Tests.Rates;

/// <summary>
/// Reference data for exact half-even rounding of simple interest (M1-22, docs/domain/rates.md, "IInterestCalculator").
/// The tests that use it come with the implementation, after the owner has approved these vectors.
/// </summary>
/// <remarks>
/// <para>
/// Rule under test: <c>interest = balance × (nominalRate / 100) × dayCountFraction</c> is evaluated exactly (as a rational
/// number) and rounded once to whole cents with <c>MidpointRounding.ToEven</c>. A product exactly halfway between two cents
/// goes to the even cent; any other product goes to the nearer cent, however small the distance from the halfway point.
/// The sign follows the balance, so a negative balance rounds the magnitude and keeps its sign.
/// </para>
/// <para>
/// Sources: the rounding rule is docs/domain/value-objects.md, "Money" (Microsoft Learn, "MidpointRounding Enum";
/// IEEE 754-2019 §4.3.1 roundTiesToEven); the day-count fractions are ISDA 2006 Definitions §4.16 as specified in
/// docs/domain/day-counts.md. The first five rows come from the M1-08 implementation note and the cross-vendor code review
/// (PR #40); they and their expected values are listed in issue #46. Every expected value
/// below was recomputed independently with exact rational arithmetic (Python <c>fractions</c>), not by the implementation.
/// </para>
/// </remarks>
public sealed class ExactInterestTests
{
    /// <summary>
    /// Gets nominal rate in percent, convention name, balance, currency, ISO start date, ISO end date (exclusive),
    /// and expected interest. The first five rows are the regressions listed in issue #46.
    /// </summary>
    public static TheoryData<decimal, string, decimal, string, string, string, decimal> ExactRoundingVectors => new()
    {
        // Exact tie with a repeating fraction: 1950.00 x 0.0006 x 20/360 = 0.065 exactly; floor 6 is even, so 0.06.
        // A plain decimal evaluation of 1950m * (0.06m / 100m) * (20m / 360m) gives 0.0650000000000000000000000001m on .NET 10, which
        // rounds up to 0.07 under plain half-even (the problem M1-08's tolerance worked around).
        { 0.06m, "ACT/360", 1950.00m, "EUR", "2024-01-01", "2024-01-21", 0.06m },

        // Just above a tie: 100.00 x 0.0500000000000000000000001 x 9/360 = 0.12500000000000000000000025 exactly, so 0.13.
        // The 1e-22 tolerance of the M1-08 implementation wrongly returns 0.12 (Codex P1 finding on PR #40).
        { 5.00000000000000000000001m, "ACT/360", 100.00m, "EUR", "2024-01-01", "2024-01-10", 0.13m },

        // Largest supported amount: decimal.MaxValue x 100% x 365/365 = decimal.MaxValue exactly (Codex P2 finding on PR #40:
        // the intermediate product overflowed).
        { 100m, "ACT/365 Fixed", decimal.MaxValue, "EUR", "2023-01-01", "2024-01-01", decimal.MaxValue },

        // Smallest representable rate percentage: decimal.MaxValue x 1e-30 = 0.079228162514264337593543950335 exactly, so 0.08.
        // The rate fraction 1e-30 itself is below decimal's 28-digit scale, so it must not be materialised as a decimal.
        { 0.0000000000000000000000000001m, "ACT/365 Fixed", decimal.MaxValue, "EUR", "2023-01-01", "2024-01-01", 0.08m },

        // Negative balance: the same exact tie as the first row, magnitude 0.065 rounds to 0.06, sign kept.
        { 0.06m, "ACT/360", -1950.00m, "EUR", "2024-01-01", "2024-01-21", -0.06m },

        // Added by the implementer, not in issue #46 (owner to accept or drop): just below the tie of the second row.
        // 100.00 x 0.0499999999999999999999999 x 9/360 = 0.12499999999999999999999975 exactly, so 0.12.
        { 4.99999999999999999999999m, "ACT/360", 100.00m, "EUR", "2024-01-01", "2024-01-10", 0.12m },
    };

    // Other cases the tests must cover (issue #46, "Acceptance"):
    //   Every existing owner-approved FixedRate and day-count vector keeps its value (FixedRateTests, day-count suites).
    //   A custom DayCountConvention that implements only the public Fraction method still works, using the decimal it returns.
    //   The old tests of the internal 1e-22 tolerance are replaced by tests of the rule above, after this vector set is approved.
    //   Public DayCountConvention.Fraction signature and return values are unchanged.
}
