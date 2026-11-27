using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Application.Commands.Activate;
using Credere.Modules.Servicing.Application.Commands.ApplyAging;
using Credere.Modules.Servicing.Application.Commands.Disburse;
using Credere.Modules.Servicing.Application.Commands.RecordPayment;
using Credere.Modules.Servicing.Application.Commands.Recover;
using Credere.Modules.Servicing.Application.Commands.Restructure;
using Credere.Modules.Servicing.Application.Commands.WriteOff;
using Credere.Modules.Servicing.Application.Queries.QuotePayoff;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain;
using NodaTime;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;

namespace Credere.Modules.Servicing.Tests.Domain.Loans;

internal static class LoanSamples
{
    // D5 and the M1 Level30E360_24m fixture supply the amount, rate, term and dates.
    // The configurable grace threshold leaves room to exercise both entry and re-aging.
    internal const int PastDueThreshold = 3;
    internal const int DefaultThreshold = 90;

    internal static LoanId Id { get; } = new(Guid.NewGuid());

    internal static DisbursementIntentId IntentId { get; } = new(Guid.NewGuid());

    internal static Guid SourceReference { get; } = Guid.NewGuid();

    internal static Guid CorrelationId { get; } = Guid.NewGuid();

    internal static PaymentId SamplePaymentId { get; } = new(Guid.NewGuid());

    internal static LocalDate DisbursedOn { get; } = Date("2026-01-15");

    internal static LocalDate ActivatedOn => DisbursedOn.PlusDays(1);

    internal static LocalDate FirstDueDate => Schedule.Rows[0].PaymentDate;

    internal static ProductTerms Product { get; } = new(
        Rate.DayCountConvention, PaymentFrequency.Monthly, RepaymentType.InterestOnlyBullet,
        PastDueThreshold, DefaultThreshold, "weekdays");

    internal static DisbursementIntent Intent() => new(
        IntentId, SourceReference, 1, "TERM", new AcceptedLoanTerms(Principal, Rate.NominalRate, LoanTerm.FromMonths(24)));

    internal static Disburse DisburseCommand() => new(Guid.NewGuid(), IntentId, DisbursedOn, FirstDueDate, CorrelationId);

    internal static Loan CreateLoan(LoanState state = LoanState.Disbursed)
    {
        var loan = Loan.Disburse(DisburseCommand(), Id, Intent(), Product, Schedule).Value;
        if (state == LoanState.Disbursed)
        {
            return loan;
        }

        _ = loan.Activate(new Activate(Guid.NewGuid(), Id, ActivatedOn, CorrelationId), new Calendar()).Value;
        switch (state)
        {
            case LoanState.Active:
                break;
            case LoanState.InGrace:
                _ = loan.ApplyAging(Age(1)).Value;
                break;
            case LoanState.PastDue:
                _ = loan.ApplyAging(Age(PastDueThreshold)).Value;
                break;
            case LoanState.PaidOff:
                var quote = loan.QuotePayoff(new QuotePayoff(Id, ActivatedOn), Calculator).Value;
                _ = loan.RecordPayment(Pay(quote.Total, ActivatedOn), SamplePaymentId, Calculator).Value;
                break;
            default:
                _ = loan.ApplyAging(Age(DefaultThreshold)).Value;
                switch (state)
                {
                    case LoanState.WrittenOff:
                        _ = loan.WriteOff(WriteOffAt(FirstDueDate), Calculator).Value;
                        break;
                    case LoanState.Restructured:
                        _ = loan.Restructure(RestructureAt(FirstDueDate)).Value;
                        break;
                    case LoanState.Recovered:
                        _ = loan.Recover(RecoverAt(FirstDueDate)).Value;
                        break;
                }

                break;
        }

        return loan;
    }

    internal static ApplyAging Age(int days) => new(Guid.NewGuid(), Id, FirstDueDate, days, CorrelationId);

    internal static RecordPayment Pay(Money money, LocalDate date) => new(Guid.NewGuid(), Id, money, date, CorrelationId);

    internal static WriteOff WriteOffAt(LocalDate date) => new(Guid.NewGuid(), Id, date, "Portion uncollectible", CorrelationId);

    internal static Restructure RestructureAt(LocalDate date) => new(Guid.NewGuid(), Id, date, "Agreed plan", CorrelationId);

    internal static Recover RecoverAt(LocalDate date) => new(Guid.NewGuid(), Id, date, "Settlement", Euro(0m), CorrelationId);

    internal sealed class Calendar(LocalDate? holiday = null) : IBusinessCalendar
    {
        public bool IsBusinessDay(LocalDate date) =>
            date.DayOfWeek is not (IsoDayOfWeek.Saturday or IsoDayOfWeek.Sunday) && date != holiday;
    }
}
