using System.Reflection;
using Credere.Modules.Servicing.Application.Commands.AccrueInterest;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain;
using Credere.Modules.Servicing.Domain.Events;
using FluentAssertions;
using Xunit;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;
using static Credere.Modules.Servicing.Tests.Domain.Loans.LoanSamples;

namespace Credere.Modules.Servicing.Tests.Domain.Loans;

public sealed class LoanReplayTests
{
    [Theory]
    [InlineData(LoanState.Disbursed)]
    [InlineData(LoanState.Active)]
    [InlineData(LoanState.InGrace)]
    [InlineData(LoanState.PastDue)]
    [InlineData(LoanState.Defaulted)]
    [InlineData(LoanState.PaidOff)]
    [InlineData(LoanState.WrittenOff)]
    [InlineData(LoanState.Restructured)]
    [InlineData(LoanState.Recovered)]
    public void Rehydrate_RecordedLifecycle_RestoresStateWithoutUncommittedEvents(LoanState state)
    {
        var original = CreateLoan(state);

        var restored = Loan.Rehydrate(original.UncommittedEvents);

        restored.State.Should().Be(state);
        restored.DaysPastDue.Should().Be(original.DaysPastDue);
        restored.Version.Should().Be(original.Version);
        restored.Id.Should().Be(original.Id);
        restored.DisbursementIntentId.Should().Be(original.DisbursementIntentId);
        restored.ProductTerms.Should().Be(original.ProductTerms);
        restored.Schedule.Should().Be(original.Schedule);
        restored.AccruedThroughDate.Should().Be(original.AccruedThroughDate);
        restored.AccrualSegmentStart.Should().Be(original.AccrualSegmentStart);
        restored.RemainingPrincipal.Should().Be(original.RemainingPrincipal);
        restored.CurrentInterest.Should().Be(original.CurrentInterest);
        restored.PastDueInterest.Should().Be(original.PastDueInterest);
        restored.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void Rehydrate_PartialPayment_RestoresTheOpenSegmentForFutureAccrual()
    {
        // IA-08 supplies the partial payment and subsequent accrual.
        var original = CreateLoan(LoanState.Active);
        var paymentDate = Date("2026-01-17");
        _ = original.RecordPayment(Pay(Euro(5002.78m), paymentDate), SamplePaymentId, Calculator).Value;

        var restored = Loan.Rehydrate(original.UncommittedEvents);

        restored.AccrualSegmentStart.Should().Be(paymentDate);
        restored.RemainingPrincipal.Should().Be(Euro(5000m));
        var accrual = new AccrueInterest(Guid.NewGuid(), Id, Date("2026-01-20"), CorrelationId);
        restored.AccrueInterest(accrual, Calculator).Value.Accrued.Should().Be(Euro(2.08m));
        restored.Version.Should().Be(original.Version + 1);
        restored.UncommittedEvents.Should().ContainSingle().Which.Should().BeOfType<InterestAccrued>();
    }

    [Fact]
    public void Rehydrate_PaymentWithExcess_RequiresTheCompleteClosingSequence()
    {
        // PA-09 and PO-06 supply the excess and the quote amount.
        var original = CreateLoan(LoanState.Active);
        _ = original.RecordPayment(Pay(Euro(10006.95m), Date("2026-01-20")), SamplePaymentId, Calculator).Value;
        var history = original.UncommittedEvents.ToArray();

        Loan.Rehydrate(history).State.Should().Be(LoanState.PaidOff);
        var missingExcess = () => Loan.Rehydrate(history.Where(item => item is not PaymentExcessRecorded));
        missingExcess.Should().Throw<InvalidOperationException>();
        var missingPayoff = () => Loan.Rehydrate(history[..^1]);
        missingPayoff.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rehydrate_InvalidHistory_Throws()
    {
        var original = CreateLoan(LoanState.Active);
        var history = original.UncommittedEvents.ToArray();
        var empty = () => Loan.Rehydrate([]);
        empty.Should().Throw<InvalidOperationException>();
        var missingDisbursement = () => Loan.Rehydrate(history[1..]);
        missingDisbursement.Should().Throw<InvalidOperationException>();
        var repeatActivation = () => Loan.Rehydrate([.. history, history[^1]]);
        repeatActivation.Should().Throw<InvalidOperationException>();
        var unknownEvent = () => Loan.Rehydrate([.. history, new UnknownEvent(history[^1].Context)]);
        unknownEvent.Should().Throw<InvalidOperationException>();
        var anotherStream = () => Loan.Rehydrate(
            [history[0], new LoanActivated(history[^1].Context with { LoanId = new LoanId(Guid.NewGuid()) })]);
        anotherStream.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rehydrate_PaymentWithDifferentAllocation_Throws()
    {
        var loan = CreateLoan(LoanState.Active);
        _ = loan.RecordPayment(Pay(Euro(5002.78m), Date("2026-01-17")), SamplePaymentId, Calculator).Value; // IA-08.
        var history = loan.UncommittedEvents.ToArray();
        var payment = (PaymentReceived)history[^1];
        history[^1] = payment with { Allocation = payment.Allocation with { NewRemainingPrincipal = Principal } };

        var replay = () => Loan.Rehydrate(history);

        replay.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rehydrate_AccrualWithInvalidPeriodOrSegment_Throws(bool invalidPeriod)
    {
        var loan = CreateLoan(LoanState.Active);
        _ = loan.AccrueInterest(new AccrueInterest(Guid.NewGuid(), Id, FirstDueDate, CorrelationId), Calculator).Value;
        var history = loan.UncommittedEvents.ToArray();
        var accrual = (InterestAccrued)history[^1];
        history[^1] = invalidPeriod
            ? accrual with { PeriodStart = ActivatedOn }
            : accrual with { SegmentStart = FirstDueDate.PlusDays(1) };

        var replay = () => Loan.Rehydrate(history);

        replay.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rehydrate_AgingFromDifferentState_Throws()
    {
        var loan = CreateLoan(LoanState.PastDue);
        var history = loan.UncommittedEvents.ToArray();
        var aging = (LoanBecamePastDue)history[^1];
        history[^1] = aging with { PreviousState = LoanState.InGrace };

        var replay = () => Loan.Rehydrate(history);

        replay.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rehydrate_WriteOffWithDifferentTotalOrBalanceComponent_Throws(bool invalidTotal)
    {
        var loan = CreateLoan(LoanState.WrittenOff);
        var history = loan.UncommittedEvents.ToArray();
        var writeOff = (LoanWrittenOff)history[^1];
        history[^1] = invalidTotal
            ? writeOff with { WrittenOffBalance = Principal }
            : writeOff with { CurrentInterest = Euro(0m) };

        var replay = () => Loan.Rehydrate(history);

        replay.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rehydrate_ExcessWithDifferentAmountOrContext_Throws(bool invalidAmount)
    {
        var loan = CreateLoan(LoanState.Active);
        _ = loan.RecordPayment(Pay(Euro(10006.95m), Date("2026-01-20")), SamplePaymentId, Calculator).Value; // PA-09 and PO-06.
        var history = loan.UncommittedEvents.ToArray();
        var excess = (PaymentExcessRecorded)history[^2];
        history[^2] = invalidAmount
            ? excess with { Excess = Euro(0m) }
            : excess with { Context = excess.Context with { CommandId = Guid.NewGuid() } };

        var replay = () => Loan.Rehydrate(history);

        replay.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkEventsCommitted_KeepsTheAppliedVersion()
    {
        var loan = CreateLoan(LoanState.Active);
        var version = loan.Version;

        loan.MarkEventsCommitted();

        loan.Version.Should().Be(version);
        loan.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_AllConcreteTypes_DeclareInitialSchemaVersion()
    {
        var events = typeof(LoanEvent).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(LoanEvent)) && !type.IsAbstract).ToArray();

        events.Should().NotBeEmpty();
        events.Select(type => type.GetCustomAttribute<EventVersionAttribute>(false))
            .Should().OnlyContain(version => version != null && version.Version == 1);
    }

    private sealed record UnknownEvent(LoanEventContext Context) : LoanEvent(Context);
}
