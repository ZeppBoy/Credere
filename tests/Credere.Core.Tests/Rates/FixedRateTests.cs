using Xunit;

namespace Credere.Core.Tests.Rates;

/// <summary>
/// Reference data for <c>FixedRate</c> and <c>IInterestCalculator.Calculate</c> (docs/domain/rates.md, "FixedRate").
/// The tests that use it come with the implementation, after the owner has approved these vectors.
/// </summary>
/// <remarks>
/// <para>
/// Formula under test: <c>interest = balance × (nominalRate / 100) × dayCountFraction(periodStart, periodEnd)</c>,
/// computed in <c>decimal</c> and rounded once, to two places with <c>MidpointRounding.ToEven</c>, when the
/// <c>Money</c> result is created. The period includes <c>periodStart</c> and excludes <c>periodEnd</c>.
/// </para>
/// <para>
/// Sources: (1) the four published rows are the Actual/Actual (ISDA) worked examples in ISDA, "EMU and Market
/// Conventions: Recent Developments" (memorandum ISDA-BS:9951.1, November 1998), each on £10,000 at 10%, with the
/// interest the memo prints, https://www.isda.org/a/AIJEE/1998-ISDA-memo-EMU-and-Market-Conventions-Recent-Developments.pdf ;
/// (2) the day-count fractions come from ISDA 2006 Definitions §4.16(b), (d), (e), (f) and (g), as specified in
/// docs/domain/day-counts.md; (3) the rounding rule is docs/domain/value-objects.md, "Money" (Microsoft Learn,
/// "MidpointRounding Enum"; IEEE 754-2019 §4.3.1 roundTiesToEven). The other rows are formula-derived, with the
/// arithmetic in each comment. Every expected value was computed independently with exact rational arithmetic
/// (Python <c>fractions</c>) and rounded half-even once at the end, not by the implementation.
/// </para>
/// </remarks>
public sealed class FixedRateTests
{
    /// <summary>
    /// Gets nominal rate in percent, convention name, balance, currency, ISO start date, ISO end date (exclusive),
    /// and expected interest (published rows).
    /// </summary>
    public static TheoryData<decimal, string, decimal, string, string, string, decimal> PublishedVectors => new()
    {
        { 10m, "ACT/ACT (ISDA)", 10000.00m, "GBP", "2003-11-01", "2004-05-01", 497.72m }, // p. 3: 61/365 + 121/366, memo £497.72.
        { 10m, "ACT/ACT (ISDA)", 10000.00m, "GBP", "1999-02-01", "1999-07-01", 410.96m }, // pp. 4-5: 150/365, memo £410.96.
        { 10m, "ACT/ACT (ISDA)", 10000.00m, "GBP", "1999-07-01", "2000-07-01", 1001.38m }, // pp. 4-5: 184/365 + 182/366, memo £1,001.38.
        { 10m, "ACT/ACT (ISDA)", 10000.00m, "GBP", "2002-08-15", "2003-07-15", 915.07m }, // p. 6: 334/365, memo £915.07.
    };

    /// <summary>
    /// Gets nominal rate in percent, convention name, balance, currency, ISO start date, ISO end date (exclusive),
    /// and expected interest (formula-derived rows).
    /// </summary>
    public static TheoryData<decimal, string, decimal, string, string, string, decimal> FormulaDerivedVectors => new()
    {
        { 5m, "ACT/360", 100000.00m, "EUR", "2024-01-01", "2024-02-01", 430.56m }, // 31 days: 100000 x 0.05 x 31/360.
        { 5m, "ACT/365 Fixed", 100000.00m, "EUR", "2024-01-01", "2024-02-01", 424.66m }, // 31 days: 100000 x 0.05 x 31/365.
        { 5m, "ACT/ACT (ISDA)", 100000.00m, "EUR", "2024-01-01", "2024-02-01", 423.5m }, // 31 days in a leap year: 100000 x 0.05 x 31/366.
        { 5m, "30/360 ISDA", 100000.00m, "EUR", "2024-01-01", "2024-02-01", 416.67m }, // 30 days: 100000 x 0.05 x 30/360.
        { 5m, "30E/360", 100000.00m, "EUR", "2024-01-01", "2024-02-01", 416.67m }, // 30 days: 100000 x 0.05 x 30/360.
        { 6m, "30/360 ISDA", 50000.00m, "EUR", "2024-01-29", "2024-03-31", 516.67m }, // D1 29, D2 31 kept: 62 days: 50000 x 0.06 x 62/360.
        { 6m, "30E/360", 50000.00m, "EUR", "2024-01-29", "2024-03-31", 508.33m }, // D2 31 becomes 30: 61 days: 50000 x 0.06 x 61/360.
        { 4.5m, "ACT/365 Fixed", 25000.00m, "EUR", "2023-12-15", "2025-01-15", 1223.63m }, // year-end crossing, leap 2024: 397 days: 25000 x 0.045 x 397/365.
        { 3.75m, "ACT/360", 1000000.00m, "USD", "2024-02-28", "2024-03-01", 208.33m }, // leap day inside: 2 days: 1000000 x 0.0375 x 2/360.
        { 12m, "ACT/ACT (ISDA)", 10000.00m, "GBP", "2023-01-01", "2025-01-01", 2400m }, // two whole years: 10000 x 0.12 x 2.
        { 100m, "ACT/365 Fixed", 12345.67m, "EUR", "2023-01-01", "2024-01-01", 12345.67m }, // 100% for 365 days returns the balance.
        { 0m, "ACT/360", 12345.67m, "EUR", "2024-01-01", "2024-07-01", 0m }, // zero rate.
        { 7.25m, "ACT/360", 0.00m, "EUR", "2024-01-01", "2024-07-01", 0m }, // zero balance.
        { 7.25m, "30/360 ISDA", 9999.99m, "EUR", "2024-05-10", "2024-05-10", 0m }, // empty period.
        { 8m, "ACT/360", 1.00m, "EUR", "2024-01-01", "2024-12-31", 0.08m }, // tiny balance: 1 x 0.08 x 365/360 = 0.0811.
        { 2.5m, "ACT/ACT (ISDA)", -5000.00m, "EUR", "2024-06-01", "2024-09-01", -31.42m }, // negative balance gives negative interest: -5000 x 0.025 x 92/366.
        { 1m, "ACT/360", 100.00m, "EUR", "2024-01-01", "2024-01-28", 0.08m }, // exact tie 7.5c, floor 7 odd, rounds up: 100.00 x 1% x 27/360 = 0.075.
        { 1.5m, "ACT/365 Fixed", 73.00m, "EUR", "2024-01-01", "2024-01-06", 0.02m }, // exact tie 1.5c, floor odd, rounds up: 73.00 x 1.5% x 5/365.
        { 5m, "ACT/360", 100.00m, "EUR", "2024-01-01", "2024-01-10", 0.12m }, // exact tie 12.5c, floor 12 even, rounds down: 100.00 x 5% x 9/360 = 0.125.
    };

    // Other cases the tests must cover (docs/domain/rates.md, "FixedRate", "IInterestCalculator", "Testing"):
    //   FixedRate.Create rejects a nominal rate above 100% with a ValidationError; 0% and 100% are accepted.
    //   Calculate throws ArgumentOutOfRangeException when periodEnd precedes periodStart.
    //   The result is in the balance's currency.
    //   Properties: zero balance gives zero; an empty period gives zero; interest(2b) is within one cent of
    //   2 x interest(b), because each side rounds once.
}
