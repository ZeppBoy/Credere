using Credere.Core.Schedules;

namespace Credere.Core.Tests.Schedules;

/// <summary>
/// Reference schedules for <c>AnnuityScheduleGenerator</c> (docs/domain/schedules.md, "1. Annuity"). Each vector is a
/// complete schedule: every row's date and amounts. They were written before the generator, and every amount was
/// computed independently of Credere's code.
/// </summary>
/// <remarks>
/// <para>
/// Rule under test: <c>payment = P x r x (1+r)^n / ((1+r)^n - 1)</c> with <c>r = nominalRate / periodsPerYear</c>, rounded
/// once to cents half-even; rows 1..n-1 pay that instalment, with interest from the day-count convention on the opening
/// balance and the rest as principal; the last row pays its interest plus the whole remaining balance. A zero rate uses
/// <c>P / n</c>. Payment dates and interest periods are the ones of "Periods and interest".
/// </para>
/// <para>
/// Sources: (1) the annuity formula and the residual placement are docs/domain/schedules.md, "1. Annuity" and "Rounding and
/// residuals" (the placement is a Credere design decision, with no regulatory source); (2) the day-count fractions are
/// ISDA 2006 Definitions §4.16(b), (f), (g), Act/360 and Act/365 Fixed, as specified in docs/domain/day-counts.md;
/// (3) the interest rounding is docs/domain/value-objects.md, "Money" (IEEE 754-2019 §4.3.1 roundTiesToEven). No
/// published amortization table exists for these conventions, so the values are not tied to an external table. The one
/// external check is that the 250,000 x 12% x 360 vector reproduces the regular payment 2,571.53 and the last payment
/// 2,576.59 that "Rounding and residuals" states.
/// </para>
/// <para>
/// Method: a Python script with exact rational arithmetic (<c>fractions.Fraction</c>), day counts coded from the ISDA
/// definitions with <c>datetime</c> and <c>calendar</c>, and half-even rounding once per amount. It checked that no instalment of the first 14 schedules lies within 1e-9 cents of a rounding tie. That check never covered an exact tie, which is why the cross-vendor review found the rounding defect; <c>InstalmentTie_roundsDown_2m</c> and <c>InstalmentTie_roundsUp_2m</c> were added for it, and the generator now rounds the exact ratio. The arithmetic of rows 1, 2 and the last is in the comment on each vector.
/// </para>
/// </remarks>
internal static partial class AnnuityScheduleGeneratorVectors
{
    /// <summary>Gets every vector.</summary>
    public static IReadOnlyList<AnnuityScheduleGeneratorVector> All { get; } = [.. ShortCases(), .. LongCases()];

    private static AnnuityScheduleGeneratorRow R(
        string date, decimal opening, decimal interest, decimal principal, decimal total, decimal closing) =>
        new(date, opening, interest, principal, total, closing);

