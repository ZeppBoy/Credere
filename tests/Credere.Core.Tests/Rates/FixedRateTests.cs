using Xunit;

namespace Credere.Core.Tests.Rates;

/// <summary>
/// Fixed-rate interest reference data from ISDA memorandum ISDA-BS:9951.1, §4, pp. 3–6,
/// and cases derived from ISDA 2006 Definitions §4.16(b).
/// </summary>
public sealed class FixedRateTests
{
    private const decimal Balance = 10_000m;
    private const string Currency = "GBP";
    private const decimal NominalPercent = 10m;

    /// <summary>Gets the calculation dates, balance, currency, nominal percent, and published interest.</summary>
    // ISDA-BS:9951.1, November 1998, §4.
    // https://www.isda.org/a/AIJEE/1998-ISDA-memo-EMU-and-Market-Conventions-Recent-Developments.pdf
    public static TheoryData<string, string, decimal, string, decimal, decimal> PublishedVectors => new()
    {
        { "2003-11-01", "2004-05-01", Balance, Currency, NominalPercent, 497.72m }, // p. 3, method comparison: 61/365 + 121/366.
        { "1999-02-01", "1999-07-01", Balance, Currency, NominalPercent, 410.96m }, // pp. 4–5, short first period: 150/365.
        { "1999-07-01", "2000-07-01", Balance, Currency, NominalPercent, 1_001.38m }, // pp. 4–5, second period: 184/365 + 182/366.
        { "2002-08-15", "2003-07-15", Balance, Currency, NominalPercent, 915.07m }, // p. 6, long first period: 334/365.
    };

    /// <summary>Gets the calculation dates, balance, currency, nominal percent, and derived interest.</summary>
    // Formula-derived from ISDA 2006 Definitions §4.16(b), using dates from ActActIsdaTests.
    // https://www.isda.org/a/smMDE/Blackline-2000-v-2006-ISDA-Definitions.pdf
    // Apply the memo's £10,000 balance and 10% rate, then round the total interest once with MidpointRounding.ToEven.
    public static TheoryData<string, string, decimal, string, decimal, decimal> FormulaDerivedVectors => new()
    {
        { "2024-02-29", "2024-02-29", Balance, Currency, NominalPercent, 0.00m }, // Empty interval: 1,000 × 0/366.
        { "2023-02-28", "2023-03-01", Balance, Currency, NominalPercent, 2.74m }, // Common-year February: 1,000 × 1/365.
        { "2024-02-28", "2024-03-01", Balance, Currency, NominalPercent, 5.46m }, // Includes 29 February: 1,000 × 2/366.
        { "2023-12-31", "2024-01-02", Balance, Currency, NominalPercent, 5.47m }, // Common to leap year: 1,000 × (1/365 + 1/366).
        { "2023-12-15", "2025-01-15", Balance, Currency, NominalPercent, 1_084.93m }, // 1,000 × ((17 + 14)/365 + 366/366).
    };
}
