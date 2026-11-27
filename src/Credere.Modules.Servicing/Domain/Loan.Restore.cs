using Credere.Core.Rates;
using Credere.Core.Schedules;
using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Domain;

internal sealed partial class Loan
{
    /// <summary>Restores a committed loan and its open accrual segment for command handling.</summary>
    /// <param name="snapshot">The current-state row and schedule snapshot mapped to domain types.</param>
    /// <returns>The loan at the stored stream version, with an empty list of uncommitted events.</returns>
    /// <exception cref="InvalidOperationException">The stored loan data is inconsistent.</exception>
    /// <remarks>After activation, the version is checked against the minimum event count for the stored state.</remarks>
    internal static Loan Restore(LoanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var terms = snapshot.AcceptedTerms;
        var product = snapshot.ProductTerms;
        var rate = FixedRate.Create(terms.NominalRate, product.DayCount);
        if (!rate.IsSuccess || terms.Principal.Amount <= 0m || snapshot.TermsVersion <= 0
            || string.IsNullOrWhiteSpace(snapshot.Product))
        {
            throw new InvalidOperationException("Stored loan terms are invalid.");
        }

        var request = ScheduleRequest.Create(
            terms.Principal, rate.Value, terms.Term, snapshot.DisbursementDate, snapshot.FirstPaymentDate, product.Frequency);
        if (!request.IsSuccess || !MatchesSchedule(request.Value, snapshot.Schedule))
        {
            throw new InvalidOperationException("The stored schedule does not match the disbursed loan.");
        }

        var loan = new Loan(snapshot, rate.Value);
        loan.EnsureRestorableState();
        return loan;
    }

    private void EnsureRestorableState()
    {
        // Accruals, payments and aging can extend the stream after activation.
        var minimumVersion = State switch
        {
            LoanState.Disbursed => 1,
            LoanState.Active => 2,
            LoanState.InGrace or LoanState.PastDue or LoanState.Defaulted => 3,
            _ => 4,
        };
        if (Version < minimumVersion || !IsLoanAmount(RemainingPrincipal) || RemainingPrincipal.Amount > Principal.Amount
            || !IsLoanAmount(FeesOutstanding) || !IsLoanAmount(CurrentInterest) || !IsLoanAmount(PastDueInterest))
        {
            throw new InvalidOperationException("The stored loan version or balances are invalid.");
        }

        if (DisbursementDate.Calendar != AccrualSegmentStart.Calendar
            || AccrualSegmentStart.Calendar != AccruedThroughDate.Calendar
            || DisbursementDate > AccrualSegmentStart || AccrualSegmentStart > AccruedThroughDate)
        {
            throw new InvalidOperationException("The stored accrual segment is outside the posted accrual period.");
        }

        var principalCleared = RemainingPrincipal.Amount == 0m;
        var interestAndFeesCleared = CurrentInterest.Amount == 0m && PastDueInterest.Amount == 0m && FeesOutstanding.Amount == 0m;
        var validState = State switch
        {
            LoanState.Disbursed => Version == 1 && DaysPastDue is null && RemainingPrincipal == Principal
                && interestAndFeesCleared && AccruedThroughDate == DisbursementDate,
            LoanState.Active => DaysPastDue is null,
            LoanState.InGrace or LoanState.PastDue or LoanState.Defaulted =>
                DaysPastDue is > 0 && AgingState(DaysPastDue.Value) == State,
            LoanState.WrittenOff or LoanState.Restructured or LoanState.Recovered => DaysPastDue >= DefaultThreshold,
            LoanState.PaidOff => DaysPastDue >= 0 && interestAndFeesCleared,
            _ => false,
        };
        if (!validState || principalCleared != (State == LoanState.PaidOff))
        {
            throw new InvalidOperationException("The stored loan version, balances, accrual or days past due do not match its state.");
        }
    }
}
