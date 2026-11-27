using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Application.Commands.Activate;
using Credere.Modules.Servicing.Application.Commands.ApplyAging;
using Credere.Modules.Servicing.Application.Commands.Disburse;
using Credere.Modules.Servicing.Application.Commands.Recover;
using Credere.Modules.Servicing.Application.Commands.Restructure;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain.Events;
using NodaTime;

namespace Credere.Modules.Servicing.Domain;

/// <summary>Applies the state transitions and payment rules in docs/domain/loan-lifecycle.md.</summary>
internal sealed partial class Loan
{
    private readonly List<LoanEvent> _uncommittedEvents = [];
    private InterestAccrual.Position _interest;

    private Loan(LoanDisbursed disbursed, FixedRate rate)
    {
        Id = disbursed.Context.LoanId;
        DisbursementIntentId = disbursed.IntentId;
        SourceReference = disbursed.SourceReference;
        TermsVersion = disbursed.AcceptedTermsVersion;
        Product = disbursed.Product;
        Principal = disbursed.AcceptedTerms.Principal;
        RemainingPrincipal = Principal;
        Term = disbursed.AcceptedTerms.Term;
        Rate = rate;
        ProductTerms = disbursed.ProductTerms;
        DisbursementDate = disbursed.Context.EffectiveDate;
        FirstPaymentDate = disbursed.FirstPaymentDate;
        Schedule = disbursed.Schedule;

        var zero = Money.Zero(Principal.Currency);
        _interest = new InterestAccrual.Position(DisbursementDate, DisbursementDate, zero, zero);
        FeesOutstanding = zero;
        State = LoanState.Disbursed;
        Version = 1;
    }

    internal LoanId Id { get; }

    internal DisbursementIntentId DisbursementIntentId { get; }

    internal Guid SourceReference { get; }

    internal int TermsVersion { get; }

    internal string Product { get; }

    internal Money Principal { get; }

    internal Money RemainingPrincipal { get; private set; }

    internal Money FeesOutstanding { get; private set; }

    internal Money PastDueInterest => _interest.PastDueInterest;

    internal Money CurrentInterest => _interest.CurrentInterest;

    internal FixedRate Rate { get; }

    internal LoanTerm Term { get; }

    internal ProductTerms ProductTerms { get; }

    internal PaymentFrequency Frequency => ProductTerms.Frequency;

    internal RepaymentType Repayment => ProductTerms.Repayment;

    internal LocalDate DisbursementDate { get; }

    internal LocalDate FirstPaymentDate { get; }

    internal int PastDueThreshold => ProductTerms.PastDueThreshold;

    internal int DefaultThreshold => ProductTerms.DefaultThreshold;

    internal LocalDate AccruedThroughDate => _interest.AccruedThroughDate;

    internal LocalDate AccrualSegmentStart => _interest.SegmentStart;

    internal AmortizationSchedule Schedule { get; }

    internal LoanState State { get; private set; }

    internal int? DaysPastDue { get; private set; }

    internal long Version { get; private set; }

    internal IReadOnlyList<LoanEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    internal static Result<Loan, DomainError> Disburse(
        Disburse command, LoanId id, DisbursementIntent intent, ProductTerms product, AmortizationSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(schedule);
        if (intent.Id != command.IntentId)
        {
            throw new ArgumentException("The supplied intent belongs to another disbursement.", nameof(intent));
        }

        if (intent.State != DisbursementIntentState.Pending)
        {
            return Result<Loan, DomainError>.Failure(
                new InvalidStateTransition(intent.State.ToString(), nameof(Disburse), "the intent already has a loan"));
        }

        var terms = intent.Terms;
        if (terms.Principal.Amount <= 0m)
        {
            return Result<Loan, DomainError>.Failure(new ValidationError("principal", "must be positive"));
        }

        var rate = FixedRate.Create(terms.NominalRate, product.DayCount);
        if (!rate.IsSuccess)
        {
            return Result<Loan, DomainError>.Failure(rate.Error);
        }

        var request = ScheduleRequest.Create(
            terms.Principal, rate.Value, terms.Term, command.DisbursementDate, command.FirstPaymentDate, product.Frequency);
        if (!request.IsSuccess)
        {
            return Result<Loan, DomainError>.Failure(request.Error);
        }

        if (!MatchesSchedule(request.Value, schedule))
        {
            throw new ArgumentException("The schedule must match the accepted terms and disbursement dates.", nameof(schedule));
        }

        var context = new LoanEventContext(id, command.DisbursementDate, command.CommandId, command.CorrelationId);
        var disbursed = new LoanDisbursed(
            context, intent.Id, intent.SourceReference, intent.AcceptedTermsVersion, intent.Product,
            terms, product, command.FirstPaymentDate, schedule);
        var loan = new Loan(disbursed, rate.Value);
        loan._uncommittedEvents.Add(disbursed);
        return Result<Loan, DomainError>.Success(loan);
    }

