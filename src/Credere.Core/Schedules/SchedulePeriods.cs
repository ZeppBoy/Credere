using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Core.Schedules;

/// <summary>
/// The payment-date and interest-period logic shared by every schedule generator
/// (docs/domain/schedules.md, "Periods and interest").
/// </summary>
/// <remarks>
/// <para>
/// Row <c>i</c> (1-based) falls on <c>FirstPaymentDate</c> plus <c>(i − 1) × 12 / periodsPerYear</c> months. Each date is
/// counted from <c>FirstPaymentDate</c>, never from the previous row, so a month-end start does not drift: 31 January
/// gives 28 February, then 31 March. When the target month is shorter than the start day the date is the last day of
/// that month (NodaTime <see cref="LocalDate.PlusMonths"/>).
/// </para>
/// <para>
/// Row <c>i</c> accrues from <c>periodStart_i</c> (inclusive) to its payment date (exclusive), where
/// <c>periodStart_1</c> is the disbursement date and <c>periodStart_i</c> is the previous payment date. The rule is a
/// Credere design rule, not a regulatory one; the reference dates are in <c>SchedulePeriodsTests</c>.
/// </para>
/// </remarks>
internal static class SchedulePeriods
{
    /// <summary>Returns the number of payments a year.</summary>
    /// <param name="frequency">The payment frequency.</param>
    /// <returns>12 for monthly, 4 for quarterly.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frequency"/> is not a defined value, which is a programmer error.</exception>
    public static int PeriodsPerYear(PaymentFrequency frequency)
    {
        if (!Enum.IsDefined(frequency))
        {
            throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Not a defined payment frequency.");
        }

        return (int)frequency;
    }

    /// <summary>Returns the number of months between two payments.</summary>
    /// <param name="frequency">The payment frequency.</param>
    /// <returns>1 for monthly, 3 for quarterly.</returns>
    public static int MonthsPerPeriod(PaymentFrequency frequency) => MonthsPerYear / PeriodsPerYear(frequency);

    /// <summary>Returns the number of rows of a schedule: <c>Term.Months</c> divided by the months per period.</summary>
    /// <param name="term">The loan term.</param>
    /// <param name="frequency">The payment frequency.</param>
    /// <returns>The number of rows.</returns>
    /// <exception cref="ArgumentException">The term is not a whole number of periods; <see cref="ScheduleRequest.Create"/> rejects such a request, so this is a programmer error.</exception>
    public static int RowCount(LoanTerm term, PaymentFrequency frequency)
    {
        ArgumentNullException.ThrowIfNull(term);

        var monthsPerPeriod = MonthsPerPeriod(frequency);
        if (term.Months % monthsPerPeriod != 0)
        {
            throw new ArgumentException(
                "The term is not a whole number of payment periods.", nameof(term));
        }

        return term.Months / monthsPerPeriod;
    }

    /// <summary>Returns the payment date of a row, counted from the first payment date.</summary>
    /// <param name="firstPaymentDate">The date of row 1.</param>
    /// <param name="frequency">The payment frequency.</param>
    /// <param name="rowIndex">The 1-based row index.</param>
    /// <returns>The payment date of the row.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rowIndex"/> is below 1, which is a programmer error.</exception>
    public static LocalDate PaymentDate(LocalDate firstPaymentDate, PaymentFrequency frequency, int rowIndex)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rowIndex, 1);

        return firstPaymentDate.PlusMonths((rowIndex - 1) * MonthsPerPeriod(frequency));
    }

    /// <summary>Returns the interest periods of a schedule, one per row, in order.</summary>
    /// <param name="disbursementDate">The start of the first period.</param>
    /// <param name="firstPaymentDate">The end of the first period.</param>
    /// <param name="frequency">The payment frequency.</param>
    /// <param name="rowCount">The number of rows, at least 1.</param>
    /// <returns>The periods, each from its start (inclusive) to its payment date (exclusive).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rowCount"/> is below 1, which is a programmer error.</exception>
    public static IReadOnlyList<(LocalDate Start, LocalDate End)> Periods(
        LocalDate disbursementDate, LocalDate firstPaymentDate, PaymentFrequency frequency, int rowCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rowCount, 1);

        var periods = new List<(LocalDate Start, LocalDate End)>(rowCount);
        var start = disbursementDate;
        for (var index = 1; index <= rowCount; index++)
        {
            var end = PaymentDate(firstPaymentDate, frequency, index);
            periods.Add((start, end));
            start = end;
        }

        return periods;
    }

    private const int MonthsPerYear = 12;
}
