using Xunit;

namespace Credere.Core.Tests.Schedules;

/// <summary>
/// Reference data for the payment-date and interest-period logic shared by all schedule generators
/// (docs/domain/schedules.md, "Periods and interest"). The tests that use it come with the implementation,
/// after the owner has approved these vectors.
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

    // Other cases the tests must cover (docs/domain/schedules.md, "Public contract" and "Periods and interest"):
    //   Row count equals Term.Months for monthly and Term.Months / 3 for quarterly.
    //   Quarterly with a Term.Months that is not a multiple of 3 is rejected (the doc says Term.Months must be a multiple of 3).
    //   Payment dates are strictly increasing for every first-payment day 1-31 over a 600-month term (property).
    //   Each date equals FirstPaymentDate plus (i - 1) x 12 / periodsPerYear months, not the previous date plus one period (property).
    //   AmortizationSchedule invariants, each violated once and accepted once: non-empty Rows; first opening balance
    //   equals the principal; last closing balance is exactly zero; OpeningBalance - PrincipalDue == ClosingBalance;
    //   InterestDue + PrincipalDue == TotalPayment; ClosingBalance(i) == OpeningBalance(i + 1); dates increase.
    //   AmortizationSchedule equality compares the rows element by element, not the list reference (AGENTS.md §6).
    //
    // Not specified in the doc, so deliberately not given vectors yet (open questions in the PR description):
    //   Whether FirstPaymentDate must be after DisbursementDate (equal gives an empty first period; earlier gives a
    //   reversed one), and whether a maximum first-period length applies.
    //   Which error a request with a mismatched frequency and term returns (ValidationError field name).
    //   Whether the rows are monotonic when the invariant says "monotonically increasing": strictly or not.
}