    internal Result<Unit, DomainError> Activate(Activate command, IBusinessCalendar calendar)
    {
        CheckId(command.Id);
        ArgumentNullException.ThrowIfNull(calendar);
        if (State != LoanState.Disbursed)
        {
            return Invalid<Unit>(nameof(Activate));
        }

        var date = DisbursementDate;
        while (date < command.EffectiveDate)
        {
            date = date.PlusDays(1);
            if (calendar.IsBusinessDay(date))
            {
                if (date == command.EffectiveDate)
                {
                    Raise(new LoanActivated(Context(date, command.CommandId, command.CorrelationId)));
                    return Success();
                }

                break;
            }
        }

        return Result<Unit, DomainError>.Failure(
            new ValidationError("effectiveDate", "must be the first business day after disbursement"));
    }

    internal Result<AgingResult, DomainError> ApplyAging(ApplyAging command)
    {
        CheckId(command.Id);
        if (!CanAccrue)
        {
            return Invalid<AgingResult>(nameof(ApplyAging));
        }

        if (command.DaysPastDue < 0)
        {
            return Result<AgingResult, DomainError>.Failure(new ValidationError("daysPastDue", "must be zero or greater"));
        }

        var context = Context(command.BusinessDate, command.CommandId, command.CorrelationId);
        var next = AgingState(command.DaysPastDue);
        var days = next == LoanState.Active ? (int?)null : command.DaysPastDue;
        if (State == LoanState.Defaulted && next != LoanState.Defaulted)
        {
            return Invalid<AgingResult>(nameof(ApplyAging));
        }

        if (State == next && DaysPastDue == days)
        {
            return Result<AgingResult, DomainError>.Success(new AgingResult(State, DaysPastDue, Changed: false));
        }

        Raise(AgingEvent(context, next, command.DaysPastDue));
        return Result<AgingResult, DomainError>.Success(new AgingResult(State, DaysPastDue, Changed: true));
    }

    internal Result<Unit, DomainError> Restructure(Restructure command)
    {
        CheckId(command.Id);
        if (State != LoanState.Defaulted)
        {
            return Invalid<Unit>(nameof(Restructure));
        }

        if (string.IsNullOrWhiteSpace(command.Plan))
        {
            return Result<Unit, DomainError>.Failure(new ValidationError("plan", "must not be empty"));
        }

        Raise(new LoanRestructured(Context(command.BusinessDate, command.CommandId, command.CorrelationId), command.Plan));
        return Success();
    }

    internal Result<Unit, DomainError> Recover(Recover command)
    {
        CheckId(command.Id);
        if (State != LoanState.Defaulted)
        {
            return Invalid<Unit>(nameof(Recover));
        }

        if (string.IsNullOrWhiteSpace(command.Method))
        {
            return Result<Unit, DomainError>.Failure(new ValidationError("method", "must not be empty"));
        }

        if (command.RecoveredAmount is null || command.RecoveredAmount.Amount < 0m)
        {
            return Result<Unit, DomainError>.Failure(
                new ValidationError("recoveredAmount", "must be zero or greater"));
        }

        Raise(new LoanRecovered(
            Context(command.BusinessDate, command.CommandId, command.CorrelationId), command.Method, command.RecoveredAmount));
        return Success();
    }

    internal void MarkEventsCommitted() => _uncommittedEvents.Clear();

    private bool CanPay => State is LoanState.Active or LoanState.InGrace or LoanState.PastDue;

    private bool CanAccrue => CanPay || State == LoanState.Defaulted;

    private static bool MatchesSchedule(ScheduleRequest request, AmortizationSchedule schedule)
    {
        var monthsPerPayment = request.PaymentFrequency == PaymentFrequency.Monthly ? 1 : 3;
        return schedule.Principal == request.Principal
            && schedule.Rows.Count == request.Term.Months / monthsPerPayment
            && schedule.Rows.Select((row, index) =>
                row.PaymentDate == request.FirstPaymentDate.PlusMonths(index * monthsPerPayment)
                && row.CapitalisedInterest.Amount == 0m).All(matches => matches);
    }

    private LoanState AgingState(int days) => days switch
    {
        0 => LoanState.Active,
        _ when days >= DefaultThreshold => LoanState.Defaulted,
        _ when days >= PastDueThreshold => LoanState.PastDue,
        _ => LoanState.InGrace,
    };

    private LoanAgingEvent AgingEvent(LoanEventContext context, LoanState next, int days) => next switch
    {
        _ when next == State => new LoanPastDueDaysUpdated(context, State, days),
        LoanState.Active => new LoanBecameActive(context, State),
        LoanState.InGrace => new LoanEnteredGracePeriod(context, State, days),
        LoanState.PastDue => new LoanBecamePastDue(context, State, days),
        LoanState.Defaulted => new LoanDefaulted(context, State, days),
        _ => throw new InvalidOperationException("Aging must end in an aging state."),
    };

    private bool IsLoanAmount(Money? money) => money is not null && money.Currency == Principal.Currency && money.Amount >= 0m;

    private void CheckId(LoanId id)
    {
        if (id != Id)
        {
            throw new ArgumentException("The supplied command or query belongs to another loan.", nameof(id));
        }
    }

    private LoanEventContext Context(LocalDate date, Guid commandId, Guid correlationId) =>
        new(Id, date, commandId, correlationId);

    private static Result<Unit, DomainError> Success() => Result<Unit, DomainError>.Success(Unit.Value);

    private Result<T, DomainError> Invalid<T>(string command) => Result<T, DomainError>.Failure(
        new InvalidStateTransition(State.ToString(), command, "the current loan state does not allow this operation"));
}
