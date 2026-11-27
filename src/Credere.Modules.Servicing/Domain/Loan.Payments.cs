using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Application.Commands.AccrueInterest;
using Credere.Modules.Servicing.Application.Commands.ExecutePayoff;
using Credere.Modules.Servicing.Application.Commands.RecordPayment;
using Credere.Modules.Servicing.Application.Commands.WriteOff;
using Credere.Modules.Servicing.Application.Queries.QuotePayoff;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain.Events;
using NodaTime;

namespace Credere.Modules.Servicing.Domain;

internal sealed partial class Loan
{
    internal Result<AccrualResult, DomainError> AccrueInterest(AccrueInterest command, IInterestCalculator calculator)
    {
        CheckId(command.Id);
        if (!CanAccrue)
        {
            return Invalid<AccrualResult>(nameof(AccrueInterest));
        }

        var projection = Project(command.BusinessDate, calculator);
        PostAccrual(Context(command.BusinessDate, command.CommandId, command.CorrelationId), projection);
        return Result<AccrualResult, DomainError>.Success(
            new AccrualResult(AccruedThroughDate, projection.Accrued, projection.Changed));
    }

    internal Result<PaymentResult, DomainError> RecordPayment(
        RecordPayment command, PaymentId paymentId, IInterestCalculator calculator)
    {
        CheckId(command.Id);
        if (!CanPay)
        {
            return Invalid<PaymentResult>(nameof(RecordPayment));
        }

        if (!IsLoanAmount(command.Amount) || command.Amount.Amount == 0m)
        {
            return Result<PaymentResult, DomainError>.Failure(
                new ValidationError("amount", "must be positive and use the loan currency"));
        }

        if (command.EffectiveDate < AccruedThroughDate)
        {
            return Result<PaymentResult, DomainError>.Failure(
                new ValidationError("effectiveDate", "must be on or after the posted accrual date"));
        }

        var projection = Project(command.EffectiveDate, calculator);
        var context = Context(command.EffectiveDate, command.CommandId, command.CorrelationId);
        return Result<PaymentResult, DomainError>.Success(ReceivePayment(context, paymentId, command.Amount, projection));
    }

    internal Result<PayoffQuote, DomainError> QuotePayoff(QuotePayoff query, IInterestCalculator calculator)
    {
        CheckId(query.Id);
        var amounts = CalculatePayoff(query.PayoffDate, _interest, calculator);
        return amounts.Map(value => new PayoffQuote(
            Id, query.PayoffDate, value.Principal, value.PastDueInterest, value.CurrentInterest, value.Fees, value.Total));
    }

    internal Result<PayoffResult, DomainError> ExecutePayoff(
        ExecutePayoff command, PaymentId paymentId, IInterestCalculator calculator)
    {
        CheckId(command.Id);
        if (!CanPay)
        {
            return Invalid<PayoffResult>(nameof(ExecutePayoff));
        }

        var projection = Project(command.PayoffDate, calculator);
        var amounts = CalculatePayoff(command.PayoffDate, projection.Position, calculator);
        if (!amounts.IsSuccess)
        {
            return Result<PayoffResult, DomainError>.Failure(amounts.Error);
        }

        var total = amounts.Value.Total;
        var match = PayoffCalculation.ValidateQuotedTotal(total, command.QuotedTotal);
        if (!match.IsSuccess)
        {
            return Result<PayoffResult, DomainError>.Failure(match.Error);
        }

        var context = Context(command.PayoffDate, command.CommandId, command.CorrelationId);
        var payment = ReceivePayment(context, paymentId, total, projection);
        return Result<PayoffResult, DomainError>.Success(new PayoffResult(payment.PaymentId, payment.Allocation));
    }

    internal Result<Unit, DomainError> WriteOff(WriteOff command, IInterestCalculator calculator)
    {
        CheckId(command.Id);
        if (State != LoanState.Defaulted)
        {
            return Invalid<Unit>(nameof(WriteOff));
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<Unit, DomainError>.Failure(new ValidationError("reason", "must not be empty"));
        }

        if (command.BusinessDate < AccruedThroughDate)
        {
            return Result<Unit, DomainError>.Failure(
                new ValidationError("businessDate", "must be on or after the posted accrual date"));
        }

        var projection = Project(command.BusinessDate, calculator);
        var balance = OutstandingTotal(projection.Position);
        var context = Context(command.BusinessDate, command.CommandId, command.CorrelationId);
        PostAccrual(context, projection);
        Raise(new LoanWrittenOff(context, command.Reason, balance));
        return Success();
    }

    private InterestAccrual.Projection Project(LocalDate date, IInterestCalculator calculator) =>
        InterestAccrual.Project(_interest, date, RemainingPrincipal, Rate, Schedule, calculator);

    private Result<PayoffCalculation.Amounts, DomainError> CalculatePayoff(
        LocalDate date, InterestAccrual.Position position, IInterestCalculator calculator) =>
        PayoffCalculation.Quote(date, RemainingPrincipal, FeesOutstanding, position, Rate, Schedule, calculator);

    private Money OutstandingTotal(InterestAccrual.Position position)
    {
        var interest = InterestAccrual.AddExactly(position.PastDueInterest, position.CurrentInterest);
        return InterestAccrual.AddExactly(InterestAccrual.AddExactly(RemainingPrincipal, interest), FeesOutstanding);
    }

    private void PostAccrual(LoanEventContext context, InterestAccrual.Projection projection)
    {
        if (projection.Changed)
        {
            var position = projection.Position;
            Raise(new InterestAccrued(
                context, AccruedThroughDate, projection.Accrued,
                position.SegmentStart, position.CurrentInterest, position.PastDueInterest));
        }
    }

    private PaymentResult ReceivePayment(
        LoanEventContext context, PaymentId id, Money amount, InterestAccrual.Projection projection)
    {
        var allocation = Allocate(amount, projection.Position);
        var next = StateAfterPayment(allocation);

        PostAccrual(context, projection);
        Raise(new PaymentReceived(context, id, amount, allocation, next));
        if (allocation.Excess.Amount > 0m)
        {
            Raise(new PaymentExcessRecorded(context, id, allocation.Excess));
        }

        if (next == LoanState.PaidOff)
        {
            Raise(new LoanPaidOff(context, id));
        }

        return new PaymentResult(id, allocation, next);
    }

    private PaymentAllocation Allocate(Money amount, InterestAccrual.Position position) =>
        PaymentAllocationWaterfall.Allocate(
            amount, FeesOutstanding, position.PastDueInterest, position.CurrentInterest, RemainingPrincipal);

    private LoanState StateAfterPayment(PaymentAllocation allocation) =>
        allocation.NewFeesOutstanding.Amount == 0m
        && allocation.NewPastDueInterest.Amount == 0m
        && allocation.NewCurrentInterest.Amount == 0m
        && allocation.NewRemainingPrincipal.Amount == 0m
            ? LoanState.PaidOff
            : State;
}
