using Credere.Core.Results;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Application.Commands.AccrueInterest;
using Credere.Modules.Servicing.Application.Commands.ExecutePayoff;
using Credere.Modules.Servicing.Application.Queries.QuotePayoff;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain.Events;
using FluentAssertions;
using Xunit;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;
using static Credere.Modules.Servicing.Tests.Domain.Loans.LoanSamples;

namespace Credere.Modules.Servicing.Tests.Domain.Loans;

public sealed class LoanPaymentTests
{
    // IA-03 and IA-05, InterestAccrual/reference-vectors.md.
    [Theory]
    [InlineData(LoanState.Active)]
    [InlineData(LoanState.InGrace)]
    [InlineData(LoanState.PastDue)]
    [InlineData(LoanState.Defaulted)]
    public void AccrueInterest_EligibleState_PostsAndTreatsRepeatedOrEarlierDatesAsNoOps(LoanState state)
    {
        var loan = CreateLoan(state);
        loan.MarkEventsCommitted();
        var dueDate = FirstDueDate;
        var dayAfterDue = dueDate.PlusDays(1);
        var first = new AccrueInterest(Guid.NewGuid(), Id, dueDate, CorrelationId);
        var second = first with { CommandId = Guid.NewGuid(), BusinessDate = dayAfterDue };

        loan.AccrueInterest(first, Calculator).Value.Should().Be(new AccrualResult(dueDate, Euro(41.67m), true));
        loan.AccrueInterest(second, Calculator).Value.Should().Be(new AccrualResult(dayAfterDue, Euro(1.39m), true));
        loan.AccrueInterest(second with { CommandId = Guid.NewGuid() }, Calculator).Value
            .Should().Be(new AccrualResult(dayAfterDue, Euro(0m), false));
        loan.AccrueInterest(first with { CommandId = Guid.NewGuid() }, Calculator).Value
            .Should().Be(new AccrualResult(dayAfterDue, Euro(0m), false));

        var postings = loan.UncommittedEvents.Cast<InterestAccrued>().ToArray();
        postings.Should().HaveCount(2);
        postings[0].PeriodStart.Should().Be(DisbursedOn);
        postings[1].PeriodStart.Should().Be(dueDate);
        postings[1].Context.CommandId.Should().Be(second.CommandId);
        loan.State.Should().Be(state);
        loan.CurrentInterest.Should().Be(Euro(1.39m));
        loan.PastDueInterest.Should().Be(Euro(41.67m));
    }

    // IA-08 and IA-07, InterestAccrual/reference-vectors.md.
    [Theory]
    [InlineData(LoanState.Active)]
    [InlineData(LoanState.InGrace)]
    [InlineData(LoanState.PastDue)]
    public void RecordPayment_PartialPrincipalPayment_AccruesFirstAndStartsNewSegment(LoanState state)
    {
        var loan = CreateLoan(state);
        loan.MarkEventsCommitted();
        var paymentDate = Date("2026-01-17");
        var command = Pay(Euro(5002.78m), paymentDate);

        var payment = loan.RecordPayment(command, SamplePaymentId, Calculator).Value;

        payment.PaymentId.Should().Be(SamplePaymentId);
        payment.State.Should().Be(state);
        payment.Allocation.CurrentInterestAllocated.Should().Be(Euro(2.78m));
        payment.Allocation.PrincipalAllocated.Should().Be(Euro(5000m));
        payment.Allocation.Excess.Should().Be(Euro(0m));
        loan.RemainingPrincipal.Should().Be(Euro(5000m));
        loan.CurrentInterest.Should().Be(Euro(0m));
        loan.AccrualSegmentStart.Should().Be(paymentDate);
        loan.UncommittedEvents.Select(item => item.GetType())
            .Should().Equal(typeof(InterestAccrued), typeof(PaymentReceived));
        loan.UncommittedEvents.Should().OnlyContain(item => item.Context.CommandId == command.CommandId);

        var next = new AccrueInterest(Guid.NewGuid(), Id, Date("2026-01-20"), CorrelationId);
        loan.AccrueInterest(next, Calculator).Value.Accrued.Should().Be(Euro(2.08m));
    }

