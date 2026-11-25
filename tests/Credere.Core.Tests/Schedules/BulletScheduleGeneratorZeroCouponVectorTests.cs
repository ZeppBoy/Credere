using Credere.Core.Rates;
using Credere.Core.Schedules;
using Xunit;
using static Credere.Core.Tests.Schedules.BulletScheduleGeneratorVectorAssertions;

namespace Credere.Core.Tests.Schedules;

/// <summary>
/// Formula-derived zero-coupon bullet schedules for docs/domain/schedules.md, §3, and M1-23 (issue #54).
/// </summary>
/// <remarks>
/// <para>
/// California State Board of Equalization, Assessors' Handbook §505, chapter 2, “Future Worth of 1,” p. 2,
/// describes adding period interest to principal before the next period. Credere rounds each accrual to cents
/// with ties to even (docs/domain/value-objects.md, “Money”) and pays the last period's interest at maturity
/// together with the accumulated principal. The row values use exact rational arithmetic before rounding.
/// </para>
/// <para>
/// Day-count fractions follow ISDA 2006 Definitions §4.16(b), (d), (e), (f) and (g).
/// Loan inputs reuse <see cref="BulletScheduleGeneratorInterestOnlyVectorTests"/> so each case shows
/// the effect of capitalising interest on the same loan.
/// </para>
/// </remarks>
public sealed class BulletScheduleGeneratorZeroCouponVectorTests
{
    private static readonly Vector[] Vectors =
    [
        // Row 1 accrues 10000.00 x 5% x 1/12 = 41.67 after rounding.
        // Capitalising that interest gives a closing balance of 10041.67, with no payment.
        // Row 2 accrues 10041.67 x 5% x 1/12 = 41.84 after rounding.
        // Capitalising that interest gives a closing balance of 10083.51, with no payment.
        // Row 24 accrues 11003.58 x 5% x 1/12 = 45.85 after rounding.
        // At maturity, 11003.58 principal plus 45.85 interest gives a 11049.43 payment.
        new("Level30E360_24m", 10000.00m, 5m, "30E/360", 24, PaymentFrequency.Monthly,
            "2026-01-15", "2026-02-15",
            [
                new("2026-02-15", 10000.00m, 41.67m, 0.00m, 0.00m, 0.00m, 10041.67m),
                new("2026-03-15", 10041.67m, 41.84m, 0.00m, 0.00m, 0.00m, 10083.51m),
                new("2026-04-15", 10083.51m, 42.01m, 0.00m, 0.00m, 0.00m, 10125.52m),
                new("2026-05-15", 10125.52m, 42.19m, 0.00m, 0.00m, 0.00m, 10167.71m),
                new("2026-06-15", 10167.71m, 42.37m, 0.00m, 0.00m, 0.00m, 10210.08m),
                new("2026-07-15", 10210.08m, 42.54m, 0.00m, 0.00m, 0.00m, 10252.62m),
                new("2026-08-15", 10252.62m, 42.72m, 0.00m, 0.00m, 0.00m, 10295.34m),
                new("2026-09-15", 10295.34m, 42.90m, 0.00m, 0.00m, 0.00m, 10338.24m),
                new("2026-10-15", 10338.24m, 43.08m, 0.00m, 0.00m, 0.00m, 10381.32m),
                new("2026-11-15", 10381.32m, 43.26m, 0.00m, 0.00m, 0.00m, 10424.58m),
                new("2026-12-15", 10424.58m, 43.44m, 0.00m, 0.00m, 0.00m, 10468.02m),
                new("2027-01-15", 10468.02m, 43.62m, 0.00m, 0.00m, 0.00m, 10511.64m),
                new("2027-02-15", 10511.64m, 43.80m, 0.00m, 0.00m, 0.00m, 10555.44m),
                new("2027-03-15", 10555.44m, 43.98m, 0.00m, 0.00m, 0.00m, 10599.42m),
                new("2027-04-15", 10599.42m, 44.16m, 0.00m, 0.00m, 0.00m, 10643.58m),
                new("2027-05-15", 10643.58m, 44.35m, 0.00m, 0.00m, 0.00m, 10687.93m),
                new("2027-06-15", 10687.93m, 44.53m, 0.00m, 0.00m, 0.00m, 10732.46m),
                new("2027-07-15", 10732.46m, 44.72m, 0.00m, 0.00m, 0.00m, 10777.18m),
                new("2027-08-15", 10777.18m, 44.90m, 0.00m, 0.00m, 0.00m, 10822.08m),
                new("2027-09-15", 10822.08m, 45.09m, 0.00m, 0.00m, 0.00m, 10867.17m),
                new("2027-10-15", 10867.17m, 45.28m, 0.00m, 0.00m, 0.00m, 10912.45m),
                new("2027-11-15", 10912.45m, 45.47m, 0.00m, 0.00m, 0.00m, 10957.92m),
                new("2027-12-15", 10957.92m, 45.66m, 0.00m, 0.00m, 0.00m, 11003.58m),
                new("2028-01-15", 11003.58m, 0.00m, 45.85m, 11003.58m, 11049.43m, 0.00m),
            ]),
        // Row 1 accrues 40000.00 x 5.5% x 1/12 = 183.33 after rounding.
        // Capitalising that interest gives a closing balance of 40183.33, with no payment.
        // Row 2 accrues 40183.33 x 5.5% x 7/90 = 171.90 after rounding.
        // Capitalising that interest gives a closing balance of 40355.23, with no payment.
        // Row 12 accrues 42082.71 x 5.5% x 31/360 = 199.31 after rounding.
        // At maturity, 42082.71 principal plus 199.31 interest gives a 42282.02 payment.
        new("Act360_monthEndClamp_12m", 40000.00m, 5.5m, "ACT/360", 12, PaymentFrequency.Monthly,
            "2026-01-01", "2026-01-31",
            [
                new("2026-01-31", 40000.00m, 183.33m, 0.00m, 0.00m, 0.00m, 40183.33m),
                new("2026-02-28", 40183.33m, 171.90m, 0.00m, 0.00m, 0.00m, 40355.23m),
                new("2026-03-31", 40355.23m, 191.13m, 0.00m, 0.00m, 0.00m, 40546.36m),
                new("2026-04-30", 40546.36m, 185.84m, 0.00m, 0.00m, 0.00m, 40732.20m),
                new("2026-05-31", 40732.20m, 192.91m, 0.00m, 0.00m, 0.00m, 40925.11m),
                new("2026-06-30", 40925.11m, 187.57m, 0.00m, 0.00m, 0.00m, 41112.68m),
                new("2026-07-31", 41112.68m, 194.71m, 0.00m, 0.00m, 0.00m, 41307.39m),
                new("2026-08-31", 41307.39m, 195.64m, 0.00m, 0.00m, 0.00m, 41503.03m),
                new("2026-09-30", 41503.03m, 190.22m, 0.00m, 0.00m, 0.00m, 41693.25m),
                new("2026-10-31", 41693.25m, 197.46m, 0.00m, 0.00m, 0.00m, 41890.71m),
                new("2026-11-30", 41890.71m, 192.00m, 0.00m, 0.00m, 0.00m, 42082.71m),
                new("2026-12-31", 42082.71m, 0.00m, 199.31m, 42082.71m, 42282.02m, 0.00m),
            ]),
        // Row 1 accrues 10000.00 x 6% x 31/365 = 50.96 after rounding.
        // Capitalising that interest gives a closing balance of 10050.96, with no payment.
        // Row 2 accrues 10050.96 x 6% x 31/365 = 51.22 after rounding.
        // Capitalising that interest gives a closing balance of 10102.18, with no payment.
        // Row 12 accrues 10566.42 x 6% x 6/73 = 52.11 after rounding.
        // At maturity, 10566.42 principal plus 52.11 interest gives a 10618.53 payment.
        new("Act365Fixed_leapYear_12m", 10000.00m, 6m, "ACT/365 Fixed", 12, PaymentFrequency.Monthly,
            "2027-12-10", "2028-01-10",
            [
                new("2028-01-10", 10000.00m, 50.96m, 0.00m, 0.00m, 0.00m, 10050.96m),
                new("2028-02-10", 10050.96m, 51.22m, 0.00m, 0.00m, 0.00m, 10102.18m),
                new("2028-03-10", 10102.18m, 48.16m, 0.00m, 0.00m, 0.00m, 10150.34m),
                new("2028-04-10", 10150.34m, 51.73m, 0.00m, 0.00m, 0.00m, 10202.07m),
                new("2028-05-10", 10202.07m, 50.31m, 0.00m, 0.00m, 0.00m, 10252.38m),
                new("2028-06-10", 10252.38m, 52.25m, 0.00m, 0.00m, 0.00m, 10304.63m),
                new("2028-07-10", 10304.63m, 50.82m, 0.00m, 0.00m, 0.00m, 10355.45m),
                new("2028-08-10", 10355.45m, 52.77m, 0.00m, 0.00m, 0.00m, 10408.22m),
                new("2028-09-10", 10408.22m, 53.04m, 0.00m, 0.00m, 0.00m, 10461.26m),
                new("2028-10-10", 10461.26m, 51.59m, 0.00m, 0.00m, 0.00m, 10512.85m),
                new("2028-11-10", 10512.85m, 53.57m, 0.00m, 0.00m, 0.00m, 10566.42m),
                new("2028-12-10", 10566.42m, 0.00m, 52.11m, 10566.42m, 10618.53m, 0.00m),
            ]),
        // Row 1 accrues 30000.00 x 6.25% x 16616/66795 = 466.43 after rounding.
        // Capitalising that interest gives a closing balance of 30466.43, with no payment.
        // Row 2 accrues 30466.43 x 6.25% x 46/183 = 478.64 after rounding.
        // Capitalising that interest gives a closing balance of 30945.07, with no payment.
        // Row 8 accrues 33440.54 x 6.25% x 91/365 = 521.08 after rounding.
        // At maturity, 33440.54 principal plus 521.08 interest gives a 33961.62 payment.
        new("QuarterlyActActIsda_24m", 30000.00m, 6.25m, "ACT/ACT (ISDA)", 24, PaymentFrequency.Quarterly,
            "2027-12-15", "2028-03-15",
            [
                new("2028-03-15", 30000.00m, 466.43m, 0.00m, 0.00m, 0.00m, 30466.43m),
                new("2028-06-15", 30466.43m, 478.64m, 0.00m, 0.00m, 0.00m, 30945.07m),
                new("2028-09-15", 30945.07m, 486.16m, 0.00m, 0.00m, 0.00m, 31431.23m),
                new("2028-12-15", 31431.23m, 488.43m, 0.00m, 0.00m, 0.00m, 31919.66m),
                new("2029-03-15", 31919.66m, 491.66m, 0.00m, 0.00m, 0.00m, 32411.32m),
                new("2029-06-15", 32411.32m, 510.59m, 0.00m, 0.00m, 0.00m, 32921.91m),
                new("2029-09-15", 32921.91m, 518.63m, 0.00m, 0.00m, 0.00m, 33440.54m),
                new("2029-12-15", 33440.54m, 0.00m, 521.08m, 33440.54m, 33961.62m, 0.00m),
            ]),
        // Row 1 accrues 20000.00 x 7% x 1/12 = 116.67 after rounding.
        // Capitalising that interest gives a closing balance of 20116.67, with no payment.
        // Row 2 accrues 20116.67 x 7% x 7/90 = 109.52 after rounding.
        // Capitalising that interest gives a closing balance of 20226.19, with no payment.
        // Row 12 accrues 21325.54 x 7% x 1/12 = 124.40 after rounding.
        // At maturity, 21325.54 principal plus 124.40 interest gives a 21449.94 payment.
        new("Thirty360Isda_monthEnd_12m", 20000.00m, 7m, "30/360 ISDA", 12, PaymentFrequency.Monthly,
            "2025-12-31", "2026-01-31",
            [
                new("2026-01-31", 20000.00m, 116.67m, 0.00m, 0.00m, 0.00m, 20116.67m),
                new("2026-02-28", 20116.67m, 109.52m, 0.00m, 0.00m, 0.00m, 20226.19m),
                new("2026-03-31", 20226.19m, 129.78m, 0.00m, 0.00m, 0.00m, 20355.97m),
                new("2026-04-30", 20355.97m, 118.74m, 0.00m, 0.00m, 0.00m, 20474.71m),
                new("2026-05-31", 20474.71m, 119.44m, 0.00m, 0.00m, 0.00m, 20594.15m),
                new("2026-06-30", 20594.15m, 120.13m, 0.00m, 0.00m, 0.00m, 20714.28m),
                new("2026-07-31", 20714.28m, 120.83m, 0.00m, 0.00m, 0.00m, 20835.11m),
                new("2026-08-31", 20835.11m, 121.54m, 0.00m, 0.00m, 0.00m, 20956.65m),
                new("2026-09-30", 20956.65m, 122.25m, 0.00m, 0.00m, 0.00m, 21078.90m),
                new("2026-10-31", 21078.90m, 122.96m, 0.00m, 0.00m, 0.00m, 21201.86m),
                new("2026-11-30", 21201.86m, 123.68m, 0.00m, 0.00m, 0.00m, 21325.54m),
                new("2026-12-31", 21325.54m, 0.00m, 124.40m, 21325.54m, 21449.94m, 0.00m),
            ]),
        // Row 1 accrues 8000.00 x 8% x 17/120 = 90.67 after rounding.
        // Capitalising that interest gives a closing balance of 8090.67, with no payment.
        // Row 2 accrues 8090.67 x 8% x 1/12 = 53.94 after rounding.
        // Capitalising that interest gives a closing balance of 8144.61, with no payment.
        // Row 12 accrues 8646.52 x 8% x 1/12 = 57.64 after rounding.
        // At maturity, 8646.52 principal plus 57.64 interest gives a 8704.16 payment.
        new("Level30E360_brokenFirstPeriod_12m", 8000.00m, 8m, "30E/360", 12, PaymentFrequency.Monthly,
            "2026-01-10", "2026-03-01",
            [
                new("2026-03-01", 8000.00m, 90.67m, 0.00m, 0.00m, 0.00m, 8090.67m),
                new("2026-04-01", 8090.67m, 53.94m, 0.00m, 0.00m, 0.00m, 8144.61m),
                new("2026-05-01", 8144.61m, 54.30m, 0.00m, 0.00m, 0.00m, 8198.91m),
                new("2026-06-01", 8198.91m, 54.66m, 0.00m, 0.00m, 0.00m, 8253.57m),
                new("2026-07-01", 8253.57m, 55.02m, 0.00m, 0.00m, 0.00m, 8308.59m),
                new("2026-08-01", 8308.59m, 55.39m, 0.00m, 0.00m, 0.00m, 8363.98m),
                new("2026-09-01", 8363.98m, 55.76m, 0.00m, 0.00m, 0.00m, 8419.74m),
                new("2026-10-01", 8419.74m, 56.13m, 0.00m, 0.00m, 0.00m, 8475.87m),
                new("2026-11-01", 8475.87m, 56.51m, 0.00m, 0.00m, 0.00m, 8532.38m),
                new("2026-12-01", 8532.38m, 56.88m, 0.00m, 0.00m, 0.00m, 8589.26m),
                new("2027-01-01", 8589.26m, 57.26m, 0.00m, 0.00m, 0.00m, 8646.52m),
                new("2027-02-01", 8646.52m, 0.00m, 57.64m, 8646.52m, 8704.16m, 0.00m),
            ]),
        // Row 1 accrues 7500.00 x 3% x 1/4 = 56.25 after rounding.
        // At maturity, 7500.00 principal plus 56.25 interest gives a 7556.25 payment.
        new("SinglePayment_quarterly", 7500.00m, 3m, "ACT/360", 3, PaymentFrequency.Quarterly,
            "2026-01-15", "2026-04-15",
            [
                new("2026-04-15", 7500.00m, 0.00m, 56.25m, 7500.00m, 7556.25m, 0.00m),
            ]),
        // Row 1 accrues 1.00 x 0% x 1/12 = 0.00 after rounding.
        // Capitalising that interest gives a closing balance of 1.00, with no payment.
        // Row 2 accrues 1.00 x 0% x 1/12 = 0.00 after rounding.
        // Capitalising that interest gives a closing balance of 1.00, with no payment.
        // Row 5 accrues 1.00 x 0% x 1/12 = 0.00 after rounding.
        // At maturity, 1.00 principal plus 0.00 interest gives a 1.00 payment.
        new("TinyPrincipal_zeroRate_5m", 1.00m, 0m, "30E/360", 5, PaymentFrequency.Monthly,
            "2026-01-15", "2026-02-15",
            [
                new("2026-02-15", 1.00m, 0.00m, 0.00m, 0.00m, 0.00m, 1.00m),
                new("2026-03-15", 1.00m, 0.00m, 0.00m, 0.00m, 0.00m, 1.00m),
                new("2026-04-15", 1.00m, 0.00m, 0.00m, 0.00m, 0.00m, 1.00m),
                new("2026-05-15", 1.00m, 0.00m, 0.00m, 0.00m, 0.00m, 1.00m),
                new("2026-06-15", 1.00m, 0.00m, 0.00m, 1.00m, 1.00m, 0.00m),
            ]),
        // The first accrual is exactly 0.065. The nearest even cent is 0.06.
        // Row 1 accrues 1950.00 x 0.06% x 1/18 = 0.06 after rounding.
        // Capitalising that interest gives a closing balance of 1950.06, with no payment.
        // Row 2 accrues 1950.06 x 0.06% x 31/360 = 0.10 after rounding.
        // At maturity, 1950.06 principal plus 0.10 interest gives a 1950.16 payment.
        new("Act360HalfCentTie", 1950.00m, 0.06m, "ACT/360", 2, PaymentFrequency.Monthly,
            "2024-01-01", "2024-01-21",
            [
                new("2024-01-21", 1950.00m, 0.06m, 0.00m, 0.00m, 0.00m, 1950.06m),
                new("2024-02-21", 1950.06m, 0.00m, 0.10m, 1950.06m, 1950.16m, 0.00m),
            ]),
    ];

