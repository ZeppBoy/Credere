using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using FsCheck.Xunit;
using NodaTime;
using NodaTime.Text;
using Xunit;

namespace Credere.Core.Tests.Schedules;

/// <summary>
/// Reference data for the payment-date and interest-period logic shared by all schedule generators
/// (docs/domain/schedules.md, "Periods and interest"). The owner approved these vectors (PR #43,
/// 2026-11-20) before the implementation was written.
/// </summary>
/// <remarks>
/// <para>
/// Rule under test: row <c>i</c> (1-based) falls on <c>FirstPaymentDate + ((i − 1) × 12 / periodsPerYear) months</c>,
/// always counted from <c>FirstPaymentDate</c> and never from the previous row, so a month-end start does not drift.
/// When the target month is shorter than the start day, the date is the last day of the target month. Row <c>i</c>
/// accrues from <c>periodStart_i</c> (inclusive) to <c>PaymentDate_i</c> (exclusive), where
/// <c>periodStart_1 = DisbursementDate</c> and <c>periodStart_i = PaymentDate_(i−1)</c>.
/// </para>
/// <para>
/// Sources: (1) the rule is docs/domain/schedules.md, "Periods and interest" (a Credere design rule, not a regulatory one);
/// (2) the expected dates are calendar facts: month lengths and the Gregorian leap-year rule (2028 is a leap year,
/// 2027 and 2100 are not, because 2100 is a century not divisible by 400). They were produced by Python's
/// <c>calendar.monthrange</c> and <c>datetime</c>, not by NodaTime or by the implementation, and checked by hand for the
/// first two rows of each group. Python's <c>calendar</c>/<c>datetime</c> and NodaTime's <c>LocalDate.PlusMonths</c>
/// (which truncates to the last valid day of the month) are independent implementations of the same calendar.
/// </para>
/// </remarks>
public sealed class SchedulePeriodsTests
{
    /// <summary>
    /// Gets ISO first payment date, periods per year (12 monthly, 4 quarterly), 1-based row index and the expected
    /// ISO payment date of that row.
    /// </summary>
    public static TheoryData<string, int, int, string> PaymentDateVectors => new()
    {
        // Monthly from 31 January 2026: every row counted from the 31st, so no drift after February.
        { "2026-01-31", 12, 1, "2026-01-31" }, // Row 1 is the first payment date itself.
        { "2026-01-31", 12, 2, "2026-02-28" }, // February 2026 has 28 days.
        { "2026-01-31", 12, 3, "2026-03-31" }, // Back to the 31st, not 28 March.
        { "2026-01-31", 12, 4, "2026-04-30" }, // April has 30 days.
        { "2026-01-31", 12, 5, "2026-05-31" },
        { "2026-01-31", 12, 6, "2026-06-30" },
        { "2026-01-31", 12, 7, "2026-07-31" },
        { "2026-01-31", 12, 8, "2026-08-31" },
        { "2026-01-31", 12, 9, "2026-09-30" },
        { "2026-01-31", 12, 10, "2026-10-31" },
        { "2026-01-31", 12, 11, "2026-11-30" },
        { "2026-01-31", 12, 12, "2026-12-31" },
        { "2026-01-31", 12, 13, "2027-01-31" }, // Year rollover.
        { "2026-01-31", 12, 14, "2027-02-28" }, // 2027 is not a leap year.
        { "2026-01-31", 12, 26, "2028-02-29" }, // 25 months on: 2028 is a leap year, and the 29th is not clamped to 28.
        { "2026-01-31", 12, 360, "2055-12-31" }, // 359 months on: 29 years and 11 months, a 30-year loan's last row.

        // Leap years and century years.
        { "2028-01-31", 12, 2, "2028-02-29" }, // Leap February.
        { "2028-01-31", 12, 3, "2028-03-31" },
        { "2028-01-29", 12, 2, "2028-02-29" }, // Day 29 fits in a leap February.
        { "2027-01-29", 12, 2, "2027-02-28" }, // Day 29 does not fit in a common February.
        { "2027-01-30", 12, 2, "2027-02-28" },
        { "2028-01-30", 12, 2, "2028-02-29" },
        { "2100-01-31", 12, 2, "2100-02-28" }, // 2100 is divisible by 100 but not by 400, so it is not a leap year.
        { "2100-01-31", 12, 3, "2100-03-31" },

        // Mid-month and year-end starts.
        { "2026-11-15", 12, 1, "2026-11-15" },
        { "2026-11-15", 12, 2, "2026-12-15" },
        { "2026-11-15", 12, 3, "2027-01-15" }, // Day 15 never clamps.
        { "2026-12-31", 12, 2, "2027-01-31" },
        { "2026-12-31", 12, 3, "2027-02-28" },

        // Quarterly: 3 months per row.
        { "2026-01-31", 4, 1, "2026-01-31" },
        { "2026-01-31", 4, 2, "2026-04-30" },
        { "2026-01-31", 4, 3, "2026-07-31" },
        { "2026-01-31", 4, 4, "2026-10-31" },
        { "2026-01-31", 4, 5, "2027-01-31" },
        { "2026-01-31", 4, 120, "2055-10-31" }, // 357 months on: 29 years and 9 months.
        { "2026-11-30", 4, 2, "2027-02-28" },
        { "2026-11-30", 4, 3, "2027-05-30" }, // Day 30 fits in May, so no clamping.
        { "2026-11-30", 4, 4, "2027-08-30" },
        { "2027-11-30", 4, 2, "2028-02-29" }, // Leap February.
        { "2026-08-31", 4, 2, "2026-11-30" },
        { "2026-08-31", 4, 3, "2027-02-28" },
    };

