using Xunit;

namespace Credere.Core.Tests.DayCounts;

/// <summary>Reference data for the Actual/365 Fixed convention.</summary>
public sealed class Act365FixedTests
{
    /// <summary>Gets ISO start dates, exclusive end dates, and expected year fractions.</summary>
    // Formula-derived from ISDA 2006 Definitions §4.16(d), as shown in ISDA's 2000–2006 blackline:
    // https://www.isda.org/a/smMDE/Blackline-2000-v-2006-ISDA-Definitions.pdf
    // Each row counts actual calendar days and divides by 365. Fractions are shown to 12 decimal places.
    public static TheoryData<string, string, decimal> ReferenceVectors => new()
    {
        { "2024-02-29", "2024-02-29", 0.000000000000m }, // Empty interval: 0 / 365.
        { "2024-01-01", "2024-01-02", 0.002739726027m }, // One ordinary day: 1 / 365.
        { "2024-01-31", "2024-02-01", 0.002739726027m }, // Last day of January: 1 / 365.
        { "2024-01-01", "2024-02-01", 0.084931506849m }, // Full January: 31 / 365.
        { "2024-04-01", "2024-05-01", 0.082191780822m }, // Full April: 30 / 365.
        { "2023-02-01", "2023-03-01", 0.076712328767m }, // Common-year February: 28 / 365.
        { "2024-02-01", "2024-03-01", 0.079452054795m }, // Leap-year February: 29 / 365.
        { "2024-02-28", "2024-03-01", 0.005479452055m }, // Includes both 28 and 29 Feb: 2 / 365.
        { "2024-02-29", "2024-03-01", 0.002739726027m }, // Includes 29 Feb at the start: 1 / 365.
        { "2024-02-28", "2024-02-29", 0.002739726027m }, // Excludes 29 Feb at the end: 1 / 365.
        { "2023-12-31", "2024-01-01", 0.002739726027m }, // Only 31 Dec: 1 / 365.
        { "2023-12-15", "2024-01-15", 0.084931506849m }, // (17 Dec days + 14 Jan days) / 365.
        { "1900-02-28", "1900-03-01", 0.002739726027m }, // Common century year: 1 / 365.
        { "2000-02-28", "2000-03-01", 0.005479452055m }, // Leap century year: 2 / 365.
        { "2023-01-01", "2023-12-27", 0.986301369863m }, // (334 Jan–Nov days + 26 Dec days) / 365.
        { "2023-01-01", "2024-01-01", 1.000000000000m }, // Full common year: 365 / 365.
        { "2024-01-01", "2025-01-01", 1.002739726027m }, // Full leap year: 366 / 365.
        { "2023-01-01", "2025-01-01", 2.002739726027m }, // (365 common-year days + 366 leap-year days) / 365.
        { "2003-11-01", "2004-05-01", 0.498630136986m }, // (61 Nov–Dec days + 121 Jan–Apr days) / 365.
        { "1999-07-01", "2000-07-01", 1.002739726027m }, // (184 Jul–Dec days + 182 Jan–Jun days) / 365.
        { "2020-02-29", "2024-02-29", 4.002739726027m }, // (307 days in 2020 + 3 * 365 + 59 days in 2024) / 365.
        { "2024-01-31", "2024-03-31", 0.164383561644m }, // (1 Jan day + 29 Feb days + 30 Mar days) / 365.
    };
}
