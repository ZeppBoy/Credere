using Xunit;

namespace Credere.Core.Tests.DayCounts;

/// <summary>Reference data for the Actual/Actual (ISDA) convention.</summary>
public sealed class ActActIsdaTests
{
    /// <summary>Gets dates, expected year fractions, and published interest on £10,000 at 10%.</summary>
    // Published examples selected in docs/domain/day-counts.md from ISDA-BS:9951.1, November 1998, §4.
    // Fractions are the published expressions evaluated to 12 decimal places, with an exclusive end date.
    // https://www.isda.org/a/AIJEE/1998-ISDA-memo-EMU-and-Market-Conventions-Recent-Developments.pdf
    public static TheoryData<string, string, decimal, decimal> PublishedVectors => new()
    {
        { "2003-11-01", "2004-05-01", 0.497724380567m, 497.72m }, // p. 3, method comparison: 61/365 + 121/366.
        { "1999-02-01", "1999-07-01", 0.410958904110m, 410.96m }, // pp. 4–5, short first period: 150/365.
        { "1999-07-01", "2000-07-01", 1.001377348604m, 1001.38m }, // pp. 4–5, second period: 184/365 + 182/366.
        { "2002-08-15", "2003-07-15", 0.915068493151m, 915.07m }, // p. 6, long first period: 334/365.
    };

    /// <summary>Gets dates and expected year fractions for additional calendar cases.</summary>
    // Formula-derived from ISDA 2006 Definitions §4.16(b). Fractions are shown to 12 decimal places.
    // https://www.isda.org/a/smMDE/Blackline-2000-v-2006-ISDA-Definitions.pdf
    public static TheoryData<string, string, decimal> FormulaDerivedVectors => new()
    {
        { "2024-02-29", "2024-02-29", 0.000000000000m }, // Empty interval: 0/366.
        { "2023-02-28", "2023-03-01", 0.002739726027m }, // Non-leap February end: 1/365.
        { "2024-02-28", "2024-02-29", 0.002732240437m }, // Excludes 29 Feb: 1/366.
        { "2024-02-29", "2024-03-01", 0.002732240437m }, // Includes 29 Feb: 1/366.
        { "2024-02-28", "2024-03-01", 0.005464480874m }, // Includes 28 and 29 Feb: 2/366.
        { "2024-01-01", "2024-02-01", 0.084699453552m }, // Full Jan before leap day: 31/366.
        { "2024-03-01", "2024-04-01", 0.084699453552m }, // Full Mar after leap day: 31/366.
        { "1900-02-28", "1900-03-01", 0.002739726027m }, // Non-leap century: 1/365.
        { "2000-02-28", "2000-03-01", 0.005464480874m }, // Leap century: 2/366.
        { "2023-12-31", "2024-01-01", 0.002739726027m }, // Excludes the first day of a leap year: 1/365.
        { "2024-12-31", "2025-01-01", 0.002732240437m }, // Excludes the first day of a common year: 1/366.
        { "2023-12-31", "2024-01-02", 0.005471966465m }, // 31 Dec and 1 Jan: 1/365 + 1/366.
        { "2024-12-31", "2025-01-02", 0.005471966465m }, // 31 Dec and 1 Jan: 1/366 + 1/365.
        { "2023-01-01", "2024-01-01", 1.000000000000m }, // Full common year: 365/365.
        { "2024-01-01", "2025-01-01", 1.000000000000m }, // Full leap year: 366/366.
        { "2023-01-01", "2026-01-01", 3.000000000000m }, // 730/365 + 366/366.
        { "2023-12-15", "2025-01-15", 1.084931506849m }, // (17 Dec days + 14 Jan days)/365 + 366/366.
        { "1999-07-01", "2004-07-01", 5.001377348604m }, // (184 + 3 × 365)/365 + (366 + 182)/366.
    };
}