    /// <summary>Gets the names of the complete zero-coupon bullet schedules.</summary>
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
        var rows = RequireSchedule(Generator, ToRequest(vector)).Rows;
        Assert.Equal(vector.Rows.Length, rows.Count);

        for (var index = 0; index < rows.Count; index++)
        {
            var expected = vector.Rows[index];
            var expectedRow = new ScheduleRow(index + 1, Date(expected.Date), Euro(expected.Opening),
                Euro(expected.Capitalised), Euro(expected.Interest), Euro(expected.Principal), Euro(expected.Total), Euro(expected.Closing));
            Assert.Equal(expectedRow, rows[index]);
        }
    }

    [Fact]
    public Task Generate_canonicalLoan_matchesSnapshotFromApprovedVector()
    {
        var vector = Vectors.Single(entry => string.Equals(entry.Name, "Level30E360_24m", StringComparison.Ordinal));
        var request = ToRequest(vector);
        var rows = RequireSchedule(Generator, request).Rows;
        return VerifySnapshot("ZeroCoupon.Level30E360_24m", request, rows);
    }

    private static readonly BulletScheduleGenerator Generator = new(new InterestCalculator(), capitaliseInterest: true);

    private static ScheduleRequest ToRequest(Vector vector) =>
        Request(vector.Principal, vector.RatePercent, vector.Convention, vector.Months, vector.Frequency,
            Date(vector.Disbursement), Date(vector.FirstPayment));

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

    private sealed record Row(
        string Date,
        decimal Opening,
        decimal Capitalised,
        decimal Interest,
        decimal Principal,
        decimal Total,
        decimal Closing);
}