    /// <summary>
    /// Gets ISO disbursement date, ISO first payment date, periods per year, number of rows, and the expected interest
    /// periods as <c>start|end</c> ISO pairs (start inclusive, end exclusive), one per row in order.
    /// </summary>
    public static TheoryData<string, string, int, int, string[]> InterestPeriodVectors => new()
    {
        // Broken first period (disbursed mid-month, first payment at month-end), monthly.
        {
            "2026-01-15", "2026-01-31", 12, 4,
            ["2026-01-15|2026-01-31", "2026-01-31|2026-02-28", "2026-02-28|2026-03-31", "2026-03-31|2026-04-30"]
        },

        // Quarterly with a long first period.
        { "2026-02-10", "2026-03-31", 4, 2, ["2026-02-10|2026-03-31", "2026-03-31|2026-06-30"] },

        // Across a leap day: the second period ends on 29 February, the third starts there.
        {
            "2027-12-20", "2028-01-31", 12, 3,
            ["2027-12-20|2028-01-31", "2028-01-31|2028-02-29", "2028-02-29|2028-03-31"]
        },
    };

    private static LocalDate Date(string iso) => LocalDatePattern.Iso.Parse(iso).Value;

    [Theory]
    [MemberData(nameof(PaymentDateVectors))]
    public void PaymentDate_countsFromTheFirstPaymentDate_clampingToTheEndOfShortMonths(
        string first, int periodsPerYear, int rowIndex, string expected)
    {
        var date = SchedulePeriods.PaymentDate(Date(first), (PaymentFrequency)periodsPerYear, rowIndex);

        Assert.Equal(Date(expected), date);
    }

    [Theory]
    [MemberData(nameof(InterestPeriodVectors))]
    public void Periods_runFromTheDisbursementDate_thenFromEachPaymentDate(
        string disbursement, string first, int periodsPerYear, int rowCount, string[] expected)
    {
        var periods = SchedulePeriods.Periods(
            Date(disbursement), Date(first), (PaymentFrequency)periodsPerYear, rowCount);

        Assert.Equal(
            expected,
            periods.Select(p => $"{LocalDatePattern.Iso.Format(p.Start)}|{LocalDatePattern.Iso.Format(p.End)}").ToArray());
    }

    [Theory]
    [InlineData(PaymentFrequency.Monthly, 24, 24)]
    [InlineData(PaymentFrequency.Monthly, 1, 1)]
    [InlineData(PaymentFrequency.Monthly, 600, 600)]
    [InlineData(PaymentFrequency.Quarterly, 24, 8)]
    [InlineData(PaymentFrequency.Quarterly, 3, 1)]
    [InlineData(PaymentFrequency.Quarterly, 600, 200)]
    public void RowCount_isTheTermInMonthsDividedByTheMonthsPerPeriod(
        PaymentFrequency frequency, int termMonths, int expected)
    {
        Assert.Equal(expected, SchedulePeriods.RowCount(LoanTerm.FromMonths(termMonths), frequency));
    }

