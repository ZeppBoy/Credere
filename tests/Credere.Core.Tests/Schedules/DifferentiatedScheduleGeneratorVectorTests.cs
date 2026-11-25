using System.Globalization;
using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using FsCheck;
using FsCheck.Xunit;
using NodaTime;
using NodaTime.Text;
using Xunit;

namespace Credere.Core.Tests.Schedules;

/// <summary>
/// Formula-derived reference schedules for differentiated repayment, calculated independently
/// of Credere before implementing the generator.
/// </summary>
/// <remarks>
/// <para>
/// The California State Board of Equalization, Assessors' Handbook §503, Problem 11, p. 46,
/// illustrates constant principal and interest on the opening balance with annual payments.
/// Credere uses monthly and quarterly periods under docs/domain/schedules.md, sections 2 and
/// "Rounding and residuals". The full remaining principal falls due in the last row.
/// </para>
/// <para>
/// Dates follow "Periods and interest" in schedules.md. Interest fractions follow
/// ISDA 2006 Definitions §4.16(b), (d), (e), (f) and (g). Principal and periodic interest
/// are each rounded to cents with ties to even per docs/domain/value-objects.md, "Money".
/// The loans use the M1-14 schedule fixtures and their dates, with zero-rate variations
/// targeting half-cent rounding and the early repayment boundary approved in PR #52. Expected rows were calculated with exact rational arithmetic and stored
/// as decimal amounts.
/// </para>
/// </remarks>
public sealed class DifferentiatedScheduleGeneratorVectorTests
{
    private static readonly Vector[] Vectors =
    [
        // 10000 / 24 gives regular principal 416.67 after rounding.
        // Row 1: 10000.00 x 5% x 1/12 rounds to 41.67 interest; 10000.00 - 416.67 = 9583.33.
        // Row 2: 9583.33 x 5% x 1/12 rounds to 39.93 interest; 9583.33 - 416.67 = 9166.66.
        // Row 24: 416.59 x 5% x 1/12 rounds to 1.74 interest; 416.59 - 416.59 = 0.00.
        new("ThirtyE360Level", 10000m, 5m, "30E/360", 24, PaymentFrequency.Monthly,
            "2026-01-15", "2026-02-15",
            [
                new("2026-02-15", 10000.00m, 41.67m, 416.67m, 458.34m, 9583.33m),
                new("2026-03-15", 9583.33m, 39.93m, 416.67m, 456.60m, 9166.66m),
                new("2026-04-15", 9166.66m, 38.19m, 416.67m, 454.86m, 8749.99m),
                new("2026-05-15", 8749.99m, 36.46m, 416.67m, 453.13m, 8333.32m),
                new("2026-06-15", 8333.32m, 34.72m, 416.67m, 451.39m, 7916.65m),
                new("2026-07-15", 7916.65m, 32.99m, 416.67m, 449.66m, 7499.98m),
                new("2026-08-15", 7499.98m, 31.25m, 416.67m, 447.92m, 7083.31m),
                new("2026-09-15", 7083.31m, 29.51m, 416.67m, 446.18m, 6666.64m),
                new("2026-10-15", 6666.64m, 27.78m, 416.67m, 444.45m, 6249.97m),
                new("2026-11-15", 6249.97m, 26.04m, 416.67m, 442.71m, 5833.30m),
                new("2026-12-15", 5833.30m, 24.31m, 416.67m, 440.98m, 5416.63m),
                new("2027-01-15", 5416.63m, 22.57m, 416.67m, 439.24m, 4999.96m),
                new("2027-02-15", 4999.96m, 20.83m, 416.67m, 437.50m, 4583.29m),
                new("2027-03-15", 4583.29m, 19.10m, 416.67m, 435.77m, 4166.62m),
                new("2027-04-15", 4166.62m, 17.36m, 416.67m, 434.03m, 3749.95m),
                new("2027-05-15", 3749.95m, 15.62m, 416.67m, 432.29m, 3333.28m),
                new("2027-06-15", 3333.28m, 13.89m, 416.67m, 430.56m, 2916.61m),
                new("2027-07-15", 2916.61m, 12.15m, 416.67m, 428.82m, 2499.94m),
                new("2027-08-15", 2499.94m, 10.42m, 416.67m, 427.09m, 2083.27m),
                new("2027-09-15", 2083.27m, 8.68m, 416.67m, 425.35m, 1666.60m),
                new("2027-10-15", 1666.60m, 6.94m, 416.67m, 423.61m, 1249.93m),
                new("2027-11-15", 1249.93m, 5.21m, 416.67m, 421.88m, 833.26m),
                new("2027-12-15", 833.26m, 3.47m, 416.67m, 420.14m, 416.59m),
                new("2028-01-15", 416.59m, 1.74m, 416.59m, 418.33m, 0.00m),
            ]),
        // 10000 / 12 gives regular principal 833.33 after rounding.
        // Row 1: 10000.00 x 5% x 31/360 rounds to 43.06 interest; 10000.00 - 833.33 = 9166.67.
        // Row 2: 9166.67 x 5% x 7/90 rounds to 35.65 interest; 9166.67 - 833.33 = 8333.34.
        // Row 12: 833.37 x 5% x 31/360 rounds to 3.59 interest; 833.37 - 833.37 = 0.00.
        new("Actual360", 10000m, 5m, "ACT/360", 12, PaymentFrequency.Monthly,
            "2026-01-01", "2026-02-01",
            [
                new("2026-02-01", 10000.00m, 43.06m, 833.33m, 876.39m, 9166.67m),
                new("2026-03-01", 9166.67m, 35.65m, 833.33m, 868.98m, 8333.34m),
                new("2026-04-01", 8333.34m, 35.88m, 833.33m, 869.21m, 7500.01m),
                new("2026-05-01", 7500.01m, 31.25m, 833.33m, 864.58m, 6666.68m),
                new("2026-06-01", 6666.68m, 28.70m, 833.33m, 862.03m, 5833.35m),
                new("2026-07-01", 5833.35m, 24.31m, 833.33m, 857.64m, 5000.02m),
                new("2026-08-01", 5000.02m, 21.53m, 833.33m, 854.86m, 4166.69m),
                new("2026-09-01", 4166.69m, 17.94m, 833.33m, 851.27m, 3333.36m),
                new("2026-10-01", 3333.36m, 13.89m, 833.33m, 847.22m, 2500.03m),
                new("2026-11-01", 2500.03m, 10.76m, 833.33m, 844.09m, 1666.70m),
                new("2026-12-01", 1666.70m, 6.94m, 833.33m, 840.27m, 833.37m),
                new("2027-01-01", 833.37m, 3.59m, 833.37m, 836.96m, 0.00m),
            ]),
        // 10000 / 12 gives regular principal 833.33 after rounding.
        // Row 1: 10000.00 x 6% x 31/365 rounds to 50.96 interest; 10000.00 - 833.33 = 9166.67.
        // Row 2: 9166.67 x 6% x 31/365 rounds to 46.71 interest; 9166.67 - 833.33 = 8333.34.
        // Row 12: 833.37 x 6% x 6/73 rounds to 4.11 interest; 833.37 - 833.37 = 0.00.
        new("Actual365FixedLeapYear", 10000m, 6m, "ACT/365 Fixed", 12, PaymentFrequency.Monthly,
            "2027-12-10", "2028-01-10",
            [
                new("2028-01-10", 10000.00m, 50.96m, 833.33m, 884.29m, 9166.67m),
                new("2028-02-10", 9166.67m, 46.71m, 833.33m, 880.04m, 8333.34m),
                new("2028-03-10", 8333.34m, 39.73m, 833.33m, 873.06m, 7500.01m),
                new("2028-04-10", 7500.01m, 38.22m, 833.33m, 871.55m, 6666.68m),
                new("2028-05-10", 6666.68m, 32.88m, 833.33m, 866.21m, 5833.35m),
                new("2028-06-10", 5833.35m, 29.73m, 833.33m, 863.06m, 5000.02m),
                new("2028-07-10", 5000.02m, 24.66m, 833.33m, 857.99m, 4166.69m),
                new("2028-08-10", 4166.69m, 21.23m, 833.33m, 854.56m, 3333.36m),
                new("2028-09-10", 3333.36m, 16.99m, 833.33m, 850.32m, 2500.03m),
                new("2028-10-10", 2500.03m, 12.33m, 833.33m, 845.66m, 1666.70m),
                new("2028-11-10", 1666.70m, 8.49m, 833.33m, 841.82m, 833.37m),
                new("2028-12-10", 833.37m, 4.11m, 833.37m, 837.48m, 0.00m),
            ]),
        // 15000 / 12 gives regular principal 1250.00 after rounding.
        // Row 1: 15000.00 x 4.8% x 6/73 rounds to 59.18 interest; 15000.00 - 1250.00 = 13750.00.
        // Row 2: 13750.00 x 4.8% x 5666/66795 rounds to 55.99 interest; 13750.00 - 1250.00 = 12500.00.
        // Row 12: 1250.00 x 4.8% x 31/366 rounds to 5.08 interest; 1250.00 - 1250.00 = 0.00.
        new("ActualActualCrossYear", 15000m, 4.8m, "ACT/ACT (ISDA)", 12, PaymentFrequency.Monthly,
            "2027-11-15", "2027-12-15",
            [
                new("2027-12-15", 15000.00m, 59.18m, 1250.00m, 1309.18m, 13750.00m),
                new("2028-01-15", 13750.00m, 55.99m, 1250.00m, 1305.99m, 12500.00m),
                new("2028-02-15", 12500.00m, 50.82m, 1250.00m, 1300.82m, 11250.00m),
                new("2028-03-15", 11250.00m, 42.79m, 1250.00m, 1292.79m, 10000.00m),
                new("2028-04-15", 10000.00m, 40.66m, 1250.00m, 1290.66m, 8750.00m),
                new("2028-05-15", 8750.00m, 34.43m, 1250.00m, 1284.43m, 7500.00m),
                new("2028-06-15", 7500.00m, 30.49m, 1250.00m, 1280.49m, 6250.00m),
                new("2028-07-15", 6250.00m, 24.59m, 1250.00m, 1274.59m, 5000.00m),
                new("2028-08-15", 5000.00m, 20.33m, 1250.00m, 1270.33m, 3750.00m),
                new("2028-09-15", 3750.00m, 15.25m, 1250.00m, 1265.25m, 2500.00m),
                new("2028-10-15", 2500.00m, 9.84m, 1250.00m, 1259.84m, 1250.00m),
                new("2028-11-15", 1250.00m, 5.08m, 1250.00m, 1255.08m, 0.00m),
            ]),
        // 20000 / 12 gives regular principal 1666.67 after rounding.
        // Row 1: 20000.00 x 7% x 1/12 rounds to 116.67 interest; 20000.00 - 1666.67 = 18333.33.
        // Row 2: 18333.33 x 7% x 7/90 rounds to 99.81 interest; 18333.33 - 1666.67 = 16666.66.
        // Row 12: 1666.63 x 7% x 1/12 rounds to 9.72 interest; 1666.63 - 1666.63 = 0.00.
        new("Thirty360IsdaMonthEnd", 20000m, 7m, "30/360 ISDA", 12, PaymentFrequency.Monthly,
            "2025-12-31", "2026-01-31",
            [
                new("2026-01-31", 20000.00m, 116.67m, 1666.67m, 1783.34m, 18333.33m),
                new("2026-02-28", 18333.33m, 99.81m, 1666.67m, 1766.48m, 16666.66m),
                new("2026-03-31", 16666.66m, 106.94m, 1666.67m, 1773.61m, 14999.99m),
                new("2026-04-30", 14999.99m, 87.50m, 1666.67m, 1754.17m, 13333.32m),
                new("2026-05-31", 13333.32m, 77.78m, 1666.67m, 1744.45m, 11666.65m),
                new("2026-06-30", 11666.65m, 68.06m, 1666.67m, 1734.73m, 9999.98m),
                new("2026-07-31", 9999.98m, 58.33m, 1666.67m, 1725.00m, 8333.31m),
                new("2026-08-31", 8333.31m, 48.61m, 1666.67m, 1715.28m, 6666.64m),
                new("2026-09-30", 6666.64m, 38.89m, 1666.67m, 1705.56m, 4999.97m),
                new("2026-10-31", 4999.97m, 29.17m, 1666.67m, 1695.84m, 3333.30m),
                new("2026-11-30", 3333.30m, 19.44m, 1666.67m, 1686.11m, 1666.63m),
                new("2026-12-31", 1666.63m, 9.72m, 1666.63m, 1676.35m, 0.00m),
            ]),
        // 8000 / 12 gives regular principal 666.67 after rounding.
        // Row 1: 8000.00 x 8% x 17/120 rounds to 90.67 interest; 8000.00 - 666.67 = 7333.33.
        // Row 2: 7333.33 x 8% x 1/12 rounds to 48.89 interest; 7333.33 - 666.67 = 6666.66.
        // Row 12: 666.63 x 8% x 1/12 rounds to 4.44 interest; 666.63 - 666.63 = 0.00.
        new("BrokenFirstPeriod", 8000m, 8m, "30E/360", 12, PaymentFrequency.Monthly,
            "2026-01-10", "2026-03-01",
            [
                new("2026-03-01", 8000.00m, 90.67m, 666.67m, 757.34m, 7333.33m),
                new("2026-04-01", 7333.33m, 48.89m, 666.67m, 715.56m, 6666.66m),
                new("2026-05-01", 6666.66m, 44.44m, 666.67m, 711.11m, 5999.99m),
                new("2026-06-01", 5999.99m, 40.00m, 666.67m, 706.67m, 5333.32m),
                new("2026-07-01", 5333.32m, 35.56m, 666.67m, 702.23m, 4666.65m),
                new("2026-08-01", 4666.65m, 31.11m, 666.67m, 697.78m, 3999.98m),
                new("2026-09-01", 3999.98m, 26.67m, 666.67m, 693.34m, 3333.31m),
                new("2026-10-01", 3333.31m, 22.22m, 666.67m, 688.89m, 2666.64m),
                new("2026-11-01", 2666.64m, 17.78m, 666.67m, 684.45m, 1999.97m),
                new("2026-12-01", 1999.97m, 13.33m, 666.67m, 680.00m, 1333.30m),
                new("2027-01-01", 1333.30m, 8.89m, 666.67m, 675.56m, 666.63m),
                new("2027-02-01", 666.63m, 4.44m, 666.63m, 671.07m, 0.00m),
            ]),
        // 30000 / 8 gives regular principal 3750.00 after rounding.
        // Row 1: 30000.00 x 6.25% x 16616/66795 rounds to 466.43 interest; 30000.00 - 3750.00 = 26250.00.
        // Row 2: 26250.00 x 6.25% x 46/183 rounds to 412.40 interest; 26250.00 - 3750.00 = 22500.00.
        // Row 8: 3750.00 x 6.25% x 91/365 rounds to 58.43 interest; 3750.00 - 3750.00 = 0.00.
        new("QuarterlyActualActual", 30000m, 6.25m, "ACT/ACT (ISDA)", 24, PaymentFrequency.Quarterly,
            "2027-12-15", "2028-03-15",
            [
                new("2028-03-15", 30000.00m, 466.43m, 3750.00m, 4216.43m, 26250.00m),
                new("2028-06-15", 26250.00m, 412.40m, 3750.00m, 4162.40m, 22500.00m),
                new("2028-09-15", 22500.00m, 353.48m, 3750.00m, 4103.48m, 18750.00m),
                new("2028-12-15", 18750.00m, 291.37m, 3750.00m, 4041.37m, 15000.00m),
                new("2029-03-15", 15000.00m, 231.05m, 3750.00m, 3981.05m, 11250.00m),
                new("2029-06-15", 11250.00m, 177.23m, 3750.00m, 3927.23m, 7500.00m),
                new("2029-09-15", 7500.00m, 118.15m, 3750.00m, 3868.15m, 3750.00m),
                new("2029-12-15", 3750.00m, 58.43m, 3750.00m, 3808.43m, 0.00m),
            ]),
        // 7500 / 1 gives regular principal 7500.00 after rounding.
        // Row 1: 7500.00 x 3% x 1/4 rounds to 56.25 interest; 7500.00 - 7500.00 = 0.00.
        new("SingleQuarterlyPayment", 7500m, 3m, "ACT/360", 3, PaymentFrequency.Quarterly,
            "2026-01-15", "2026-04-15",
            [
                new("2026-04-15", 7500.00m, 56.25m, 7500.00m, 7556.25m, 0.00m),
            ]),
        // 10000.01 / 2 = 5000.005, a tie that rounds to 5000.00.
        // Row 1: 10000.01 x 0% x 1/12 rounds to 0.00 interest; 10000.01 - 5000.00 = 5000.01.
        // Row 2: 5000.01 x 0% x 1/12 rounds to 0.00 interest; 5000.01 - 5000.01 = 0.00.
        new("ZeroRateHalfCentRoundsDown", 10000.01m, 0m, "30E/360", 2, PaymentFrequency.Monthly,
            "2026-01-01", "2026-02-01",
            [
                new("2026-02-01", 10000.01m, 0.00m, 5000.00m, 5000.00m, 5000.01m),
                new("2026-03-01", 5000.01m, 0.00m, 5000.01m, 5000.01m, 0.00m),
            ]),
        // 10000.03 / 2 = 5000.015, a tie that rounds to 5000.02.
        // Row 1: 10000.03 x 0% x 1/12 rounds to 0.00 interest; 10000.03 - 5000.02 = 5000.01.
        // Row 2: 5000.01 x 0% x 1/12 rounds to 0.00 interest; 5000.01 - 5000.01 = 0.00.
        new("ZeroRateHalfCentRoundsUp", 10000.03m, 0m, "30E/360", 2, PaymentFrequency.Monthly,
            "2026-01-01", "2026-02-01",
            [
                new("2026-02-01", 10000.03m, 0.00m, 5000.02m, 5000.02m, 5000.01m),
                new("2026-03-01", 5000.01m, 0.00m, 5000.01m, 5000.01m, 0.00m),
            ]),
    ];

