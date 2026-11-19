using Xunit;

namespace Credere.Core.Tests.DayCounts;

/// <summary>Reference data for the Actual/360 convention.</summary>
public sealed class Act360Tests
{
    /// <summary>Gets ISO start dates, exclusive end dates, and expected year fractions.</summary>
    // Formula-derived from ISDA 2006 Definitions §4.16(e), as shown in ISDA's 2000–2006 blackline:
    // https://www.isda.org/a/smMDE/Blackline-2000-v-2006-ISDA-Definitions.pdf
    // Each row counts actual calendar days and divides by 360. Fractions are shown to 12 decimal places.
    public static TheoryData<string, string, decimal> ReferenceVectors => new()
    {
        { "2024-02-29", "2024-02-29", 0.000000000000m }, // Empty interval: 0 / 360.
        { "2024-01-01", "2024-01-02", 0.002777777778m }, // One ordinary day: 1 / 360.
        { "2024-01-31", "2024-02-01", 0.002777777778m }, // Last day of January: 1 / 360.
        { "2024-01-01", "2024-02-01", 0.086111111111m }, // Full January: 31 / 360.
        { "2024-04-01", "2024-05-01", 0.083333333333m }, // Full April: 30 / 360.
        { "2023-02-01", "2023-03-01", 0.077777777778m }, // Common-year February: 28 / 360.
        { "2024-02-01", "2024-03-01", 0.080555555556m }, // Leap-year February: 29 / 360.
        { "2024-02-28", "2024-03-01", 0.005555555556m }, // Includes both 28 and 29 Feb: 2 / 360.
        { "2024-02-29", "2024-03-01", 0.002777777778m }, // Includes 29 Feb at the start: 1 / 360.
        { "2024-02-28", "2024-02-29", 0.002777777778m }, // Excludes 29 Feb at the end: 1 / 360.
        { "2023-12-31", "2024-01-01", 0.002777777778m }, // Only 31 Dec: 1 / 360.
        { "2023-12-15", "2024-01-15", 0.086111111111m }, // (17 Dec days + 14 Jan days) / 360.
        { "1900-02-28", "1900-03-01", 0.002777777778m }, // Common century year: 1 / 360.
        { "2000-02-28", "2000-03-01", 0.005555555556m }, // Leap century year: 2 / 360.
        { "2023-01-01", "2023-12-27", 1.000000000000m }, // (334 Jan–Nov days + 26 Dec days) / 360.
        { "2023-01-01", "2024-01-01", 1.013888888889m }, // Full common year: 365 / 360.
        { "2024-01-01", "2025-01-01", 1.016666666667m }, // Full leap year: 366 / 360.
        { "2023-01-01", "2025-01-01", 2.030555555556m }, // (365 common-year days + 366 leap-year days) / 360.
        { "2003-11-01", "2004-05-01", 0.505555555556m }, // (61 Nov–Dec days + 121 Jan–Apr days) / 360.
        { "1999-07-01", "2000-07-01", 1.016666666667m }, // (184 Jul–Dec days + 182 Jan–Jun days) / 360.
        { "2020-02-29", "2024-02-29", 4.058333333333m }, // (307 days in 2020 + 3 * 365 + 59 days in 2024) / 360.
        { "2024-01-31", "2024-03-31", 0.166666666667m }, // (1 Jan day + 29 Feb days + 30 Mar days) / 360.
    };
}
