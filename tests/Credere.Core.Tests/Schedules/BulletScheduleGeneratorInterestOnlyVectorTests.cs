using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using NodaTime;
using NodaTime.Text;
using Xunit;

namespace Credere.Core.Tests.Schedules;

/// <summary>
/// Formula-derived reference schedules for the interest-only bullet mode in docs/domain/schedules.md, §3.
/// </summary>
/// <remarks>
/// <para>
/// California State Board of Equalization, Assessors' Handbook §503, Problem 7, p. 41,
/// illustrates periodic interest on a constant balance and repayment of the entire principal at maturity.
/// Credere uses monthly or quarterly payments under schedules.md, “Periods and interest.”
/// </para>
/// <para>
/// Interest fractions follow ISDA 2006 Definitions §4.16(b), (d), (e), (f) and (g).
/// Periodic interest is rounded to cents with ties to even per docs/domain/value-objects.md, “Money.”
/// The loan inputs reuse the M1-14 schedule fixtures. The half-cent case uses the accrual inputs from
/// <c>ExactInterestTests.ExactRoundingVectors</c>. Every row was calculated using exact rational arithmetic
/// before invoking any Credere code.
/// </para>
/// </remarks>
public sealed class BulletScheduleGeneratorInterestOnlyVectorTests
{
    private static readonly Vector[] Vectors =
    [
        // Row 1 interest is 10000.00 x 5% x 1/12, which rounds to 41.67.
        // Principal due is 0.00, payment is 41.67, and closing balance is 10000.00.
        // Row 2 interest is 10000.00 x 5% x 1/12, which rounds to 41.67.
        // Principal due is 0.00, payment is 41.67, and closing balance is 10000.00.
        // Row 24 interest is 10000.00 x 5% x 1/12, which rounds to 41.67.
        // Principal due is 10000.00, payment is 10041.67, and closing balance is 0.00.
        new("Level30E360_24m", 10000.00m, 5m, "30E/360", 24, PaymentFrequency.Monthly,
            "2026-01-15", "2026-02-15",
            [
                new("2026-02-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2026-03-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2026-04-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2026-05-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2026-06-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2026-07-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2026-08-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2026-09-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2026-10-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2026-11-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2026-12-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2027-01-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2027-02-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2027-03-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2027-04-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2027-05-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2027-06-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2027-07-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2027-08-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2027-09-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2027-10-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2027-11-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2027-12-15", 10000.00m, 41.67m, 0.00m, 41.67m, 10000.00m),
                new("2028-01-15", 10000.00m, 41.67m, 10000.00m, 10041.67m, 0.00m),
            ]),
        // Row 1 interest is 40000.00 x 5.5% x 1/12, which rounds to 183.33.
        // Principal due is 0.00, payment is 183.33, and closing balance is 40000.00.
        // Row 2 interest is 40000.00 x 5.5% x 7/90, which rounds to 171.11.
        // Principal due is 0.00, payment is 171.11, and closing balance is 40000.00.
        // Row 12 interest is 40000.00 x 5.5% x 31/360, which rounds to 189.44.
        // Principal due is 40000.00, payment is 40189.44, and closing balance is 0.00.
        new("Act360_monthEndClamp_12m", 40000.00m, 5.5m, "ACT/360", 12, PaymentFrequency.Monthly,
            "2026-01-01", "2026-01-31",
            [
                new("2026-01-31", 40000.00m, 183.33m, 0.00m, 183.33m, 40000.00m),
                new("2026-02-28", 40000.00m, 171.11m, 0.00m, 171.11m, 40000.00m),
                new("2026-03-31", 40000.00m, 189.44m, 0.00m, 189.44m, 40000.00m),
                new("2026-04-30", 40000.00m, 183.33m, 0.00m, 183.33m, 40000.00m),
                new("2026-05-31", 40000.00m, 189.44m, 0.00m, 189.44m, 40000.00m),
                new("2026-06-30", 40000.00m, 183.33m, 0.00m, 183.33m, 40000.00m),
                new("2026-07-31", 40000.00m, 189.44m, 0.00m, 189.44m, 40000.00m),
                new("2026-08-31", 40000.00m, 189.44m, 0.00m, 189.44m, 40000.00m),
                new("2026-09-30", 40000.00m, 183.33m, 0.00m, 183.33m, 40000.00m),
                new("2026-10-31", 40000.00m, 189.44m, 0.00m, 189.44m, 40000.00m),
                new("2026-11-30", 40000.00m, 183.33m, 0.00m, 183.33m, 40000.00m),
                new("2026-12-31", 40000.00m, 189.44m, 40000.00m, 40189.44m, 0.00m),
            ]),
        // Row 1 interest is 10000.00 x 6% x 31/365, which rounds to 50.96.
        // Principal due is 0.00, payment is 50.96, and closing balance is 10000.00.
        // Row 2 interest is 10000.00 x 6% x 31/365, which rounds to 50.96.
        // Principal due is 0.00, payment is 50.96, and closing balance is 10000.00.
        // Row 12 interest is 10000.00 x 6% x 6/73, which rounds to 49.32.
        // Principal due is 10000.00, payment is 10049.32, and closing balance is 0.00.
        new("Act365Fixed_leapYear_12m", 10000.00m, 6m, "ACT/365 Fixed", 12, PaymentFrequency.Monthly,
            "2027-12-10", "2028-01-10",
            [
                new("2028-01-10", 10000.00m, 50.96m, 0.00m, 50.96m, 10000.00m),
                new("2028-02-10", 10000.00m, 50.96m, 0.00m, 50.96m, 10000.00m),
                new("2028-03-10", 10000.00m, 47.67m, 0.00m, 47.67m, 10000.00m),
                new("2028-04-10", 10000.00m, 50.96m, 0.00m, 50.96m, 10000.00m),
                new("2028-05-10", 10000.00m, 49.32m, 0.00m, 49.32m, 10000.00m),
                new("2028-06-10", 10000.00m, 50.96m, 0.00m, 50.96m, 10000.00m),
                new("2028-07-10", 10000.00m, 49.32m, 0.00m, 49.32m, 10000.00m),
                new("2028-08-10", 10000.00m, 50.96m, 0.00m, 50.96m, 10000.00m),
                new("2028-09-10", 10000.00m, 50.96m, 0.00m, 50.96m, 10000.00m),
                new("2028-10-10", 10000.00m, 49.32m, 0.00m, 49.32m, 10000.00m),
                new("2028-11-10", 10000.00m, 50.96m, 0.00m, 50.96m, 10000.00m),
                new("2028-12-10", 10000.00m, 49.32m, 10000.00m, 10049.32m, 0.00m),
            ]),
        // Row 1 interest is 30000.00 x 6.25% x 16616/66795, which rounds to 466.43.
        // Principal due is 0.00, payment is 466.43, and closing balance is 30000.00.
        // Row 2 interest is 30000.00 x 6.25% x 46/183, which rounds to 471.31.
        // Principal due is 0.00, payment is 471.31, and closing balance is 30000.00.
        // Row 8 interest is 30000.00 x 6.25% x 91/365, which rounds to 467.47.
        // Principal due is 30000.00, payment is 30467.47, and closing balance is 0.00.
        new("QuarterlyActActIsda_24m", 30000.00m, 6.25m, "ACT/ACT (ISDA)", 24, PaymentFrequency.Quarterly,
            "2027-12-15", "2028-03-15",
            [
                new("2028-03-15", 30000.00m, 466.43m, 0.00m, 466.43m, 30000.00m),
                new("2028-06-15", 30000.00m, 471.31m, 0.00m, 471.31m, 30000.00m),
                new("2028-09-15", 30000.00m, 471.31m, 0.00m, 471.31m, 30000.00m),
                new("2028-12-15", 30000.00m, 466.19m, 0.00m, 466.19m, 30000.00m),
                new("2029-03-15", 30000.00m, 462.09m, 0.00m, 462.09m, 30000.00m),
                new("2029-06-15", 30000.00m, 472.60m, 0.00m, 472.60m, 30000.00m),
                new("2029-09-15", 30000.00m, 472.60m, 0.00m, 472.60m, 30000.00m),
                new("2029-12-15", 30000.00m, 467.47m, 30000.00m, 30467.47m, 0.00m),
            ]),
        // Row 1 interest is 20000.00 x 7% x 1/12, which rounds to 116.67.
        // Principal due is 0.00, payment is 116.67, and closing balance is 20000.00.
        // Row 2 interest is 20000.00 x 7% x 7/90, which rounds to 108.89.
        // Principal due is 0.00, payment is 108.89, and closing balance is 20000.00.
        // Row 12 interest is 20000.00 x 7% x 1/12, which rounds to 116.67.
        // Principal due is 20000.00, payment is 20116.67, and closing balance is 0.00.
        new("Thirty360Isda_monthEnd_12m", 20000.00m, 7m, "30/360 ISDA", 12, PaymentFrequency.Monthly,
            "2025-12-31", "2026-01-31",
            [
                new("2026-01-31", 20000.00m, 116.67m, 0.00m, 116.67m, 20000.00m),
                new("2026-02-28", 20000.00m, 108.89m, 0.00m, 108.89m, 20000.00m),
                new("2026-03-31", 20000.00m, 128.33m, 0.00m, 128.33m, 20000.00m),
                new("2026-04-30", 20000.00m, 116.67m, 0.00m, 116.67m, 20000.00m),
                new("2026-05-31", 20000.00m, 116.67m, 0.00m, 116.67m, 20000.00m),
                new("2026-06-30", 20000.00m, 116.67m, 0.00m, 116.67m, 20000.00m),
                new("2026-07-31", 20000.00m, 116.67m, 0.00m, 116.67m, 20000.00m),
                new("2026-08-31", 20000.00m, 116.67m, 0.00m, 116.67m, 20000.00m),
                new("2026-09-30", 20000.00m, 116.67m, 0.00m, 116.67m, 20000.00m),
                new("2026-10-31", 20000.00m, 116.67m, 0.00m, 116.67m, 20000.00m),
                new("2026-11-30", 20000.00m, 116.67m, 0.00m, 116.67m, 20000.00m),
                new("2026-12-31", 20000.00m, 116.67m, 20000.00m, 20116.67m, 0.00m),
            ]),
        // Row 1 interest is 8000.00 x 8% x 17/120, which rounds to 90.67.
        // Principal due is 0.00, payment is 90.67, and closing balance is 8000.00.
        // Row 2 interest is 8000.00 x 8% x 1/12, which rounds to 53.33.
        // Principal due is 0.00, payment is 53.33, and closing balance is 8000.00.
        // Row 12 interest is 8000.00 x 8% x 1/12, which rounds to 53.33.
        // Principal due is 8000.00, payment is 8053.33, and closing balance is 0.00.
        new("Level30E360_brokenFirstPeriod_12m", 8000.00m, 8m, "30E/360", 12, PaymentFrequency.Monthly,
            "2026-01-10", "2026-03-01",
            [
                new("2026-03-01", 8000.00m, 90.67m, 0.00m, 90.67m, 8000.00m),
                new("2026-04-01", 8000.00m, 53.33m, 0.00m, 53.33m, 8000.00m),
                new("2026-05-01", 8000.00m, 53.33m, 0.00m, 53.33m, 8000.00m),
                new("2026-06-01", 8000.00m, 53.33m, 0.00m, 53.33m, 8000.00m),
                new("2026-07-01", 8000.00m, 53.33m, 0.00m, 53.33m, 8000.00m),
                new("2026-08-01", 8000.00m, 53.33m, 0.00m, 53.33m, 8000.00m),
                new("2026-09-01", 8000.00m, 53.33m, 0.00m, 53.33m, 8000.00m),
                new("2026-10-01", 8000.00m, 53.33m, 0.00m, 53.33m, 8000.00m),
                new("2026-11-01", 8000.00m, 53.33m, 0.00m, 53.33m, 8000.00m),
                new("2026-12-01", 8000.00m, 53.33m, 0.00m, 53.33m, 8000.00m),
                new("2027-01-01", 8000.00m, 53.33m, 0.00m, 53.33m, 8000.00m),
                new("2027-02-01", 8000.00m, 53.33m, 8000.00m, 8053.33m, 0.00m),
            ]),
        // Row 1 interest is 7500.00 x 3% x 1/4, which rounds to 56.25.
        // Principal due is 7500.00, payment is 7556.25, and closing balance is 0.00.
        new("SinglePayment_quarterly", 7500.00m, 3m, "ACT/360", 3, PaymentFrequency.Quarterly,
            "2026-01-15", "2026-04-15",
            [
                new("2026-04-15", 7500.00m, 56.25m, 7500.00m, 7556.25m, 0.00m),
            ]),
        // Row 1 interest is 1.00 x 0% x 1/12, which rounds to 0.00.
        // Principal due is 0.00, payment is 0.00, and closing balance is 1.00.
        // Row 2 interest is 1.00 x 0% x 1/12, which rounds to 0.00.
        // Principal due is 0.00, payment is 0.00, and closing balance is 1.00.
        // Row 5 interest is 1.00 x 0% x 1/12, which rounds to 0.00.
        // Principal due is 1.00, payment is 1.00, and closing balance is 0.00.
        new("TinyPrincipal_zeroRate_5m", 1.00m, 0m, "30E/360", 5, PaymentFrequency.Monthly,
            "2026-01-15", "2026-02-15",
            [
                new("2026-02-15", 1.00m, 0.00m, 0.00m, 0.00m, 1.00m),
                new("2026-03-15", 1.00m, 0.00m, 0.00m, 0.00m, 1.00m),
                new("2026-04-15", 1.00m, 0.00m, 0.00m, 0.00m, 1.00m),
                new("2026-05-15", 1.00m, 0.00m, 0.00m, 0.00m, 1.00m),
                new("2026-06-15", 1.00m, 0.00m, 1.00m, 1.00m, 0.00m),
            ]),
        // The first interest amount is exactly 0.065. Its nearest even cent is 0.06.
        // Row 1 interest is 1950.00 x 0.06% x 1/18, which rounds to 0.06.
        // Principal due is 0.00, payment is 0.06, and closing balance is 1950.00.
        // Row 2 interest is 1950.00 x 0.06% x 31/360, which rounds to 0.10.
        // Principal due is 1950.00, payment is 1950.10, and closing balance is 0.00.
        new("Act360HalfCentTie", 1950.00m, 0.06m, "ACT/360", 2, PaymentFrequency.Monthly,
            "2024-01-01", "2024-01-21",
            [
                new("2024-01-21", 1950.00m, 0.06m, 0.00m, 0.06m, 1950.00m),
                new("2024-02-21", 1950.00m, 0.10m, 1950.00m, 1950.10m, 0.00m),
            ]),
    ];

