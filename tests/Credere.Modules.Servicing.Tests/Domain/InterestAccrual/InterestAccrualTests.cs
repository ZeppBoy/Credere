using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Domain;
using FluentAssertions;
using Xunit;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;

namespace Credere.Modules.Servicing.Tests.Domain;

public sealed class InterestAccrualTests
{
    // D5 and InterestAccrual/reference-vectors.md, "Monthly interest-only loan".
    public static TheoryData<string, string, decimal, decimal, decimal> FirstRunCases => new()
    {
        { "IA-01", "2026-01-15", 0m, 0m, 0m },
        { "IA-02", "2026-02-14", 40.28m, 40.28m, 0m },
        { "IA-03", "2026-02-15", 41.67m, 41.67m, 0m },
        { "IA-06", "2026-03-15", 83.34m, 41.67m, 41.67m },
        // IA-13: position starts at disbursement, including when the loan is activated later.
        { "IA-13", "2026-01-20", 6.94m, 6.94m, 0m },
        { "IA-15", "2026-01-31", 20.83m, 20.83m, 0m },
    };

    public static TheoryData<string, string, string, string, decimal, decimal, decimal, decimal, decimal> SubsequentRunCases => new()
    {
        { "IA-04", "2026-02-14", "2026-01-15", "2026-02-15", 40.28m, 10000m, 1.39m, 41.67m, 0m },
        { "IA-05", "2026-02-15", "2026-02-15", "2026-02-16", 41.67m, 10000m, 1.39m, 1.39m, 41.67m },
        { "IA-07", "2026-01-17", "2026-01-15", "2026-01-20", 0m, 10000m, 4.16m, 4.16m, 0m },
        { "IA-08", "2026-01-17", "2026-01-17", "2026-01-20", 0m, 5000m, 2.08m, 2.08m, 0m },
        { "IA-10", "2026-02-15", "2026-02-15", "2026-02-16", 40.28m, 10000m, 1.39m, 1.39m, 40.28m },
        { "IA-14", "2026-02-16", "2026-02-16", "2026-02-20", 0m, 9000m, 5m, 5m, 0m },
    };

    // D5 and InterestAccrual/reference-vectors.md, "Exact midpoint rounding".
    public static TheoryData<string, string, decimal, decimal, bool, decimal> MidpointCases => new()
    {
        { "IA-11", "2024-01-21", 1950m, 0.06m, false, 0.06m },
        { "IA-12", "2024-01-06", 73m, 1.50m, true, 0.02m },
    };

    [Theory]
    [MemberData(nameof(FirstRunCases))]
    public void Project_FirstRun_MatchesD5(string caseId, string runDate, decimal posted, decimal current, decimal pastDue)
    {
        var position = At();
        var end = Date(runDate);

        var result = InterestAccrual.Project(position, end, Principal, Rate, Schedule, Calculator);

        result.Posting.Changed.Should().Be(end > position.AccruedThroughDate, "{0} defines whether the date advanced", caseId);
        result.Position.AccruedThroughDate.Should().Be(end);
        result.Posting.AccruedThroughDate.Should().Be(end);
        AssertBalances(result, posted, current, pastDue);
    }

    [Theory]
    [MemberData(nameof(SubsequentRunCases))]
    public void Project_FromPostedPosition_MatchesD5(
        string caseId, string postedDate, string segmentStart, string runDate,
        decimal priorCurrent, decimal principal, decimal posted, decimal current, decimal pastDue)
    {
        var position = At(postedDate, segmentStart, priorCurrent);

        var result = InterestAccrual.Project(position, Date(runDate), Euro(principal), Rate, Schedule, Calculator);

        result.Posting.Changed.Should().BeTrue("{0} advances accrual", caseId);
        result.Position.AccruedThroughDate.Should().Be(Date(runDate));
        AssertBalances(result, posted, current, pastDue);
    }

    [Theory]
    [MemberData(nameof(MidpointCases))]
    public void Project_ExactMidpoint_RoundsEachSegmentToEven(
        string caseId, string runDate, decimal principal, decimal percent, bool act365, decimal expected)
    {
        DayCountConvention dayCount = act365 ? new Act365Fixed() : new Act360();
        var rate = FixedRate.Create(Percentage.Create(percent).Value, dayCount).Value;
        var amount = Euro(principal);
        var position = At("2024-01-01", "2024-01-01");
        var end = Date(runDate);
        var schedule = MakeSchedule(amount, rate, position.SegmentStart, end, LoanTerm.FromMonths(2));

        var result = InterestAccrual.Project(position, end, amount, rate, schedule, Calculator);

        result.Posting.Accrued.Should().Be(Euro(expected), "{0} specifies the rounded segment interest", caseId);
        result.Position.CurrentInterest.Should().Be(Euro(expected));
        result.Position.PastDueInterest.Should().Be(Euro(0m));
    }

    [Fact]
    public void Project_PriorDate_PreservesThePostedPosition()
    {
        // IA-09, replay after the first due date has passed.
        var position = At("2026-02-16", "2026-02-15", current: 1.39m, pastDue: 41.67m);

        var result = InterestAccrual.Project(position, Date("2026-02-15"), Principal, Rate, Schedule, Calculator);

        result.Position.Should().BeSameAs(position);
        result.Posting.Changed.Should().BeFalse();
        result.Posting.AccruedThroughDate.Should().Be(position.AccruedThroughDate);
        result.Posting.Accrued.Should().Be(Euro(0m));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(31)]
    public void Project_DifferentPostingIntervals_ProduceTheScheduleTotal(int intervalDays)
    {
        // D5 rules 3 and 6. IA-03/IA-06 cover the first two periods of the M1 schedule.
        var position = At();
        var end = Schedule.Rows[1].PaymentDate;
        var oneRun = InterestAccrual.Project(position, end, Principal, Rate, Schedule, Calculator);
        var total = Euro(0m);

        while (position.AccruedThroughDate < end)
        {
            var nextDate = position.AccruedThroughDate.PlusDays(intervalDays);
            var result = InterestAccrual.Project(
                position, nextDate < end ? nextDate : end, Principal, Rate, Schedule, Calculator);
            total += result.Posting.Accrued;
            position = result.Position;
        }

        total.Should().Be(Euro(83.34m)).And.Be(oneRun.Posting.Accrued);
        position.Should().Be(oneRun.Position);
        position.PastDueInterest.Should().Be(Schedule.Rows[0].InterestDue);
        position.CurrentInterest.Should().Be(Schedule.Rows[1].InterestDue);
        position.SegmentStart.Should().Be(end);
    }

    private static void AssertBalances(InterestAccrual.Projection result, decimal posted, decimal current, decimal pastDue)
    {
        result.Posting.Accrued.Should().Be(Euro(posted));
        result.Position.CurrentInterest.Should().Be(Euro(current));
        result.Position.PastDueInterest.Should().Be(Euro(pastDue));
    }
}
