using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Application.Queries.GetLoan;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain;

namespace Credere.Modules.Servicing.Infrastructure;

/// <summary>
/// Copies aggregates onto their current-state rows, and rows onto the public views and the schedule. Every amount of a
/// loan shares the loan's currency, which the aggregate guarantees.
/// </summary>
internal static class ServicingRowMapper
{
    /// <summary>Writes the loan's state onto <paramref name="row"/>, including its stream version.</summary>
    /// <param name="row">The row to fill. A tracked row keeps its concurrency token.</param>
    /// <param name="loan">The aggregate.</param>
    internal static void Apply(LoanRow row, Loan loan)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(loan);

        row.Id = loan.Id;
        row.DisbursementIntentId = loan.DisbursementIntentId;
        row.SourceReference = loan.SourceReference;
        row.TermsVersion = loan.TermsVersion;
        row.Product = loan.Product;
        row.Currency = loan.Principal.Currency;
        row.Principal = loan.Principal.Amount;
        row.RemainingPrincipal = loan.RemainingPrincipal.Amount;
        row.FeesOutstanding = loan.FeesOutstanding.Amount;
        row.PastDueInterest = loan.PastDueInterest.Amount;
        row.CurrentInterest = loan.CurrentInterest.Amount;
        row.NominalRate = loan.Rate.NominalRate.Value;
        row.DayCountConvention = loan.Rate.DayCountConvention.Name;
        row.TermMonths = loan.Term.Months;
        row.Frequency = loan.Frequency;
        row.Repayment = loan.Repayment;
        row.PastDueThreshold = loan.PastDueThreshold;
        row.DefaultThreshold = loan.DefaultThreshold;
        row.Calendar = loan.ProductTerms.Calendar;
        row.DisbursementDate = loan.DisbursementDate;
        row.FirstPaymentDate = loan.FirstPaymentDate;
        row.AccruedThroughDate = loan.AccruedThroughDate;
        row.AccrualSegmentStart = loan.AccrualSegmentStart;
        row.State = loan.State;
        row.DaysPastDue = loan.DaysPastDue;
        row.StreamVersion = loan.Version;
    }

    /// <summary>Builds the public view of a loan row.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The view.</returns>
    /// <exception cref="InvalidOperationException">The row holds a value the domain would not have stored.</exception>
    internal static LoanView ToView(LoanRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new LoanView(
            row.Id,
            row.SourceReference,
            row.TermsVersion,
            row.Product,
            ToMoney(row.Principal, row.Currency),
            ToMoney(row.RemainingPrincipal, row.Currency),
            ToMoney(row.FeesOutstanding, row.Currency),
            ToMoney(row.PastDueInterest, row.Currency),
            ToMoney(row.CurrentInterest, row.Currency),
            Percentage.Create(row.NominalRate).Value,
            row.DayCountConvention,
            LoanTerm.Create(row.TermMonths).Value,
            row.Frequency,
            row.Repayment,
            row.DisbursementDate,
            row.FirstPaymentDate,
            row.AccruedThroughDate,
            row.PastDueThreshold,
            row.DefaultThreshold,
            row.State,
            row.DaysPastDue);
    }

    /// <summary>Builds one row for each row of a schedule snapshot.</summary>
    /// <param name="loanId">The loan the schedule belongs to.</param>
    /// <param name="schedule">The schedule taken at disbursement.</param>
    /// <returns>The rows, in schedule order.</returns>
    internal static IReadOnlyList<LoanScheduleRow> ToScheduleRows(LoanId loanId, AmortizationSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        return schedule.Rows
            .Select(row => new LoanScheduleRow
            {
                LoanId = loanId,
                Index = row.Index,
                PaymentDate = row.PaymentDate,
                Currency = row.OpeningBalance.Currency,
                OpeningBalance = row.OpeningBalance.Amount,
                CapitalisedInterest = row.CapitalisedInterest.Amount,
                InterestDue = row.InterestDue.Amount,
                PrincipalDue = row.PrincipalDue.Amount,
                TotalPayment = row.TotalPayment.Amount,
                ClosingBalance = row.ClosingBalance.Amount,
            })
            .ToList();
    }

    /// <summary>Rebuilds a schedule from its snapshot rows, checking the schedule invariants.</summary>
    /// <param name="rows">The snapshot rows of one loan, in any order.</param>
    /// <returns>The schedule.</returns>
    /// <exception cref="InvalidOperationException">The rows do not form a valid schedule.</exception>
    internal static AmortizationSchedule ToSchedule(IReadOnlyCollection<LoanScheduleRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var ordered = rows
            .OrderBy(row => row.Index)
            .Select(row => new ScheduleRow(
                row.Index,
                row.PaymentDate,
                ToMoney(row.OpeningBalance, row.Currency),
                ToMoney(row.CapitalisedInterest, row.Currency),
                ToMoney(row.InterestDue, row.Currency),
                ToMoney(row.PrincipalDue, row.Currency),
                ToMoney(row.TotalPayment, row.Currency),
                ToMoney(row.ClosingBalance, row.Currency)))
            .ToList();
        if (ordered.Count == 0)
        {
            throw new InvalidOperationException("A schedule snapshot must have at least one row.");
        }

        var result = AmortizationSchedule.Create(ordered[0].OpeningBalance, ordered);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"The stored schedule is invalid: {result.Error.Message}");
    }

    /// <summary>Writes the intent's state onto <paramref name="row"/>.</summary>
    /// <param name="row">The row to fill. A tracked row keeps its concurrency token.</param>
    /// <param name="intent">The intent.</param>
    internal static void Apply(DisbursementIntentRow row, DisbursementIntent intent)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(intent);

        row.Id = intent.Id;
        row.SourceReference = intent.SourceReference;
        row.AcceptedTermsVersion = intent.AcceptedTermsVersion;
        row.Product = intent.Product;
        row.Currency = intent.Terms.Principal.Currency;
        row.Principal = intent.Terms.Principal.Amount;
        row.NominalRate = intent.Terms.NominalRate.Value;
        row.TermMonths = intent.Terms.Term.Months;
        row.State = intent.State;
        row.LoanId = intent.LoanId;
    }

    /// <summary>Rebuilds an intent from its row.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The intent, disbursed when the row says so.</returns>
    /// <exception cref="InvalidOperationException">The row holds a state the domain would not have stored.</exception>
    internal static DisbursementIntent ToIntent(DisbursementIntentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var intent = new DisbursementIntent(row.Id, row.SourceReference, row.AcceptedTermsVersion, row.Product, ToTerms(row));
        switch (row.State)
        {
            case DisbursementIntentState.Pending when row.LoanId is null:
                break;
            case DisbursementIntentState.Disbursed when row.LoanId is { } loanId:
                var marked = intent.MarkDisbursed(loanId);
                if (!marked.IsSuccess)
                {
                    throw new InvalidOperationException($"The stored intent cannot be restored: {marked.Error.Message}");
                }

                break;
            default:
                throw new InvalidOperationException(
                    $"The intent {row.Id.Value} is {row.State} with loan {row.LoanId?.Value.ToString() ?? "none"}, which no command produces.");
        }

        return intent;
    }

    /// <summary>Builds the public view of an intent row.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The view.</returns>
    internal static DisbursementIntentView ToIntentView(DisbursementIntentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new DisbursementIntentView(
            row.Id, row.SourceReference, row.AcceptedTermsVersion, row.Product, ToTerms(row), row.State, row.LoanId);
    }

    private static AcceptedLoanTerms ToTerms(DisbursementIntentRow row) => new(
        ToMoney(row.Principal, row.Currency),
        Percentage.Create(row.NominalRate).Value,
        LoanTerm.Create(row.TermMonths).Value);

    private static Money ToMoney(decimal amount, string currency) => Money.Create(amount, currency).Value;
}
