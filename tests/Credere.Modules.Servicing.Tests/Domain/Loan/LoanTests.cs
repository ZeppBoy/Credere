using Credere.Core.Results;
using Credere.Modules.Servicing.Application.Commands.Activate;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain;
using Credere.Modules.Servicing.Domain.Events;
using FluentAssertions;
using Xunit;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;
using static Credere.Modules.Servicing.Tests.Domain.Loans.LoanSamples;

namespace Credere.Modules.Servicing.Tests.Domain.Loans;

public sealed class LoanTests
{
    [Fact]
    public void Disburse_ApprovedPendingIntent_CapturesTermsAndSchedule()
    {
        var intent = Intent();
        var command = DisburseCommand();

        var loan = Loan.Disburse(command, Id, intent, Product, Schedule).Value;

        loan.State.Should().Be(LoanState.Disbursed);
        loan.Id.Should().Be(Id);
        loan.DisbursementIntentId.Should().Be(IntentId);
        loan.SourceReference.Should().Be(SourceReference);
        loan.TermsVersion.Should().Be(intent.AcceptedTermsVersion);
        loan.Product.Should().Be(intent.Product);
        loan.ProductTerms.Should().Be(Product);
        loan.Rate.Should().Be(Rate);
        loan.Frequency.Should().Be(Product.Frequency);
        loan.Repayment.Should().Be(Product.Repayment);
        loan.Term.Should().Be(intent.Terms.Term);
        loan.Schedule.Should().Be(Schedule);
        loan.Principal.Should().Be(Principal);
        loan.RemainingPrincipal.Should().Be(Principal);
        loan.CurrentInterest.Should().Be(Euro(0m));
        loan.PastDueInterest.Should().Be(Euro(0m));
        loan.FeesOutstanding.Should().Be(Euro(0m));
        loan.DisbursementDate.Should().Be(DisbursedOn);
        loan.FirstPaymentDate.Should().Be(FirstDueDate);
        loan.AccruedThroughDate.Should().Be(DisbursedOn);
        loan.AccrualSegmentStart.Should().Be(DisbursedOn);
        loan.DaysPastDue.Should().BeNull();
        loan.Version.Should().Be(1);
        loan.UncommittedEvents.Should().ContainSingle().Which.Should().BeOfType<LoanDisbursed>()
            .Which.Context.Should().Be(new LoanEventContext(Id, DisbursedOn, command.CommandId, command.CorrelationId));
        intent.State.Should().Be(DisbursementIntentState.Pending);
    }

    [Fact]
    public void Disburse_AlreadyUsedIntent_ReturnsInvalidStateTransition()
    {
        var intent = Intent();
        _ = intent.MarkDisbursed(Id).Value;

        var result = Loan.Disburse(DisburseCommand(), Id, intent, Product, Schedule);

        result.Error.Should().BeOfType<InvalidStateTransition>();
        intent.LoanId.Should().Be(Id);
        intent.MarkDisbursed(new LoanId(Guid.NewGuid())).Error.Should().BeOfType<InvalidStateTransition>();
        intent.LoanId.Should().Be(Id);
    }

    [Fact]
    public void Disburse_FirstPaymentOnDisbursementDate_ReturnsValidationError()
    {
        var result = Loan.Disburse(
            DisburseCommand() with { FirstPaymentDate = DisbursedOn }, Id, Intent(), Product, Schedule);

        result.Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("firstPaymentDate");
    }

    [Fact]
    public void Disburse_ScheduleFromDifferentTerms_Throws()
    {
        var attempt = () => Loan.Disburse(
            DisburseCommand() with { FirstPaymentDate = FirstDueDate.PlusDays(1) }, Id, Intent(), Product, Schedule);

        attempt.Should().Throw<ArgumentException>().WithParameterName("schedule");
    }