    /// <summary>Gets the names of the complete reference schedules.</summary>
    public static TheoryData<string> Names
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var vector in Vectors)
            {
                names.Add(vector.Name);
            }

            return names;
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Generate_referenceSchedule_matchesEveryField(string name)
    {
        var vector = Vectors.Single(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
        var schedule = Generate(Request(vector));
        Assert.Equal(vector.Rows.Length, schedule.Rows.Count);

        for (var index = 0; index < vector.Rows.Length; index++)
        {
            var expected = vector.Rows[index];
            var row = new ScheduleRow(index + 1, Date(expected.Date), Euro(expected.Opening), Euro(0m),
                Euro(expected.Interest), Euro(expected.Principal), Euro(expected.Total), Euro(expected.Closing));

            Assert.Equal(row, schedule.Rows[index]);
        }
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    public void Generate_regularPrincipalWouldDepleteTheBalance_returnsAValidationErrorOnPrincipal(
        int principalCents, int firstRejectedRow)
    {
        // PR #52 and M1-23. Both principals round to a one-cent instalment over five months.
        // The three-cent loan first reaches zero on row 3, and would overpay on row 4 if allowed to continue.
        // The four-cent loan first reaches zero on row 4.
        var request = Request(principalCents / 100m, 0m, new ThirtyE360(), 5, PaymentFrequency.Monthly,
            Date("2026-01-01"), Date("2026-02-01"));

        var result = Generator.Generate(request);

        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal("principal", error.Field);
        Assert.Contains($"row {firstRejectedRow.ToString(CultureInfo.InvariantCulture)}:", error.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_singleZeroRatePaymentAtDecimalLimit_paysThePrincipalExactly()
    {
        var request = Request(decimal.MaxValue, 0m, new ThirtyE360(), 1, PaymentFrequency.Monthly,
            Date("2026-01-01"), Date("2026-02-01"));

        var row = Assert.Single(Generate(request).Rows);
        Assert.Equal(request.Principal, row.TotalPayment);
        Assert.Equal(Euro(0m), row.ClosingBalance);
    }

    [Fact]
    public void Generate_roundedPrincipalBeyondDecimalPrecision_returnsAValidationErrorOnPrincipal()
    {
        // MaxValue is an odd whole-unit amount. Half ends in .50, which exceeds the decimal coefficient limit.
        var request = Request(decimal.MaxValue, 0m, new ThirtyE360(), 2, PaymentFrequency.Monthly,
            Date("2026-01-01"), Date("2026-02-01"));

        var result = Generator.Generate(request);

        Assert.Equal("principal", Assert.IsType<ValidationError>(result.Error).Field);
    }

    [Fact]
    public void Generate_negativeOriginalPrincipal_returnsAValidationErrorOnPrincipal()
    {
        var request = Request(-1m, 0m, new ThirtyE360(), 1, PaymentFrequency.Monthly,
            Date("2026-01-01"), Date("2026-02-01"));

        var result = Generator.Generate(request);

        Assert.Equal("principal", Assert.IsType<ValidationError>(result.Error).Field);
    }

    [Property]
    public void Generate_validPrincipalAndTerm_preservesPrincipalInterestAndRowCount(
        PositiveInt amount, PositiveInt count, bool quarterly)
    {
        var rowCount = 1 + (count.Get % 60);
        // A regular principal of at least one unit keeps the final balance positive despite rounding.
        var principal = ((100 + (amount.Get % 10_000)) * rowCount + (amount.Get % rowCount)) / 100m;
        var frequency = quarterly ? PaymentFrequency.Quarterly : PaymentFrequency.Monthly;
        var months = quarterly ? rowCount * 3 : rowCount;
        var request = Request(principal, 5m, new ThirtyE360(), months, frequency,
            Date("2026-01-15"), quarterly ? Date("2026-04-15") : Date("2026-02-15"));

        var rows = Generate(request).Rows;

        Assert.Equal(rowCount, rows.Count);
        Assert.Equal(principal, rows.Sum(row => row.PrincipalDue.Amount));
        Assert.Equal(Euro(0m), rows[^1].ClosingBalance);
        Assert.All(rows.Take(rowCount - 1), row => Assert.Equal(rows[0].PrincipalDue, row.PrincipalDue));

        var previousDate = request.DisbursementDate;
        foreach (var row in rows)
        {
            Assert.Equal(Calculator.Calculate(request.Rate, row.OpeningBalance, previousDate, row.PaymentDate), row.InterestDue);
            previousDate = row.PaymentDate;
        }
    }

    [Theory]
    [InlineData("ThirtyE360Level")]
    [InlineData("QuarterlyActualActual")]
    public Task Generate_canonicalLoan_matchesSnapshotFromApprovedVectors(string name)
    {
        var vector = Vectors.Single(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
        var rows = Generate(Request(vector)).Rows;
        var header = FormattableString.Invariant(
            $"{vector.Name}: EUR {vector.Principal:F2} at {vector.RatePercent}% ({vector.Convention}), {vector.Months} months, {vector.Frequency}");
        var lines = rows.Select(row => FormattableString.Invariant(
            $"{row.Index} | {LocalDatePattern.Iso.Format(row.PaymentDate)} | {row.OpeningBalance.Amount:F2} | {row.InterestDue.Amount:F2} | {row.PrincipalDue.Amount:F2} | {row.TotalPayment.Amount:F2} | {row.ClosingBalance.Amount:F2}"));
        var snapshot = header + "\nindex | date | opening | interest | principal | total | closing\n"
            + string.Join('\n', lines) + "\n";

        return Verifier.Verify(snapshot, "txt")
            .UseDirectory("Snapshots")
            .UseFileName($"DifferentiatedScheduleGenerator.{name}");
    }

    private static readonly InterestCalculator Calculator = new();
    private static readonly DifferentiatedScheduleGenerator Generator = new(Calculator);

    private static AmortizationSchedule Generate(ScheduleRequest request)
    {
        var result = Generator.Generate(request);
        Assert.True(result.IsSuccess, result.IsSuccess ? string.Empty : result.Error.ToString());
        return result.Value;
    }

    private static ScheduleRequest Request(Vector vector)
    {
        DayCountConvention convention = vector.Convention switch
        {
            "ACT/360" => new Act360(),
            "ACT/365 Fixed" => new Act365Fixed(),
            "ACT/ACT (ISDA)" => new ActActIsda(),
            "30/360 ISDA" => new Thirty360Isda(),
            "30E/360" => new ThirtyE360(),
            _ => throw new ArgumentOutOfRangeException(nameof(vector), vector.Convention, "Unknown reference convention."),
        };

        return Request(vector.Principal, vector.RatePercent, convention, vector.Months, vector.Frequency,
            Date(vector.Disbursement), Date(vector.FirstPayment));
    }

    private static ScheduleRequest Request(
        decimal amount, decimal ratePercent, DayCountConvention convention, int months,
        PaymentFrequency frequency, LocalDate disbursement, LocalDate firstPayment) =>
        ScheduleRequest.Create(Euro(amount), FixedRate.Create(new Percentage(ratePercent), convention).Value,
            LoanTerm.FromMonths(months), disbursement, firstPayment, frequency).Value;

    private static LocalDate Date(string iso) => LocalDatePattern.Iso.Parse(iso).Value;

    private static Money Euro(decimal amount) => Money.Create(amount, "EUR").Value;

    private sealed record Vector(
        string Name,
        decimal Principal,
        decimal RatePercent,
        string Convention,
        int Months,
        PaymentFrequency Frequency,
        string Disbursement,
        string FirstPayment,
        Row[] Rows);

    private sealed record Row(string Date, decimal Opening, decimal Interest, decimal Principal, decimal Total, decimal Closing);
}
