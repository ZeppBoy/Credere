using Credere.Core.Results;
using Credere.Modules.Servicing.Application.Commands.AccrueInterest;
using Credere.Modules.Servicing.Application.Commands.Activate;
using Credere.Modules.Servicing.Application.Commands.ExecutePayoff;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain.Events;
using FluentAssertions;
using Xunit;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;
using static Credere.Modules.Servicing.Tests.Domain.Loans.LoanSamples;

namespace Credere.Modules.Servicing.Tests.Domain.Loans;

public sealed class LoanTerminalTests
{
    // PO-04 supplies the posted balance components. The owner approved their retention on #71.
    [Fact]
    public void WriteOff_AdvancingDate_PostsAccrualAndRetainsAuditableBalances()
    {
        var loan = CreateLoan(LoanState.Defaulted);
        loan.MarkEventsCommitted();
        var command = WriteOffAt(FirstDueDate.PlusDays(1));

        loan.WriteOff(command, Calculator).IsSuccess.Should().BeTrue();

        loan.State.Should().Be(LoanState.WrittenOff);
        loan.AccruedThroughDate.Should().Be(command.BusinessDate);
        loan.RemainingPrincipal.Should().Be(Principal);
        loan.CurrentInterest.Should().Be(Euro(1.39m));
        loan.PastDueInterest.Should().Be(Euro(41.67m));
        loan.UncommittedEvents.Select(item => item.GetType()).Should().Equal(typeof(InterestAccrued), typeof(LoanWrittenOff));
        var posting = loan.UncommittedEvents.OfType<InterestAccrued>().Single();
        posting.Amount.Should().Be(Euro(43.06m));
        var writeOff = loan.UncommittedEvents.OfType<LoanWrittenOff>().Single();
        writeOff.Context.Should().Be(posting.Context);
        writeOff.Reason.Should().Be(command.Reason);
        writeOff.RemainingPrincipal.Should().Be(Principal);
        writeOff.PastDueInterest.Should().Be(Euro(41.67m));
        writeOff.CurrentInterest.Should().Be(Euro(1.39m));
        writeOff.FeesOutstanding.Should().Be(Euro(0m));
        writeOff.WrittenOffBalance.Should().Be(Euro(10043.06m));
    }

    [Fact]
    public void WriteOff_OnPostedDate_EmitsOnlyTheWriteOff()
    {
        var loan = CreateLoan(LoanState.Defaulted);
        _ = loan.AccrueInterest(new AccrueInterest(Guid.NewGuid(), Id, FirstDueDate, CorrelationId), Calculator).Value;
        loan.MarkEventsCommitted();

        loan.WriteOff(WriteOffAt(FirstDueDate), Calculator).IsSuccess.Should().BeTrue();

        // PO-03 provides the balance even when the posting belongs to an earlier command.
        var writeOff = loan.UncommittedEvents.Should().ContainSingle().Which.Should().BeOfType<LoanWrittenOff>().Which;
        writeOff.RemainingPrincipal.Should().Be(Principal);
        writeOff.PastDueInterest.Should().Be(Euro(0m));
        writeOff.CurrentInterest.Should().Be(Euro(41.67m));
        writeOff.FeesOutstanding.Should().Be(Euro(0m));
        writeOff.WrittenOffBalance.Should().Be(Euro(10041.67m));
    }