    [Fact]
    public void RecordPayment_InterestOnlyOnPostedDate_DoesNotPostAgainOrResetSegment()
    {
        var loan = CreateLoan(LoanState.Active);
        var date = Date("2026-01-17");
        var accrual = new AccrueInterest(Guid.NewGuid(), Id, date, CorrelationId);
        _ = loan.AccrueInterest(accrual, Calculator).Value;
        loan.MarkEventsCommitted();

        var payment = loan.RecordPayment(Pay(Euro(2.78m), date), SamplePaymentId, Calculator).Value;

        payment.Allocation.PrincipalAllocated.Should().Be(Euro(0m));
        loan.UncommittedEvents.Should().ContainSingle().Which.Should().BeOfType<PaymentReceived>();
        loan.AccrualSegmentStart.Should().Be(DisbursedOn);
        // IA-07, the difference of two rounded amounts over the same segment.
        loan.AccrueInterest(accrual with { BusinessDate = Date("2026-01-20") }, Calculator)
            .Value.Accrued.Should().Be(Euro(4.16m));
    }

    // PO-06 and PA-09. A cent over the quoted total is returned as excess.
    [Theory]
    [InlineData(LoanState.Active, false)]
    [InlineData(LoanState.InGrace, true)]
    [InlineData(LoanState.PastDue, false)]
    public void RecordPayment_ClosingPayment_RecordsPayoffAndAnyExcess(LoanState state, bool overpay)
    {
        var loan = CreateLoan(state);
        loan.MarkEventsCommitted();
        var date = Date("2026-01-20");
        var excess = Euro(overpay ? 0.01m : 0m);
        var command = Pay(Euro(10006.94m) + excess, date);

        var result = loan.RecordPayment(command, SamplePaymentId, Calculator).Value;

        loan.State.Should().Be(LoanState.PaidOff);
        result.State.Should().Be(LoanState.PaidOff);
        loan.RemainingPrincipal.Should().Be(Euro(0m));
        loan.CurrentInterest.Should().Be(Euro(0m));
        loan.PastDueInterest.Should().Be(Euro(0m));
        result.Allocation.Excess.Should().Be(excess);
        var expected = overpay
            ? new[] { typeof(InterestAccrued), typeof(PaymentReceived), typeof(PaymentExcessRecorded), typeof(LoanPaidOff) }
            : new[] { typeof(InterestAccrued), typeof(PaymentReceived), typeof(LoanPaidOff) };
        loan.UncommittedEvents.Select(item => item.GetType()).Should().Equal(expected);
        loan.UncommittedEvents.OfType<LoanPaidOff>().Single().FinalPaymentId.Should().Be(SamplePaymentId);
        if (overpay)
        {
            loan.UncommittedEvents.OfType<PaymentExcessRecorded>().Single().Excess.Should().Be(excess);
        }
    }