    /// <summary>Gets the names of the complete interest-only bullet schedules.</summary>
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
    public void ReferenceSchedule_matchesTheSharedPeriodInterestAndRowContracts(string name)
    {
        var vector = Vectors.Single(entry => entry.Name == name);
        DayCountConvention convention = vector.Convention switch
        {
            "ACT/360" => new Act360(),
            "ACT/365 Fixed" => new Act365Fixed(),
            "ACT/ACT (ISDA)" => new ActActIsda(),
            "30/360 ISDA" => new Thirty360Isda(),
            "30E/360" => new ThirtyE360(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), vector.Convention, "Unknown reference convention."),
        };
        var rate = FixedRate.Create(new Percentage(vector.RatePercent), convention).Value;
        var principal = Euro(vector.Principal);
        var disbursement = Date(vector.Disbursement);
        var firstPayment = Date(vector.FirstPayment);
        var term = LoanTerm.FromMonths(vector.Months);
        Assert.True(ScheduleRequest.Create(principal, rate, term, disbursement, firstPayment, vector.Frequency).IsSuccess);

        var rowCount = SchedulePeriods.RowCount(term, vector.Frequency);
        Assert.Equal(rowCount, vector.Rows.Length);
        var periods = SchedulePeriods.Periods(disbursement, firstPayment, vector.Frequency, rowCount);
        var calculator = new InterestCalculator();
        var rows = new List<ScheduleRow>(rowCount);
        var zero = Money.Zero(principal.Currency);
        for (var index = 0; index < rowCount; index++)
        {
            var expected = vector.Rows[index];
            var date = Date(expected.Date);
            var opening = Euro(expected.Opening);
            var interest = Euro(expected.Interest);
            Assert.Equal(date, periods[index].End);
            Assert.Equal(principal, opening);
            Assert.Equal(interest, calculator.Calculate(rate, opening, periods[index].Start, periods[index].End));

            rows.Add(new ScheduleRow(index + 1, date, opening, zero, interest,
                Euro(expected.Principal), Euro(expected.Total), Euro(expected.Closing)));
        }

        var schedule = AmortizationSchedule.Create(principal, rows);
        Assert.True(schedule.IsSuccess, schedule.IsSuccess ? string.Empty : schedule.Error.ToString());
        Assert.All(rows.Take(rowCount - 1), row =>
        {
            Assert.Equal(zero, row.PrincipalDue);
            Assert.Equal(row.InterestDue, row.TotalPayment);
        });
        Assert.Equal(principal, rows[^1].PrincipalDue);
    }

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