    [Fact]
    public void WriteOff_InvalidReasonOrBackdate_PreservesStateAndPostings()
    {
        var loan = CreateLoan(LoanState.Defaulted);
        _ = loan.AccrueInterest(new AccrueInterest(Guid.NewGuid(), Id, FirstDueDate, CorrelationId), Calculator).Value;
        loan.MarkEventsCommitted();

        loan.WriteOff(WriteOffAt(FirstDueDate.PlusDays(1)) with { Reason = " " }, Calculator)
            .Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("reason");
        loan.WriteOff(WriteOffAt(FirstDueDate.PlusDays(-1)), Calculator)
            .Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("businessDate");
        loan.State.Should().Be(LoanState.Defaulted);
        loan.AccruedThroughDate.Should().Be(FirstDueDate);
        loan.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void Restructure_DefaultedLoan_RecordsThePlan()
    {
        var loan = CreateLoan(LoanState.Defaulted);
        loan.MarkEventsCommitted();
        var command = RestructureAt(FirstDueDate);

        loan.Restructure(command).IsSuccess.Should().BeTrue();

        loan.State.Should().Be(LoanState.Restructured);
        loan.UncommittedEvents.Should().ContainSingle().Which.Should().BeOfType<LoanRestructured>()
            .Which.Plan.Should().Be(command.Plan);
    }

    [Fact]
    public void Recover_DefaultedLoanWithZeroRecovery_RecordsTheSettlement()
    {
        var loan = CreateLoan(LoanState.Defaulted);
        loan.MarkEventsCommitted();
        var command = RecoverAt(FirstDueDate);

        loan.Recover(command).IsSuccess.Should().BeTrue();

        loan.State.Should().Be(LoanState.Recovered);
        var recovered = loan.UncommittedEvents.Should().ContainSingle().Which.Should().BeOfType<LoanRecovered>().Which;
        recovered.Method.Should().Be(command.Method);
        recovered.RecoveredAmount.Should().Be(Euro(0m));
    }

    [Fact]
    public void RestructureAndRecover_InvalidDetails_PreserveDefaultedState()
    {
        var loan = CreateLoan(LoanState.Defaulted);
        loan.MarkEventsCommitted();
        var recovery = RecoverAt(FirstDueDate);

        loan.Restructure(RestructureAt(FirstDueDate) with { Plan = "" })
            .Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("plan");
        loan.Recover(recovery with { Method = " " })
            .Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("method");
        loan.Recover(recovery with { RecoveredAmount = Euro(-1m) })
            .Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("recoveredAmount");

        loan.State.Should().Be(LoanState.Defaulted);
        loan.UncommittedEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(LoanState.Disbursed)]
    [InlineData(LoanState.PaidOff)]
    [InlineData(LoanState.WrittenOff)]
    [InlineData(LoanState.Restructured)]
    [InlineData(LoanState.Recovered)]
    public void OtherMutations_BeforeActivationOrAfterClosure_ReturnInvalidStateTransition(LoanState state)
    {
        var loan = CreateLoan(state);
        loan.MarkEventsCommitted();

        loan.ApplyAging(Age(1)).Error.Should().BeOfType<InvalidStateTransition>();
        loan.AccrueInterest(new AccrueInterest(Guid.NewGuid(), Id, FirstDueDate, CorrelationId), Calculator)
            .Error.Should().BeOfType<InvalidStateTransition>();
        loan.RecordPayment(Pay(Principal, FirstDueDate), SamplePaymentId, Calculator)
            .Error.Should().BeOfType<InvalidStateTransition>();
        loan.ExecutePayoff(new ExecutePayoff(Guid.NewGuid(), Id, FirstDueDate, Principal, CorrelationId), SamplePaymentId, Calculator)
            .Error.Should().BeOfType<InvalidStateTransition>();
        loan.State.Should().Be(state);
        loan.UncommittedEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(LoanState.Disbursed)]
    [InlineData(LoanState.Active)]
    [InlineData(LoanState.InGrace)]
    [InlineData(LoanState.PastDue)]
    [InlineData(LoanState.PaidOff)]
    [InlineData(LoanState.WrittenOff)]
    [InlineData(LoanState.Restructured)]
    [InlineData(LoanState.Recovered)]
    public void TerminalCommands_OutsideDefaultedState_ReturnInvalidStateTransition(LoanState state)
    {
        var loan = CreateLoan(state);
        loan.MarkEventsCommitted();

        loan.WriteOff(WriteOffAt(FirstDueDate), Calculator).Error.Should().BeOfType<InvalidStateTransition>();
        loan.Restructure(RestructureAt(FirstDueDate)).Error.Should().BeOfType<InvalidStateTransition>();
        loan.Recover(RecoverAt(FirstDueDate)).Error.Should().BeOfType<InvalidStateTransition>();
        loan.State.Should().Be(state);
        loan.UncommittedEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(LoanState.Active)]
    [InlineData(LoanState.InGrace)]
    [InlineData(LoanState.PastDue)]
    [InlineData(LoanState.Defaulted)]
    [InlineData(LoanState.PaidOff)]
    [InlineData(LoanState.WrittenOff)]
    [InlineData(LoanState.Restructured)]
    [InlineData(LoanState.Recovered)]
    public void Activate_AfterActivation_ReturnsInvalidStateTransition(LoanState state)
    {
        var loan = CreateLoan(state);
        loan.MarkEventsCommitted();

        loan.Activate(new Activate(Guid.NewGuid(), Id, ActivatedOn, CorrelationId), new Calendar())
            .Error.Should().BeOfType<InvalidStateTransition>();

        loan.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void Payments_DefaultedLoan_AreInvalid()
    {
        var loan = CreateLoan(LoanState.Defaulted);
        loan.MarkEventsCommitted();

        loan.RecordPayment(Pay(Principal, FirstDueDate), SamplePaymentId, Calculator)
            .Error.Should().BeOfType<InvalidStateTransition>();
        loan.ExecutePayoff(new ExecutePayoff(Guid.NewGuid(), Id, FirstDueDate, Principal, CorrelationId), SamplePaymentId, Calculator)
            .Error.Should().BeOfType<InvalidStateTransition>();
        loan.State.Should().Be(LoanState.Defaulted);
        loan.UncommittedEvents.Should().BeEmpty();
    }
}