    [Fact]
    public void Activate_AfterHolidayAndWeekend_UsesFirstBusinessDay()
    {
        var loan = CreateLoan();
        loan.MarkEventsCommitted();
        var calendar = new Calendar(ActivatedOn);
        var monday = Date("2026-01-19");

        loan.Activate(new Activate(Guid.NewGuid(), Id, monday, CorrelationId), calendar).IsSuccess.Should().BeTrue();

        loan.State.Should().Be(LoanState.Active);
        loan.DaysPastDue.Should().BeNull();
        loan.AccruedThroughDate.Should().Be(DisbursedOn);
        loan.UncommittedEvents.Should().ContainSingle().Which.Should().BeOfType<LoanActivated>()
            .Which.Context.EffectiveDate.Should().Be(monday);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void Activate_DateOtherThanFirstBusinessDay_PreservesDisbursedState(int daysAfterDisbursement)
    {
        var loan = CreateLoan();
        loan.MarkEventsCommitted();
        var command = new Activate(Guid.NewGuid(), Id, DisbursedOn.PlusDays(daysAfterDisbursement), CorrelationId);

        loan.Activate(command, new Calendar(ActivatedOn)).Error.Should().BeOfType<ValidationError>()
            .Which.Field.Should().Be("effectiveDate");

        loan.State.Should().Be(LoanState.Disbursed);
        loan.UncommittedEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(LoanState.Active, 0, LoanState.Active, false, null)]
    [InlineData(LoanState.Active, 1, LoanState.InGrace, true, typeof(LoanEnteredGracePeriod))]
    [InlineData(LoanState.Active, PastDueThreshold, LoanState.PastDue, true, typeof(LoanBecamePastDue))]
    [InlineData(LoanState.Active, DefaultThreshold, LoanState.Defaulted, true, typeof(LoanDefaulted))]
    [InlineData(LoanState.InGrace, 0, LoanState.Active, true, typeof(LoanBecameActive))]
    [InlineData(LoanState.InGrace, 1, LoanState.InGrace, false, null)]
    [InlineData(LoanState.InGrace, 2, LoanState.InGrace, true, typeof(LoanPastDueDaysUpdated))]
    [InlineData(LoanState.InGrace, PastDueThreshold, LoanState.PastDue, true, typeof(LoanBecamePastDue))]
    [InlineData(LoanState.InGrace, DefaultThreshold, LoanState.Defaulted, true, typeof(LoanDefaulted))]
    [InlineData(LoanState.PastDue, 0, LoanState.Active, true, typeof(LoanBecameActive))]
    [InlineData(LoanState.PastDue, 1, LoanState.InGrace, true, typeof(LoanEnteredGracePeriod))]
    [InlineData(LoanState.PastDue, PastDueThreshold, LoanState.PastDue, false, null)]
    [InlineData(LoanState.PastDue, DefaultThreshold - 1, LoanState.PastDue, true, typeof(LoanPastDueDaysUpdated))]
    [InlineData(LoanState.PastDue, DefaultThreshold, LoanState.Defaulted, true, typeof(LoanDefaulted))]
    [InlineData(LoanState.Defaulted, DefaultThreshold, LoanState.Defaulted, false, null)]
    [InlineData(LoanState.Defaulted, DefaultThreshold + 1, LoanState.Defaulted, true, typeof(LoanPastDueDaysUpdated))]
    public void ApplyAging_TransitionTable_RecordsOnlyStateOrDaysChanges(
        LoanState before, int days, LoanState after, bool changed, Type? eventType)
    {
        var loan = CreateLoan(before);
        loan.MarkEventsCommitted();

        var result = loan.ApplyAging(Age(days)).Value;

        result.State.Should().Be(after);
        result.DaysPastDue.Should().Be(after == LoanState.Active ? null : days);
        result.Changed.Should().Be(changed);
        loan.State.Should().Be(result.State);
        loan.DaysPastDue.Should().Be(result.DaysPastDue);
        if (eventType is null)
        {
            loan.UncommittedEvents.Should().BeEmpty();
        }
        else
        {
            var aging = loan.UncommittedEvents.Should().ContainSingle().Which.Should().BeAssignableTo<LoanAgingEvent>().Which;
            aging.Should().BeOfType(eventType);
            aging.PreviousState.Should().Be(before);
            aging.NewState.Should().Be(after);
            aging.DaysPastDue.Should().Be(result.DaysPastDue);
        }
    }

    [Fact]
    public void ApplyAging_DefaultedLoanBelowThreshold_CannotCure()
    {
        var loan = CreateLoan(LoanState.Defaulted);
        loan.MarkEventsCommitted();

        loan.ApplyAging(Age(DefaultThreshold - 1)).Error.Should().BeOfType<InvalidStateTransition>();
        loan.State.Should().Be(LoanState.Defaulted);
        loan.DaysPastDue.Should().Be(DefaultThreshold);
        loan.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void ApplyAging_NegativeDays_ReturnsValidationError()
    {
        var loan = CreateLoan(LoanState.Active);
        loan.MarkEventsCommitted();

        loan.ApplyAging(Age(-1)).Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("daysPastDue");
        loan.UncommittedEvents.Should().BeEmpty();
    }
}
