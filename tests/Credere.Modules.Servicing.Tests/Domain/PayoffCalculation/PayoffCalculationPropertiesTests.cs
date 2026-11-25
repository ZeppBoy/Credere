using Credere.Modules.Servicing.Domain;
using FsCheck;
using FsCheck.Xunit;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;

namespace Credere.Modules.Servicing.Tests.Domain;

public sealed class PayoffCalculationPropertiesTests
{
    // D5, docs/modules/servicing.md, "Payoff quotes". A quote includes unposted interest through its date.
    [Property(MaxTest = 300)]
    public bool Quote_BeforeAndAfterPostingForItsDate_ReturnsTheSameBalances(
        NonNegativeInt period, NonNegativeInt daysBeforeDueDate)
    {
        var dueDate = Schedule.Rows[period.Get % Schedule.Rows.Count].PaymentDate;
        var payoffDate = dueDate.PlusDays(-(daysBeforeDueDate.Get % 28));
        var fees = Euro(0m);
        var initial = At();

        var beforePosting = PayoffCalculation.Quote(payoffDate, Principal, fees, initial, Rate, Schedule, Calculator).Value;
        var posted = InterestAccrual.Project(initial, payoffDate, Principal, Rate, Schedule, Calculator).Position;
        var afterPosting = PayoffCalculation.Quote(payoffDate, Principal, fees, posted, Rate, Schedule, Calculator).Value;

        return beforePosting == afterPosting
            && afterPosting.Principal == Principal
            && afterPosting.CurrentInterest == posted.CurrentInterest
            && afterPosting.PastDueInterest == posted.PastDueInterest
            && afterPosting.Total == Principal + fees + posted.CurrentInterest + posted.PastDueInterest
            && PayoffCalculation.ValidateQuotedTotal(afterPosting.Total, beforePosting.Total).IsSuccess;
    }
}
