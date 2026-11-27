using Credere.Core.DayCounts;
using Credere.Core.Schedules;
using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Domain;

/// <summary>The immutable configuration copied onto a loan at disbursement.</summary>
internal sealed record ProductTerms
{
    internal ProductTerms(
        DayCountConvention dayCount,
        PaymentFrequency frequency,
        RepaymentType repayment,
        int pastDueThreshold,
        int defaultThreshold,
        string calendar)
    {
        ArgumentNullException.ThrowIfNull(dayCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pastDueThreshold);
        ArgumentOutOfRangeException.ThrowIfLessThan(defaultThreshold, pastDueThreshold);
        ArgumentException.ThrowIfNullOrWhiteSpace(calendar);
        if (!Enum.IsDefined(frequency))
        {
            throw new ArgumentOutOfRangeException(nameof(frequency));
        }

        if (!Enum.IsDefined(repayment))
        {
            throw new ArgumentOutOfRangeException(nameof(repayment));
        }

        DayCount = dayCount;
        Frequency = frequency;
        Repayment = repayment;
        PastDueThreshold = pastDueThreshold;
        DefaultThreshold = defaultThreshold;
        Calendar = calendar;
    }

    internal DayCountConvention DayCount { get; }

    internal PaymentFrequency Frequency { get; }

    internal RepaymentType Repayment { get; }

    internal int PastDueThreshold { get; }

    internal int DefaultThreshold { get; }

    internal string Calendar { get; }
}