    [Theory]
    [InlineData("EUR", 0)]
    [InlineData("EUR", -1)]
    [InlineData("USD", 1)]
    public void RecordPayment_InvalidAmount_DoesNotPostAccrual(string currency, int amount)
    {
        var loan = CreateLoan(LoanState.Active);
        loan.MarkEventsCommitted();
        var command = Pay(Money.Create(amount, currency).Value, Date("2026-01-20"));

        loan.RecordPayment(command, SamplePaymentId, Calculator).Error.Should().BeOfType<ValidationError>()
            .Which.Field.Should().Be("amount");

        loan.AccruedThroughDate.Should().Be(DisbursedOn);
        loan.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void RecordPayment_Backdated_ReturnsValidationError()
    {
        var loan = CreateLoan(LoanState.Active);
        _ = loan.AccrueInterest(new AccrueInterest(Guid.NewGuid(), Id, FirstDueDate, CorrelationId), Calculator).Value;
        loan.MarkEventsCommitted();

        loan.RecordPayment(Pay(Principal, FirstDueDate.PlusDays(-1)), SamplePaymentId, Calculator)
            .Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("effectiveDate");
        loan.AccruedThroughDate.Should().Be(FirstDueDate);
        loan.UncommittedEvents.Should().BeEmpty();
    }

    // PO-06, PO-08, PO-10 and PO-11, PayoffCalculation/reference-vectors.md.
    [Fact]
    public void ExecutePayoff_InterveningPayment_RequiresFreshQuoteAndPostsOnlyOnSuccess()
    {
        var loan = CreateLoan(LoanState.Active);
        loan.MarkEventsCommitted();
        var date = Date("2026-01-20");
        var query = new QuotePayoff(Id, date);
        var original = loan.QuotePayoff(query, Calculator).Value;
        original.Total.Should().Be(Euro(10006.94m));
        loan.UncommittedEvents.Should().BeEmpty();

        var partialDate = Date("2026-01-17");
        _ = loan.RecordPayment(Pay(Euro(5002.78m), partialDate), new PaymentId(Guid.NewGuid()), Calculator).Value;
        loan.MarkEventsCommitted();

        var command = new ExecutePayoff(Guid.NewGuid(), Id, date, original.Total, CorrelationId);
        loan.ExecutePayoff(command, SamplePaymentId, Calculator).Error.Should()
            .Be(new PayoffQuoteStale(Euro(10006.94m), Euro(5002.08m)));
        loan.UncommittedEvents.Should().BeEmpty();
        loan.AccruedThroughDate.Should().Be(partialDate);
        loan.RemainingPrincipal.Should().Be(Euro(5000m));

        var updated = loan.QuotePayoff(query, Calculator).Value;
        updated.Total.Should().Be(Euro(5002.08m));
        var payoff = loan.ExecutePayoff(command with { QuotedTotal = updated.Total }, SamplePaymentId, Calculator).Value;

        payoff.PaymentId.Should().Be(SamplePaymentId);
        payoff.Allocation.Excess.Should().Be(Euro(0m));
        loan.State.Should().Be(LoanState.PaidOff);
        loan.AccruedThroughDate.Should().Be(date);
        loan.UncommittedEvents.Select(item => item.GetType())
            .Should().Equal(typeof(InterestAccrued), typeof(PaymentReceived), typeof(LoanPaidOff));
    }

    [Fact]
    public void QuoteAndExecutePayoff_EarlierThanPostedDate_ReturnValidationError()
    {
        var loan = CreateLoan(LoanState.Active);
        _ = loan.AccrueInterest(new AccrueInterest(Guid.NewGuid(), Id, FirstDueDate, CorrelationId), Calculator).Value;
        loan.MarkEventsCommitted();
        var priorDate = FirstDueDate.PlusDays(-1);

        loan.QuotePayoff(new QuotePayoff(Id, priorDate), Calculator).Error
            .Should().BeOfType<ValidationError>().Which.Field.Should().Be("payoffDate");
        loan.ExecutePayoff(new ExecutePayoff(Guid.NewGuid(), Id, priorDate, Principal, CorrelationId), SamplePaymentId, Calculator)
            .Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("payoffDate");
        loan.UncommittedEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(LoanState.Disbursed)]
    [InlineData(LoanState.Defaulted)]
    [InlineData(LoanState.PaidOff)]
    [InlineData(LoanState.WrittenOff)]
    [InlineData(LoanState.Restructured)]
    [InlineData(LoanState.Recovered)]
    public void QuotePayoff_IneligibleState_ReturnsInvalidStateTransition(LoanState state)
    {
        var loan = CreateLoan(state);
        loan.MarkEventsCommitted();

        loan.QuotePayoff(new QuotePayoff(Id, loan.AccruedThroughDate), Calculator)
            .Error.Should().BeOfType<InvalidStateTransition>();

        loan.UncommittedEvents.Should().BeEmpty();
    }
}