    private static AnnuityScheduleGeneratorVector[] ShortCases() =>
    [
        // Level30E360_24m: 10000.00 EUR at 5% (30E/360), 24 months, monthly, disbursed 2026-01-15, first payment 2026-02-15.
        // Instalment: P x r x (1+r)^24 / ((1+r)^24 - 1), r = 5% / 12 = 0.004166666667: 438.713897 -> 438.71 (nearest half-cent tie is 0.1103 cents away).
        // Row 1: interest 10000.00 x 5% x 1/12 = 41.666667 -> 41.67; principal 438.71 - 41.67 = 397.04; closing 9602.96.
        // Row 2: interest 9602.96 x 5% x 1/12 = 40.012333 -> 40.01; principal 438.71 - 40.01 = 398.70; closing 9204.26.
        // Row 24 (last): interest 436.99 x 5% x 1/12 = 1.820792 -> 1.82; principal = opening 436.99; total 438.81.
        // Half-cent ties in this schedule (half-even): row 12: exact 23.085 -> 23.08.
        new(
            "Level30E360_24m", 10000.00m, 5m, "30E/360", 24, PaymentFrequency.Monthly,
            "2026-01-15", "2026-02-15", 438.71m,
            [
                R("2026-02-15", 10000.00m, 41.67m, 397.04m, 438.71m, 9602.96m),
                R("2026-03-15", 9602.96m, 40.01m, 398.70m, 438.71m, 9204.26m),
                R("2026-04-15", 9204.26m, 38.35m, 400.36m, 438.71m, 8803.90m),
                R("2026-05-15", 8803.90m, 36.68m, 402.03m, 438.71m, 8401.87m),
                R("2026-06-15", 8401.87m, 35.01m, 403.70m, 438.71m, 7998.17m),
                R("2026-07-15", 7998.17m, 33.33m, 405.38m, 438.71m, 7592.79m),
                R("2026-08-15", 7592.79m, 31.64m, 407.07m, 438.71m, 7185.72m),
                R("2026-09-15", 7185.72m, 29.94m, 408.77m, 438.71m, 6776.95m),
                R("2026-10-15", 6776.95m, 28.24m, 410.47m, 438.71m, 6366.48m),
                R("2026-11-15", 6366.48m, 26.53m, 412.18m, 438.71m, 5954.30m),
                R("2026-12-15", 5954.30m, 24.81m, 413.90m, 438.71m, 5540.40m),
                R("2027-01-15", 5540.40m, 23.08m, 415.63m, 438.71m, 5124.77m),
                R("2027-02-15", 5124.77m, 21.35m, 417.36m, 438.71m, 4707.41m),
                R("2027-03-15", 4707.41m, 19.61m, 419.10m, 438.71m, 4288.31m),
                R("2027-04-15", 4288.31m, 17.87m, 420.84m, 438.71m, 3867.47m),
                R("2027-05-15", 3867.47m, 16.11m, 422.60m, 438.71m, 3444.87m),
                R("2027-06-15", 3444.87m, 14.35m, 424.36m, 438.71m, 3020.51m),
                R("2027-07-15", 3020.51m, 12.59m, 426.12m, 438.71m, 2594.39m),
                R("2027-08-15", 2594.39m, 10.81m, 427.90m, 438.71m, 2166.49m),
                R("2027-09-15", 2166.49m, 9.03m, 429.68m, 438.71m, 1736.81m),
                R("2027-10-15", 1736.81m, 7.24m, 431.47m, 438.71m, 1305.34m),
                R("2027-11-15", 1305.34m, 5.44m, 433.27m, 438.71m, 872.07m),
                R("2027-12-15", 872.07m, 3.63m, 435.08m, 438.71m, 436.99m),
                R("2028-01-15", 436.99m, 1.82m, 436.99m, 438.81m, 0.00m),
            ]),
        // Act360_12m: 10000.00 EUR at 5% (ACT/360), 12 months, monthly, disbursed 2026-01-01, first payment 2026-02-01.
        // Instalment: P x r x (1+r)^12 / ((1+r)^12 - 1), r = 5% / 12 = 0.004166666667: 856.074818 -> 856.07 (nearest half-cent tie is 0.0182 cents away).
        // Row 1: interest 10000.00 x 5% x 31/360 = 43.055556 -> 43.06; principal 856.07 - 43.06 = 813.01; closing 9186.99.
        // Row 2: interest 9186.99 x 5% x 7/90 = 35.727183 -> 35.73; principal 856.07 - 35.73 = 820.34; closing 8366.65.
        // Row 12 (last): interest 855.20 x 5% x 31/360 = 3.682111 -> 3.68; principal = opening 855.20; total 858.88.
        new(
            "Act360_12m", 10000.00m, 5m, "ACT/360", 12, PaymentFrequency.Monthly,
            "2026-01-01", "2026-02-01", 856.07m,
            [
                R("2026-02-01", 10000.00m, 43.06m, 813.01m, 856.07m, 9186.99m),
                R("2026-03-01", 9186.99m, 35.73m, 820.34m, 856.07m, 8366.65m),
                R("2026-04-01", 8366.65m, 36.02m, 820.05m, 856.07m, 7546.60m),
                R("2026-05-01", 7546.60m, 31.44m, 824.63m, 856.07m, 6721.97m),
                R("2026-06-01", 6721.97m, 28.94m, 827.13m, 856.07m, 5894.84m),
                R("2026-07-01", 5894.84m, 24.56m, 831.51m, 856.07m, 5063.33m),
                R("2026-08-01", 5063.33m, 21.80m, 834.27m, 856.07m, 4229.06m),
                R("2026-09-01", 4229.06m, 18.21m, 837.86m, 856.07m, 3391.20m),
                R("2026-10-01", 3391.20m, 14.13m, 841.94m, 856.07m, 2549.26m),
                R("2026-11-01", 2549.26m, 10.98m, 845.09m, 856.07m, 1704.17m),
                R("2026-12-01", 1704.17m, 7.10m, 848.97m, 856.07m, 855.20m),
                R("2027-01-01", 855.20m, 3.68m, 855.20m, 858.88m, 0.00m),
            ]),
        // Act365Fixed_leapYear_12m: 10000.00 EUR at 6% (ACT/365 Fixed), 12 months, monthly, disbursed 2027-12-10, first payment 2028-01-10.
        // Instalment: P x r x (1+r)^12 / ((1+r)^12 - 1), r = 6% / 12 = 0.005: 860.664297 -> 860.66 (nearest half-cent tie is 0.0703 cents away).
        // Row 1: interest 10000.00 x 6% x 31/365 = 50.958904 -> 50.96; principal 860.66 - 50.96 = 809.70; closing 9190.30.
        // Row 2: interest 9190.30 x 6% x 31/365 = 46.832762 -> 46.83; principal 860.66 - 46.83 = 813.83; closing 8376.47.
        // Row 12 (last): interest 857.56 x 6% x 6/73 = 4.229063 -> 4.23; principal = opening 857.56; total 861.79.
        new(
            "Act365Fixed_leapYear_12m", 10000.00m, 6m, "ACT/365 Fixed", 12, PaymentFrequency.Monthly,
            "2027-12-10", "2028-01-10", 860.66m,
            [
                R("2028-01-10", 10000.00m, 50.96m, 809.70m, 860.66m, 9190.30m),
                R("2028-02-10", 9190.30m, 46.83m, 813.83m, 860.66m, 8376.47m),
                R("2028-03-10", 8376.47m, 39.93m, 820.73m, 860.66m, 7555.74m),
                R("2028-04-10", 7555.74m, 38.50m, 822.16m, 860.66m, 6733.58m),
                R("2028-05-10", 6733.58m, 33.21m, 827.45m, 860.66m, 5906.13m),
                R("2028-06-10", 5906.13m, 30.10m, 830.56m, 860.66m, 5075.57m),
                R("2028-07-10", 5075.57m, 25.03m, 835.63m, 860.66m, 4239.94m),
                R("2028-08-10", 4239.94m, 21.61m, 839.05m, 860.66m, 3400.89m),
                R("2028-09-10", 3400.89m, 17.33m, 843.33m, 860.66m, 2557.56m),
                R("2028-10-10", 2557.56m, 12.61m, 848.05m, 860.66m, 1709.51m),
                R("2028-11-10", 1709.51m, 8.71m, 851.95m, 860.66m, 857.56m),
                R("2028-12-10", 857.56m, 4.23m, 857.56m, 861.79m, 0.00m),
            ]),
        // ActActIsda_leapYear_12m: 15000.00 EUR at 4.8% (ACT/ACT (ISDA)), 12 months, monthly, disbursed 2027-11-15, first payment 2027-12-15.
        // Instalment: P x r x (1+r)^12 / ((1+r)^12 - 1), r = 4.8% / 12 = 0.004: 1282.737849 -> 1282.74 (nearest half-cent tie is 0.2849 cents away).
        // Row 1: interest 15000.00 x 4.8% x 6/73 = 59.178082 -> 59.18; principal 1282.74 - 59.18 = 1223.56; closing 13776.44.
        // Row 2: interest 13776.44 x 4.8% x 5666/66795 = 56.093283 -> 56.09; principal 1282.74 - 56.09 = 1226.65; closing 12549.79.
        // Row 12 (last): interest 1276.92 x 4.8% x 31/366 = 5.191412 -> 5.19; principal = opening 1276.92; total 1282.11.
        new(
            "ActActIsda_leapYear_12m", 15000.00m, 4.8m, "ACT/ACT (ISDA)", 12, PaymentFrequency.Monthly,
            "2027-11-15", "2027-12-15", 1282.74m,
            [
                R("2027-12-15", 15000.00m, 59.18m, 1223.56m, 1282.74m, 13776.44m),
                R("2028-01-15", 13776.44m, 56.09m, 1226.65m, 1282.74m, 12549.79m),
                R("2028-02-15", 12549.79m, 51.02m, 1231.72m, 1282.74m, 11318.07m),
                R("2028-03-15", 11318.07m, 43.05m, 1239.69m, 1282.74m, 10078.38m),
                R("2028-04-15", 10078.38m, 40.97m, 1241.77m, 1282.74m, 8836.61m),
                R("2028-05-15", 8836.61m, 34.77m, 1247.97m, 1282.74m, 7588.64m),
                R("2028-06-15", 7588.64m, 30.85m, 1251.89m, 1282.74m, 6336.75m),
                R("2028-07-15", 6336.75m, 24.93m, 1257.81m, 1282.74m, 5078.94m),
                R("2028-08-15", 5078.94m, 20.65m, 1262.09m, 1282.74m, 3816.85m),
                R("2028-09-15", 3816.85m, 15.52m, 1267.22m, 1282.74m, 2549.63m),
                R("2028-10-15", 2549.63m, 10.03m, 1272.71m, 1282.74m, 1276.92m),
                R("2028-11-15", 1276.92m, 5.19m, 1276.92m, 1282.11m, 0.00m),
            ]),
        // Thirty360Isda_monthEnd_12m: 20000.00 EUR at 7% (30/360 ISDA), 12 months, monthly, disbursed 2025-12-31, first payment 2026-01-31.
        // Instalment: P x r x (1+r)^12 / ((1+r)^12 - 1), r = 7% / 12 = 0.005833333333: 1730.534922 -> 1730.53 (nearest half-cent tie is 0.0078 cents away).
        // Row 1: interest 20000.00 x 7% x 1/12 = 116.666667 -> 116.67; principal 1730.53 - 116.67 = 1613.86; closing 18386.14.
        // Row 2: interest 18386.14 x 7% x 7/90 = 100.102318 -> 100.10; principal 1730.53 - 100.10 = 1630.43; closing 16755.71.
        // Row 12 (last): interest 1723.26 x 7% x 1/12 = 10.052350 -> 10.05; principal = opening 1723.26; total 1733.31.
        new(
            "Thirty360Isda_monthEnd_12m", 20000.00m, 7m, "30/360 ISDA", 12, PaymentFrequency.Monthly,
            "2025-12-31", "2026-01-31", 1730.53m,
            [
                R("2026-01-31", 20000.00m, 116.67m, 1613.86m, 1730.53m, 18386.14m),
                R("2026-02-28", 18386.14m, 100.10m, 1630.43m, 1730.53m, 16755.71m),
                R("2026-03-31", 16755.71m, 107.52m, 1623.01m, 1730.53m, 15132.70m),
                R("2026-04-30", 15132.70m, 88.27m, 1642.26m, 1730.53m, 13490.44m),
                R("2026-05-31", 13490.44m, 78.69m, 1651.84m, 1730.53m, 11838.60m),
                R("2026-06-30", 11838.60m, 69.06m, 1661.47m, 1730.53m, 10177.13m),
                R("2026-07-31", 10177.13m, 59.37m, 1671.16m, 1730.53m, 8505.97m),
                R("2026-08-31", 8505.97m, 49.62m, 1680.91m, 1730.53m, 6825.06m),
                R("2026-09-30", 6825.06m, 39.81m, 1690.72m, 1730.53m, 5134.34m),
                R("2026-10-31", 5134.34m, 29.95m, 1700.58m, 1730.53m, 3433.76m),
                R("2026-11-30", 3433.76m, 20.03m, 1710.50m, 1730.53m, 1723.26m),
                R("2026-12-31", 1723.26m, 10.05m, 1723.26m, 1733.31m, 0.00m),
            ]),
        // Level30E360_brokenFirstPeriod_12m: 8000.00 EUR at 8% (30E/360), 12 months, monthly, disbursed 2026-01-10, first payment 2026-03-01.
        // Instalment: P x r x (1+r)^12 / ((1+r)^12 - 1), r = 8% / 12 = 0.006666666667: 695.907433 -> 695.91 (nearest half-cent tie is 0.2433 cents away).
        // Row 1: interest 8000.00 x 8% x 17/120 = 90.666667 -> 90.67; principal 695.91 - 90.67 = 605.24; closing 7394.76.
        // Row 2: interest 7394.76 x 8% x 1/12 = 49.298400 -> 49.30; principal 695.91 - 49.30 = 646.61; closing 6748.15.
        // Row 12 (last): interest 731.18 x 8% x 1/12 = 4.874533 -> 4.87; principal = opening 731.18; total 736.05.
        new(
            "Level30E360_brokenFirstPeriod_12m", 8000.00m, 8m, "30E/360", 12, PaymentFrequency.Monthly,
            "2026-01-10", "2026-03-01", 695.91m,
            [
                R("2026-03-01", 8000.00m, 90.67m, 605.24m, 695.91m, 7394.76m),
                R("2026-04-01", 7394.76m, 49.30m, 646.61m, 695.91m, 6748.15m),
                R("2026-05-01", 6748.15m, 44.99m, 650.92m, 695.91m, 6097.23m),
                R("2026-06-01", 6097.23m, 40.65m, 655.26m, 695.91m, 5441.97m),
                R("2026-07-01", 5441.97m, 36.28m, 659.63m, 695.91m, 4782.34m),
                R("2026-08-01", 4782.34m, 31.88m, 664.03m, 695.91m, 4118.31m),
                R("2026-09-01", 4118.31m, 27.46m, 668.45m, 695.91m, 3449.86m),
                R("2026-10-01", 3449.86m, 23.00m, 672.91m, 695.91m, 2776.95m),
                R("2026-11-01", 2776.95m, 18.51m, 677.40m, 695.91m, 2099.55m),
                R("2026-12-01", 2099.55m, 14.00m, 681.91m, 695.91m, 1417.64m),
                R("2027-01-01", 1417.64m, 9.45m, 686.46m, 695.91m, 731.18m),
                R("2027-02-01", 731.18m, 4.87m, 731.18m, 736.05m, 0.00m),
            ]),
        // ZeroRate_exact_12m: 12000.00 EUR at 0% (ACT/360), 12 months, monthly, disbursed 2026-01-01, first payment 2026-02-01.
        // Instalment: zero rate, so P / n = 1000.000000 -> 1000.00.
        // Row 1: interest 12000.00 x 0% x 31/360 = 0.000000 -> 0.00; principal 1000.00 - 0.00 = 1000.00; closing 11000.00.
        // Row 2: interest 11000.00 x 0% x 7/90 = 0.000000 -> 0.00; principal 1000.00 - 0.00 = 1000.00; closing 10000.00.
        // Row 12 (last): interest 1000.00 x 0% x 31/360 = 0.000000 -> 0.00; principal = opening 1000.00; total 1000.00.
        new(
            "ZeroRate_exact_12m", 12000.00m, 0m, "ACT/360", 12, PaymentFrequency.Monthly,
            "2026-01-01", "2026-02-01", 1000.00m,
            [
                R("2026-02-01", 12000.00m, 0.00m, 1000.00m, 1000.00m, 11000.00m),
                R("2026-03-01", 11000.00m, 0.00m, 1000.00m, 1000.00m, 10000.00m),
                R("2026-04-01", 10000.00m, 0.00m, 1000.00m, 1000.00m, 9000.00m),
                R("2026-05-01", 9000.00m, 0.00m, 1000.00m, 1000.00m, 8000.00m),
                R("2026-06-01", 8000.00m, 0.00m, 1000.00m, 1000.00m, 7000.00m),
                R("2026-07-01", 7000.00m, 0.00m, 1000.00m, 1000.00m, 6000.00m),
                R("2026-08-01", 6000.00m, 0.00m, 1000.00m, 1000.00m, 5000.00m),
                R("2026-09-01", 5000.00m, 0.00m, 1000.00m, 1000.00m, 4000.00m),
                R("2026-10-01", 4000.00m, 0.00m, 1000.00m, 1000.00m, 3000.00m),
                R("2026-11-01", 3000.00m, 0.00m, 1000.00m, 1000.00m, 2000.00m),
                R("2026-12-01", 2000.00m, 0.00m, 1000.00m, 1000.00m, 1000.00m),
                R("2027-01-01", 1000.00m, 0.00m, 1000.00m, 1000.00m, 0.00m),
            ]),
        // ZeroRate_residual_12m: 10000.00 EUR at 0% (30E/360), 12 months, monthly, disbursed 2026-01-01, first payment 2026-02-01.
        // Instalment: zero rate, so P / n = 833.333333 -> 833.33.
        // Row 1: interest 10000.00 x 0% x 1/12 = 0.000000 -> 0.00; principal 833.33 - 0.00 = 833.33; closing 9166.67.
        // Row 2: interest 9166.67 x 0% x 1/12 = 0.000000 -> 0.00; principal 833.33 - 0.00 = 833.33; closing 8333.34.
        // Row 12 (last): interest 833.37 x 0% x 1/12 = 0.000000 -> 0.00; principal = opening 833.37; total 833.37.
        new(
            "ZeroRate_residual_12m", 10000.00m, 0m, "30E/360", 12, PaymentFrequency.Monthly,
            "2026-01-01", "2026-02-01", 833.33m,
            [
                R("2026-02-01", 10000.00m, 0.00m, 833.33m, 833.33m, 9166.67m),
                R("2026-03-01", 9166.67m, 0.00m, 833.33m, 833.33m, 8333.34m),
                R("2026-04-01", 8333.34m, 0.00m, 833.33m, 833.33m, 7500.01m),
                R("2026-05-01", 7500.01m, 0.00m, 833.33m, 833.33m, 6666.68m),
                R("2026-06-01", 6666.68m, 0.00m, 833.33m, 833.33m, 5833.35m),
                R("2026-07-01", 5833.35m, 0.00m, 833.33m, 833.33m, 5000.02m),
                R("2026-08-01", 5000.02m, 0.00m, 833.33m, 833.33m, 4166.69m),
                R("2026-09-01", 4166.69m, 0.00m, 833.33m, 833.33m, 3333.36m),
                R("2026-10-01", 3333.36m, 0.00m, 833.33m, 833.33m, 2500.03m),
                R("2026-11-01", 2500.03m, 0.00m, 833.33m, 833.33m, 1666.70m),
                R("2026-12-01", 1666.70m, 0.00m, 833.33m, 833.33m, 833.37m),
                R("2027-01-01", 833.37m, 0.00m, 833.37m, 833.37m, 0.00m),
            ]),
        // Quarterly30E360_48m: 50000.00 EUR at 4.5% (30E/360), 48 months, quarterly, disbursed 2026-01-15, first payment 2026-04-15.
        // Instalment: P x r x (1+r)^16 / ((1+r)^16 - 1), r = 4.5% / 4 = 0.01125: 3432.181265 -> 3432.18 (nearest half-cent tie is 0.3735 cents away).
        // Row 1: interest 50000.00 x 4.5% x 1/4 = 562.500000 -> 562.50; principal 3432.18 - 562.50 = 2869.68; closing 47130.32.
        // Row 2: interest 47130.32 x 4.5% x 1/4 = 530.216100 -> 530.22; principal 3432.18 - 530.22 = 2901.96; closing 44228.36.
        // Row 16 (last): interest 3394.03 x 4.5% x 1/4 = 38.182837 -> 38.18; principal = opening 3394.03; total 3432.21.
        new(
            "Quarterly30E360_48m", 50000.00m, 4.5m, "30E/360", 48, PaymentFrequency.Quarterly,
            "2026-01-15", "2026-04-15", 3432.18m,
            [
                R("2026-04-15", 50000.00m, 562.50m, 2869.68m, 3432.18m, 47130.32m),
                R("2026-07-15", 47130.32m, 530.22m, 2901.96m, 3432.18m, 44228.36m),
                R("2026-10-15", 44228.36m, 497.57m, 2934.61m, 3432.18m, 41293.75m),
                R("2027-01-15", 41293.75m, 464.55m, 2967.63m, 3432.18m, 38326.12m),
                R("2027-04-15", 38326.12m, 431.17m, 3001.01m, 3432.18m, 35325.11m),
                R("2027-07-15", 35325.11m, 397.41m, 3034.77m, 3432.18m, 32290.34m),
                R("2027-10-15", 32290.34m, 363.27m, 3068.91m, 3432.18m, 29221.43m),
                R("2028-01-15", 29221.43m, 328.74m, 3103.44m, 3432.18m, 26117.99m),
                R("2028-04-15", 26117.99m, 293.83m, 3138.35m, 3432.18m, 22979.64m),
                R("2028-07-15", 22979.64m, 258.52m, 3173.66m, 3432.18m, 19805.98m),
                R("2028-10-15", 19805.98m, 222.82m, 3209.36m, 3432.18m, 16596.62m),
                R("2029-01-15", 16596.62m, 186.71m, 3245.47m, 3432.18m, 13351.15m),
                R("2029-04-15", 13351.15m, 150.20m, 3281.98m, 3432.18m, 10069.17m),
                R("2029-07-15", 10069.17m, 113.28m, 3318.90m, 3432.18m, 6750.27m),
                R("2029-10-15", 6750.27m, 75.94m, 3356.24m, 3432.18m, 3394.03m),
                R("2030-01-15", 3394.03m, 38.18m, 3394.03m, 3432.21m, 0.00m),
            ]),
        // QuarterlyActActIsda_24m: 30000.00 EUR at 6.25% (ACT/ACT (ISDA)), 24 months, quarterly, disbursed 2027-12-15, first payment 2028-03-15.
        // Instalment: P x r x (1+r)^8 / ((1+r)^8 - 1), r = 6.25% / 4 = 0.015625: 4018.439988 -> 4018.44 (nearest half-cent tie is 0.4988 cents away).
        // Row 1: interest 30000.00 x 6.25% x 16616/66795 = 466.427128 -> 466.43; principal 4018.44 - 466.43 = 3552.01; closing 26447.99.
        // Row 2: interest 26447.99 x 6.25% x 46/183 = 415.508040 -> 415.51; principal 4018.44 - 415.51 = 3602.93; closing 22845.06.
        // Row 8 (last): interest 3955.86 x 6.25% x 91/365 = 61.640969 -> 61.64; principal = opening 3955.86; total 4017.50.
        new(
            "QuarterlyActActIsda_24m", 30000.00m, 6.25m, "ACT/ACT (ISDA)", 24, PaymentFrequency.Quarterly,
            "2027-12-15", "2028-03-15", 4018.44m,
            [
                R("2028-03-15", 30000.00m, 466.43m, 3552.01m, 4018.44m, 26447.99m),
                R("2028-06-15", 26447.99m, 415.51m, 3602.93m, 4018.44m, 22845.06m),
                R("2028-09-15", 22845.06m, 358.90m, 3659.54m, 4018.44m, 19185.52m),
                R("2028-12-15", 19185.52m, 298.14m, 3720.30m, 4018.44m, 15465.22m),
                R("2029-03-15", 15465.22m, 238.21m, 3780.23m, 4018.44m, 11684.99m),
                R("2029-06-15", 11684.99m, 184.08m, 3834.36m, 4018.44m, 7850.63m),
                R("2029-09-15", 7850.63m, 123.67m, 3894.77m, 4018.44m, 3955.86m),
                R("2029-12-15", 3955.86m, 61.64m, 3955.86m, 4017.50m, 0.00m),
            ]),
        // SinglePayment_monthly: 5000.00 EUR at 9% (30E/360), 1 months, monthly, disbursed 2026-01-15, first payment 2026-02-15.
        // Instalment: P x r x (1+r)^1 / ((1+r)^1 - 1), r = 9% / 12 = 0.0075: 5037.500000 -> 5037.50 (nearest half-cent tie is 0.5000 cents away).
        // Row 1 (last): interest 5000.00 x 9% x 1/12 = 37.500000 -> 37.50; principal = opening 5000.00; total 5037.50.
        new(
            "SinglePayment_monthly", 5000.00m, 9m, "30E/360", 1, PaymentFrequency.Monthly,
            "2026-01-15", "2026-02-15", 5037.50m,
            [
                R("2026-02-15", 5000.00m, 37.50m, 5000.00m, 5037.50m, 0.00m),
            ]),
        // SinglePayment_quarterly: 7500.00 EUR at 3% (ACT/360), 3 months, quarterly, disbursed 2026-01-15, first payment 2026-04-15.
        // Instalment: P x r x (1+r)^1 / ((1+r)^1 - 1), r = 3% / 4 = 0.0075: 7556.250000 -> 7556.25 (nearest half-cent tie is 0.5000 cents away).
        // Row 1 (last): interest 7500.00 x 3% x 1/4 = 56.250000 -> 56.25; principal = opening 7500.00; total 7556.25.
        new(
            "SinglePayment_quarterly", 7500.00m, 3m, "ACT/360", 3, PaymentFrequency.Quarterly,
            "2026-01-15", "2026-04-15", 7556.25m,
            [
                R("2026-04-15", 7500.00m, 56.25m, 7500.00m, 7556.25m, 0.00m),
            ]),
        // Act360_monthEndClamp_12m: 40000.00 EUR at 5.5% (ACT/360), 12 months, monthly, disbursed 2026-01-01, first payment 2026-01-31.
        // Instalment: P x r x (1+r)^12 / ((1+r)^12 - 1), r = 5.5% / 12 = 0.004583333333: 3433.471383 -> 3433.47 (nearest half-cent tie is 0.3617 cents away).
        // Row 1: interest 40000.00 x 5.5% x 1/12 = 183.333333 -> 183.33; principal 3433.47 - 183.33 = 3250.14; closing 36749.86.
        // Row 2: interest 36749.86 x 5.5% x 7/90 = 157.207734 -> 157.21; principal 3433.47 - 157.21 = 3276.26; closing 33473.60.
        // Row 12 (last): interest 3422.99 x 5.5% x 31/360 = 16.211661 -> 16.21; principal = opening 3422.99; total 3439.20.
        new(
            "Act360_monthEndClamp_12m", 40000.00m, 5.5m, "ACT/360", 12, PaymentFrequency.Monthly,
            "2026-01-01", "2026-01-31", 3433.47m,
            [
                R("2026-01-31", 40000.00m, 183.33m, 3250.14m, 3433.47m, 36749.86m),
                R("2026-02-28", 36749.86m, 157.21m, 3276.26m, 3433.47m, 33473.60m),
                R("2026-03-31", 33473.60m, 158.53m, 3274.94m, 3433.47m, 30198.66m),
                R("2026-04-30", 30198.66m, 138.41m, 3295.06m, 3433.47m, 26903.60m),
                R("2026-05-31", 26903.60m, 127.42m, 3306.05m, 3433.47m, 23597.55m),
                R("2026-06-30", 23597.55m, 108.16m, 3325.31m, 3433.47m, 20272.24m),
                R("2026-07-31", 20272.24m, 96.01m, 3337.46m, 3433.47m, 16934.78m),
                R("2026-08-31", 16934.78m, 80.20m, 3353.27m, 3433.47m, 13581.51m),
                R("2026-09-30", 13581.51m, 62.25m, 3371.22m, 3433.47m, 10210.29m),
                R("2026-10-31", 10210.29m, 48.36m, 3385.11m, 3433.47m, 6825.18m),
                R("2026-11-30", 6825.18m, 31.28m, 3402.19m, 3433.47m, 3422.99m),
                R("2026-12-31", 3422.99m, 16.21m, 3422.99m, 3439.20m, 0.00m),
            ]),
        // TinyPrincipal_zeroRate_5m: 1.00 EUR at 0% (30E/360), 5 months, monthly, disbursed 2026-01-15, first payment 2026-02-15.
        // Instalment: zero rate, so P / n = 0.200000 -> 0.20.
        // Row 1: interest 1.00 x 0% x 1/12 = 0.000000 -> 0.00; principal 0.20 - 0.00 = 0.20; closing 0.80.
        // Row 2: interest 0.80 x 0% x 1/12 = 0.000000 -> 0.00; principal 0.20 - 0.00 = 0.20; closing 0.60.
        // Row 5 (last): interest 0.20 x 0% x 1/12 = 0.000000 -> 0.00; principal = opening 0.20; total 0.20.
        new(
            "TinyPrincipal_zeroRate_5m", 1.00m, 0m, "30E/360", 5, PaymentFrequency.Monthly,
            "2026-01-15", "2026-02-15", 0.20m,
            [
                R("2026-02-15", 1.00m, 0.00m, 0.20m, 0.20m, 0.80m),
                R("2026-03-15", 0.80m, 0.00m, 0.20m, 0.20m, 0.60m),
                R("2026-04-15", 0.60m, 0.00m, 0.20m, 0.20m, 0.40m),
                R("2026-05-15", 0.40m, 0.00m, 0.20m, 0.20m, 0.20m),
                R("2026-06-15", 0.20m, 0.00m, 0.20m, 0.20m, 0.00m),
            ]),
        // TinyPrincipal_6pct_12m: 0.95 EUR at 6% (30E/360), 12 months, monthly, disbursed 2026-01-15, first payment 2026-02-15.
        // Instalment: P x r x (1+r)^12 / ((1+r)^12 - 1), r = 6% / 12 = 0.005: 0.081763 -> 0.08 (nearest half-cent tie is 0.3237 cents away).
        // Row 1: interest 0.95 x 6% x 1/12 = 0.004750 -> 0.00; principal 0.08 - 0.00 = 0.08; closing 0.87.
        // Row 2: interest 0.87 x 6% x 1/12 = 0.004350 -> 0.00; principal 0.08 - 0.00 = 0.08; closing 0.79.
        // Row 12 (last): interest 0.07 x 6% x 1/12 = 0.000350 -> 0.00; principal = opening 0.07; total 0.07.
        new(
            "TinyPrincipal_6pct_12m", 0.95m, 6m, "30E/360", 12, PaymentFrequency.Monthly,
            "2026-01-15", "2026-02-15", 0.08m,
            [
                R("2026-02-15", 0.95m, 0.00m, 0.08m, 0.08m, 0.87m),
                R("2026-03-15", 0.87m, 0.00m, 0.08m, 0.08m, 0.79m),
                R("2026-04-15", 0.79m, 0.00m, 0.08m, 0.08m, 0.71m),
                R("2026-05-15", 0.71m, 0.00m, 0.08m, 0.08m, 0.63m),
                R("2026-06-15", 0.63m, 0.00m, 0.08m, 0.08m, 0.55m),
                R("2026-07-15", 0.55m, 0.00m, 0.08m, 0.08m, 0.47m),
                R("2026-08-15", 0.47m, 0.00m, 0.08m, 0.08m, 0.39m),
                R("2026-09-15", 0.39m, 0.00m, 0.08m, 0.08m, 0.31m),
                R("2026-10-15", 0.31m, 0.00m, 0.08m, 0.08m, 0.23m),
                R("2026-11-15", 0.23m, 0.00m, 0.08m, 0.08m, 0.15m),
                R("2026-12-15", 0.15m, 0.00m, 0.08m, 0.08m, 0.07m),
                R("2027-01-15", 0.07m, 0.00m, 0.07m, 0.07m, 0.00m),
            ]),
        // InstalmentTie_roundsDown_2m: 100.50 EUR at 12% (30E/360), 2 months, monthly, disbursed 2026-01-15, first payment 2026-02-15.
        // Instalment: P x r x (1+r)^2 / ((1+r)^2 - 1), r = 12% / 12 = 0.01: 51.005000 -> 51.00 (EXACT HALF-CENT TIE: rounds to the even cent).
        // Row 1: interest 100.50 x 12% x 1/12 = 1.005000 -> 1.00; principal 51.00 - 1.00 = 50.00; closing 50.50.  EXACT HALF-CENT TIE, rounds to the even cent.
        // Row 2 (last): interest 50.50 x 12% x 1/12 = 0.505000 -> 0.50; principal = opening 50.50; total 51.00.  EXACT HALF-CENT TIE, rounds to the even cent.
        // Half-cent ties in this schedule (half-even): row 1: exact 1.005 -> 1.00; row 2: exact 0.505 -> 0.50.
        new(
            "InstalmentTie_roundsDown_2m", 100.50m, 12m, "30E/360", 2, PaymentFrequency.Monthly,
            "2026-01-15", "2026-02-15", 51.00m,
            [
                R("2026-02-15", 100.50m, 1.00m, 50.00m, 51.00m, 50.50m),
                R("2026-03-15", 50.50m, 0.50m, 50.50m, 51.00m, 0.00m),
            ]),
        // InstalmentTie_roundsUp_2m: 4.50 EUR at 100% (30E/360), 2 months, monthly, disbursed 2026-01-15, first payment 2026-02-15.
        // Instalment: P x r x (1+r)^2 / ((1+r)^2 - 1), r = 100% / 12 = 0.08333333333: 2.535000 -> 2.54 (EXACT HALF-CENT TIE: rounds to the even cent).
        // Row 1: interest 4.50 x 100% x 1/12 = 0.375000 -> 0.38; principal 2.54 - 0.38 = 2.16; closing 2.34.  EXACT HALF-CENT TIE, rounds to the even cent.
        // Row 2 (last): interest 2.34 x 100% x 1/12 = 0.195000 -> 0.20; principal = opening 2.34; total 2.54.  EXACT HALF-CENT TIE, rounds to the even cent.
        // Half-cent ties in this schedule (half-even): row 1: exact 0.375 -> 0.38; row 2: exact 0.195 -> 0.20.
        new(
            "InstalmentTie_roundsUp_2m", 4.50m, 100m, "30E/360", 2, PaymentFrequency.Monthly,
            "2026-01-15", "2026-02-15", 2.54m,
            [
                R("2026-02-15", 4.50m, 0.38m, 2.16m, 2.54m, 2.34m),
                R("2026-03-15", 2.34m, 0.20m, 2.34m, 2.54m, 0.00m),
            ]),
    ];
}