    [Fact]
    public void RowCount_termThatIsNotWholePeriods_throws()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => SchedulePeriods.RowCount(LoanTerm.FromMonths(10), PaymentFrequency.Quarterly));

        Assert.Equal("term", exception.ParamName);
        Assert.Contains("whole number of payment periods", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RowCount_nullTerm_throws()
    {
        Assert.Throws<ArgumentNullException>(() => SchedulePeriods.RowCount(null!, PaymentFrequency.Monthly));
    }

    [Theory]
    [InlineData(PaymentFrequency.Monthly, 12, 1)]
    [InlineData(PaymentFrequency.Quarterly, 4, 3)]
    public void PeriodsPerYear_andMonthsPerPeriod_followTheFrequency(
        PaymentFrequency frequency, int periodsPerYear, int monthsPerPeriod)
    {
        Assert.Equal(periodsPerYear, SchedulePeriods.PeriodsPerYear(frequency));
        Assert.Equal(monthsPerPeriod, SchedulePeriods.MonthsPerPeriod(frequency));
    }

    [Fact]
    public void PeriodsPerYear_undefinedFrequency_throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => SchedulePeriods.PeriodsPerYear((PaymentFrequency)7));

        Assert.Equal("frequency", exception.ParamName);
        Assert.Contains("Not a defined payment frequency", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentDate_rowIndexBelowOne_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SchedulePeriods.PaymentDate(new LocalDate(2026, 1, 31), PaymentFrequency.Monthly, 0));
    }

    [Fact]
    public void Periods_rowCountBelowOne_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SchedulePeriods.Periods(
                new LocalDate(2026, 1, 1), new LocalDate(2026, 2, 1), PaymentFrequency.Monthly, 0));
    }

    // Property: for every first-payment day 1-31 in every month, every date over the longest term equals the first date
    // plus the months, clamped to the last day of the target month, computed here from year and month arithmetic.
    [Property]
    public bool PaymentDates_equalTheFirstDatePlusMonths_clampedToTheMonthEnd_andStrictlyIncrease(
        byte year, byte month, byte day, bool quarterly)
    {
        var frequency = quarterly ? PaymentFrequency.Quarterly : PaymentFrequency.Monthly;
        var monthsPerPeriod = quarterly ? 3 : 1;
        var startYear = 2000 + (year % 100);
        var startMonth = 1 + (month % 12);
        var startDay = Math.Min(1 + (day % 31), CalendarSystem.Iso.GetDaysInMonth(startYear, startMonth));
        var first = new LocalDate(startYear, startMonth, startDay);
        var rows = 600 / monthsPerPeriod;
        var previous = SchedulePeriods.PaymentDate(first, frequency, 1);

        if (previous != first)
        {
            return false;
        }

        for (var index = 2; index <= rows; index++)
        {
            var date = SchedulePeriods.PaymentDate(first, frequency, index);
            var monthIndex = (startYear * 12) + (startMonth - 1) + ((index - 1) * monthsPerPeriod);
            var (expectedYear, expectedMonth) = (monthIndex / 12, (monthIndex % 12) + 1);
            var expectedDay = Math.Min(startDay, CalendarSystem.Iso.GetDaysInMonth(expectedYear, expectedMonth));

            if (date != new LocalDate(expectedYear, expectedMonth, expectedDay) || date <= previous)
            {
                return false;
            }

            previous = date;
        }

        return true;
    }

    [Property]
    public bool Periods_areContiguous_andStartAtTheDisbursementDate(byte offset, byte gap, bool quarterly)
    {
        var frequency = quarterly ? PaymentFrequency.Quarterly : PaymentFrequency.Monthly;
        var disbursement = new LocalDate(2026, 1, 1).PlusDays(offset);
        var first = disbursement.PlusDays(1 + gap);

        var periods = SchedulePeriods.Periods(disbursement, first, frequency, 12);

        return periods[0].Start == disbursement
            && periods[0].End == first
            && periods.Skip(1).Select((period, i) => period.Start == periods[i].End).All(contiguous => contiguous);
    }

    // Other cases the tests must cover (docs/domain/schedules.md, "Public contract" and "Periods and interest"):
    //   Row count equals Term.Months for monthly and Term.Months / 3 for quarterly (RowCount tests above).
    //   The ScheduleRequest rules (first payment after disbursement, quarterly term a multiple of 3, strictly
    //   increasing dates) are in ScheduleRequestTests; the AmortizationSchedule invariants are in AmortizationScheduleTests.
}
