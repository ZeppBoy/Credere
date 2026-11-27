using Credere.Core.Rates;
using Credere.Core.Schedules;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain.Events;

namespace Credere.Modules.Servicing.Domain;

internal sealed partial class Loan
{
    /// <summary>Restores one loan from an ordered history without raising new events.</summary>
    internal static Loan Rehydrate(IEnumerable<LoanEvent> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        using var events = history.GetEnumerator();
        if (!events.MoveNext() || events.Current is not LoanDisbursed disbursed)
        {
            throw new InvalidOperationException("Loan history must start with a disbursement.");
        }

        var terms = disbursed.AcceptedTerms;
        var rate = FixedRate.Create(terms.NominalRate, disbursed.ProductTerms.DayCount);
        if (!rate.IsSuccess || terms.Principal.Amount <= 0m || disbursed.AcceptedTermsVersion <= 0
            || string.IsNullOrWhiteSpace(disbursed.Product))
        {
            throw new InvalidOperationException("Stored loan terms are invalid.");
        }

        var request = ScheduleRequest.Create(
            terms.Principal, rate.Value, terms.Term, disbursed.Context.EffectiveDate,
            disbursed.FirstPaymentDate, disbursed.ProductTerms.Frequency);
        if (!request.IsSuccess || !MatchesSchedule(request.Value, disbursed.Schedule))
        {
            throw new InvalidOperationException("The stored schedule does not match the disbursed loan.");
        }

        var loan = new Loan(disbursed, rate.Value);
        PaymentReceived? closingPayment = null;
        var expectExcess = false;
        while (events.MoveNext())
        {
            var next = events.Current;
            if (next is null || next.Context.LoanId != loan.Id || !loan.IsReplayable(next))
            {
                throw new InvalidOperationException("Loan history contains an invalid event.");
            }

            if (closingPayment is not null)
            {
                if (expectExcess)
                {
                    if (next is not PaymentExcessRecorded excess
                        || excess.Context != closingPayment.Context || excess.PaymentId != closingPayment.PaymentId
                        || excess.Excess != closingPayment.Allocation.Excess)
                    {
                        throw new InvalidOperationException("The closing payment is missing its matching excess event.");
                    }

                    expectExcess = false;
                }
                else
                {
                    if (next is not LoanPaidOff paidOff
                        || paidOff.Context != closingPayment.Context || paidOff.FinalPaymentId != closingPayment.PaymentId)
                    {
                        throw new InvalidOperationException("The closing payment is missing its matching payoff event.");
                    }

                    closingPayment = null;
                }
            }
            else if (next is PaymentReceived { State: LoanState.PaidOff } payment)
            {
                closingPayment = payment;
                expectExcess = payment.Allocation.Excess.Amount > 0m;
            }
            else if (next is PaymentExcessRecorded or LoanPaidOff)
            {
                throw new InvalidOperationException("A payment outcome must follow its closing payment.");
            }

            loan.Apply(next);
        }

        if (closingPayment is not null)
        {
            throw new InvalidOperationException("Loan history ends before its closing payment is complete.");
        }

        return loan;
    }

    private bool IsReplayable(LoanEvent @event) => @event switch
    {
        LoanActivated => State == LoanState.Disbursed && @event.Context.EffectiveDate > DisbursementDate,
        LoanAgingEvent aging => IsReplayableAging(aging),
        InterestAccrued accrual => CanAccrue
            && accrual.PeriodStart == AccruedThroughDate
            && accrual.Context.EffectiveDate > AccruedThroughDate
            && accrual.SegmentStart >= AccrualSegmentStart
            && accrual.SegmentStart <= accrual.Context.EffectiveDate
            && IsLoanAmount(accrual.Amount) && IsLoanAmount(accrual.CurrentInterest) && IsLoanAmount(accrual.PastDueInterest),
        PaymentReceived payment => IsReplayablePayment(payment),
        PaymentExcessRecorded or LoanPaidOff => CanPay,
        LoanWrittenOff writeOff => State == LoanState.Defaulted
            && writeOff.Context.EffectiveDate == AccruedThroughDate
            && !string.IsNullOrWhiteSpace(writeOff.Reason)
            && writeOff.RemainingPrincipal == RemainingPrincipal
            && writeOff.PastDueInterest == PastDueInterest
            && writeOff.CurrentInterest == CurrentInterest
            && writeOff.FeesOutstanding == FeesOutstanding
            && writeOff.WrittenOffBalance == OutstandingTotal(_interest),
        LoanRestructured restructure => State == LoanState.Defaulted && !string.IsNullOrWhiteSpace(restructure.Plan),
        LoanRecovered recover => State == LoanState.Defaulted
            && !string.IsNullOrWhiteSpace(recover.Method) && recover.RecoveredAmount is { Amount: >= 0m },
        _ => false,
    };

    private bool IsReplayableAging(LoanAgingEvent aging)
    {
        var days = aging.DaysPastDue ?? 0;
        return CanAccrue
            && days >= 0
            && (State != LoanState.Defaulted || days >= DefaultThreshold)
            && (State != aging.NewState || DaysPastDue != aging.DaysPastDue)
            && AgingEvent(aging.Context, AgingState(days), days) == aging;
    }

    private bool IsReplayablePayment(PaymentReceived payment)
    {
        if (!CanPay || payment.Context.EffectiveDate != AccruedThroughDate
            || !IsLoanAmount(payment.Amount) || payment.Amount.Amount == 0m)
        {
            return false;
        }

        var allocation = Allocate(payment.Amount, _interest);
        return payment.Allocation == allocation && payment.State == StateAfterPayment(allocation);
    }

    private void Raise(LoanEvent @event)
    {
        Apply(@event);
        _uncommittedEvents.Add(@event);
    }

    private void Apply(LoanEvent @event)
    {
        switch (@event)
        {
            case LoanActivated:
                State = LoanState.Active;
                break;
            case LoanAgingEvent aging:
                State = aging.NewState;
                DaysPastDue = aging.DaysPastDue;
                break;
            case InterestAccrued accrual:
                _interest = new InterestAccrual.Position(
                    accrual.Context.EffectiveDate, accrual.SegmentStart, accrual.CurrentInterest, accrual.PastDueInterest);
                break;
            case PaymentReceived payment:
                var allocation = payment.Allocation;
                RemainingPrincipal = allocation.NewRemainingPrincipal;
                FeesOutstanding = allocation.NewFeesOutstanding;
                _interest = _interest with
                {
                    CurrentInterest = allocation.NewCurrentInterest,
                    PastDueInterest = allocation.NewPastDueInterest,
                    SegmentStart = allocation.PrincipalAllocated.Amount > 0m
                        ? payment.Context.EffectiveDate : AccrualSegmentStart,
                };
                break;
            case PaymentExcessRecorded:
                break;
            case LoanPaidOff:
                State = LoanState.PaidOff;
                DaysPastDue ??= 0;
                break;
            case LoanWrittenOff:
                State = LoanState.WrittenOff;
                break;
            case LoanRestructured:
                State = LoanState.Restructured;
                break;
            case LoanRecovered:
                State = LoanState.Recovered;
                break;
            default:
                throw new InvalidOperationException("Unsupported loan event.");
        }

        Version++;
    }
}
