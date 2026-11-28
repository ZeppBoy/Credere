using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Application.Commands.AccrueInterest;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain;
using Credere.Modules.Servicing.Domain.Events;
using FluentAssertions;
using NodaTime;
using Xunit;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;
using static Credere.Modules.Servicing.Tests.Domain.Loans.LoanSamples;

namespace Credere.Modules.Servicing.Tests.Domain.Loans;

public sealed class LoanRestoreTests
{
    [Theory]
    [InlineData(LoanState.Disbursed)]
    [InlineData(LoanState.Active)]
    [InlineData(LoanState.InGrace)]
    [InlineData(LoanState.PastDue)]
    [InlineData(LoanState.Defaulted)]
    [InlineData(LoanState.WrittenOff)]
    [InlineData(LoanState.Restructured)]
    [InlineData(LoanState.Recovered)]
    [InlineData(LoanState.PaidOff)]
    public void Restore_CurrentStateAndSchedule_RestoresAllStateWithoutRaisingEvents(LoanState state)
    {
        var original = CreateLoan(state);

        var restored = Loan.Restore(SnapshotOf(original));

        SnapshotOf(restored).Should().Be(SnapshotOf(original));
        restored.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void Restore_AfterPrincipalPayment_ContinuesAccrualAtTheNextStreamVersion()
    {
        // IA-14, InterestAccrual/reference-vectors.md. A principal payment on February 16 starts a new segment.
        var original = CreateLoan(LoanState.Active);
        var paymentDate = Date("2026-02-16");
        var payment = original.RecordPayment(Pay(Euro(1043.06m), paymentDate), SamplePaymentId, Calculator).Value;
        payment.Allocation.PrincipalAllocated.Should().Be(Euro(1000m));

        var restored = Loan.Restore(SnapshotOf(original));
        var accrualDate = Date("2026-02-20");
        var command = new AccrueInterest(Guid.NewGuid(), Id, accrualDate, CorrelationId);

        restored.AccrueInterest(command, Calculator).Value.Should().Be(new AccrualResult(accrualDate, Euro(5m), true));
        restored.Version.Should().Be(original.Version + 1);
        restored.RemainingPrincipal.Should().Be(Euro(9000m));
        restored.AccrualSegmentStart.Should().Be(paymentDate);
        var posted = restored.UncommittedEvents.Should().ContainSingle().Which;
        var accrual = posted.Should().BeOfType<InterestAccrued>().Which;
        accrual.PeriodStart.Should().Be(paymentDate);
        accrual.Context.CommandId.Should().Be(command.CommandId);
    }

    [Fact]
    public void Restore_ScheduleDoesNotMatchCommittedTerms_Throws()
    {
        var snapshot = SnapshotOf(CreateLoan()) with { FirstPaymentDate = FirstDueDate.PlusDays(1) };

        var act = () => Loan.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>().WithMessage("*schedule*");
    }

    [Fact]
    public void Restore_AccrualSegmentStartsAfterPostedDate_Throws()
    {
        var snapshot = SnapshotOf(CreateLoan(LoanState.Active));
        snapshot = snapshot with { Interest = snapshot.Interest with { SegmentStart = FirstDueDate } };

        var act = () => Loan.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>().WithMessage("*accrual segment*");
    }

    [Fact]
    public void Restore_AgingStateDoesNotMatchTheDaysPastDue_Throws()
    {
        var snapshot = SnapshotOf(CreateLoan(LoanState.PastDue)) with { DaysPastDue = 0 };

        var act = () => Loan.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>().WithMessage("*days past due*");
    }

    [Fact]
    public void Restore_ManyAgingTransitions_PreservesTheActiveStreamVersion()
    {
        var original = CreateLoan(LoanState.Active);
        for (var cycle = 0; cycle < 249; cycle++)
        {
            original.ApplyAging(Age(1)).IsSuccess.Should().BeTrue();
            original.ApplyAging(Age(0)).IsSuccess.Should().BeTrue();
        }

        var restored = Loan.Restore(SnapshotOf(original));

        restored.State.Should().Be(LoanState.Active);
        restored.Version.Should().Be(500);
        restored.UncommittedEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(LoanState.Disbursed, 0)]
    [InlineData(LoanState.Disbursed, 7)]
    [InlineData(LoanState.Active, 1)]
    [InlineData(LoanState.PastDue, 2)]
    [InlineData(LoanState.WrittenOff, 3)]
    [InlineData(LoanState.PaidOff, 3)]
    public void Restore_VersionCannotProduceTheStoredState_Throws(LoanState state, long version)
    {
        var snapshot = SnapshotOf(CreateLoan(state)) with { Version = version };

        var act = () => Loan.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>().WithMessage("*version*");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Restore_DisbursementWithAnAccruedOrReducedBalance_Throws(bool principalReduced)
    {
        var snapshot = SnapshotOf(CreateLoan());
        var inconsistent = principalReduced
            ? snapshot with { RemainingPrincipal = Euro(0.01m) }
            : snapshot with { Interest = snapshot.Interest with { CurrentInterest = Euro(0.01m) } };

        var act = () => Loan.Restore(inconsistent);

        act.Should().Throw<InvalidOperationException>().WithMessage("*balances*");
    }

    [Fact]
    public void Restore_AccrualPostedBeforeActivation_Throws()
    {
        var snapshot = SnapshotOf(CreateLoan());
        snapshot = snapshot with { Interest = snapshot.Interest with { AccruedThroughDate = ActivatedOn } };

        var act = () => Loan.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>().WithMessage("*accrual*");
    }

    [Theory]
    [InlineData(LoanState.Active)]
    [InlineData(LoanState.PaidOff)]
    public void Restore_PrincipalBalanceContradictsThePayoffState_Throws(LoanState state)
    {
        var principal = state == LoanState.PaidOff ? Euro(0.01m) : Euro(0m);
        var snapshot = SnapshotOf(CreateLoan(state)) with { RemainingPrincipal = principal };

        var act = () => Loan.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>().WithMessage("*balances*");
    }

    [Fact]
    public void Restore_PaidOffWithOutstandingFees_Throws()
    {
        var snapshot = SnapshotOf(CreateLoan(LoanState.PaidOff)) with { FeesOutstanding = Euro(0.01m) };

        var act = () => Loan.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>().WithMessage("*balances*");
    }

    [Theory]
    [InlineData("EUR", -1)]
    [InlineData("USD", 0)]
    public void Restore_InvalidFees_Throws(string currency, int amount)
    {
        var fees = Money.Create(amount, currency).Value;
        var snapshot = SnapshotOf(CreateLoan(LoanState.Active)) with { FeesOutstanding = fees };

        var act = () => Loan.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>().WithMessage("*balances*");
    }

    [Fact]
    public void Restore_RemainingPrincipalExceedsDisbursement_Throws()
    {
        var snapshot = SnapshotOf(CreateLoan(LoanState.Active)) with { RemainingPrincipal = Principal + Euro(0.01m) };

        var act = () => Loan.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>().WithMessage("*balances*");
    }

    [Fact]
    public void Restore_AccrualDatesUseDifferentCalendars_Throws()
    {
        var snapshot = SnapshotOf(CreateLoan(LoanState.Active));
        var julian = DisbursedOn.WithCalendar(CalendarSystem.Julian);
        snapshot = snapshot with { Interest = snapshot.Interest with { SegmentStart = julian } };

        var act = () => Loan.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>().WithMessage("*accrual segment*");
    }

    [Theory]
    [InlineData(LoanState.WrittenOff)]
    [InlineData(LoanState.Restructured)]
    [InlineData(LoanState.Recovered)]
    public void Restore_DefaultedOutcomeWithInsufficientDaysPastDue_Throws(LoanState state)
    {
        var snapshot = SnapshotOf(CreateLoan(state)) with { DaysPastDue = DefaultThreshold - 1 };

        var act = () => Loan.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>().WithMessage("*days past due*");
    }

    [Fact]
    public void Restore_BlankProduct_Throws()
    {
        var snapshot = SnapshotOf(CreateLoan()) with { Product = string.Empty };

        var act = () => Loan.Restore(snapshot);

        act.Should().Throw<InvalidOperationException>().WithMessage("*terms*");
    }

    private static LoanSnapshot SnapshotOf(Loan loan) => new(
        loan.Id,
        loan.DisbursementIntentId,
        loan.SourceReference,
        loan.TermsVersion,
        loan.Product,
        new AcceptedLoanTerms(loan.Principal, loan.Rate.NominalRate, loan.Term),
        loan.ProductTerms,
        loan.DisbursementDate,
        loan.FirstPaymentDate,
        loan.Schedule,
        loan.RemainingPrincipal,
        loan.FeesOutstanding,
        new InterestAccrual.Position(loan.AccruedThroughDate, loan.AccrualSegmentStart, loan.CurrentInterest, loan.PastDueInterest),
        loan.State,
        loan.DaysPastDue,
        loan.Version);
}
