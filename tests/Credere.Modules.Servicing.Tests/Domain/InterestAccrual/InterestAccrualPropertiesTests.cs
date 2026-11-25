using Credere.Modules.Servicing.Domain;
using FsCheck;
using FsCheck.Xunit;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;

namespace Credere.Modules.Servicing.Tests.Domain;

public sealed class InterestAccrualPropertiesTests
{
    // D5, docs/modules/servicing.md, "Interest accrual and posting", rule 3.
    [Property(MaxTest = 300)]
    public bool Project_IrregularPostingDates_MatchTheSingleRun(
        NonNegativeInt period, NonNegativeInt daysBeforeDueDate, PositiveInt firstInterval, PositiveInt[] laterIntervals)
    {
        var position = At();
        var dueDate = Schedule.Rows[period.Get % Schedule.Rows.Count].PaymentDate;
        var end = dueDate.PlusDays(-(daysBeforeDueDate.Get % 28));
        var single = InterestAccrual.Project(position, end, Principal, Rate, Schedule, Calculator);
        var posted = Euro(0m);

        foreach (var interval in laterIntervals.Prepend(firstInterval))
        {
            var nextDate = position.AccruedThroughDate.PlusDays(1 + (interval.Get - 1) % 62);
            if (nextDate >= end)
            {
                break;
            }

            var projection = InterestAccrual.Project(position, nextDate, Principal, Rate, Schedule, Calculator);
            posted += projection.Accrued;
            position = projection.Position;
        }

        var final = InterestAccrual.Project(position, end, Principal, Rate, Schedule, Calculator);

        return posted + final.Accrued == single.Accrued
            && final.Position == single.Position;
    }

    // D5, docs/modules/servicing.md, "Interest accrual and posting", rule 6.
    [Property(MaxTest = 300)]
    public bool Project_InterestPaidOnEachDueDate_MatchesTheSchedule(PositiveInt periods)
    {
        var periodsToPay = 1 + (periods.Get - 1) % Schedule.Rows.Count;
        var position = At();
        var zero = Euro(0m);

        foreach (var row in Schedule.Rows.Take(periodsToPay))
        {
            var projection = InterestAccrual.Project(position, row.PaymentDate, Principal, Rate, Schedule, Calculator);
            if (projection.Accrued != row.InterestDue
                || projection.Position.CurrentInterest != row.InterestDue
                || projection.Position.PastDueInterest != zero)
            {
                return false;
            }

            // The interest-only payment clears the current bucket at this due date.
            position = projection.Position with { CurrentInterest = zero };
        }

        return true;
    }
}
