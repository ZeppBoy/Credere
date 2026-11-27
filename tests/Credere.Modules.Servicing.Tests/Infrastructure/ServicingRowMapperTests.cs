using Credere.Core.Results;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Infrastructure;
using FluentAssertions;
using Xunit;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;
using static Credere.Modules.Servicing.Tests.Domain.Loans.LoanSamples;
using static Credere.Modules.Servicing.Tests.Infrastructure.PersistenceSamples;

namespace Credere.Modules.Servicing.Tests.Infrastructure;

public sealed class ServicingRowMapperTests
{
    [Fact]
    public void ToView_DisbursedLoan_CarriesTheTermsAndTheOpeningBalances()
    {
        var loan = CreateLoan();
        var row = new LoanRow();

        ServicingRowMapper.Apply(row, loan);
        var view = ServicingRowMapper.ToView(row);

        view.Id.Should().Be(Id);
        view.SourceReference.Should().Be(SourceReference);
        view.TermsVersion.Should().Be(1);
        view.Product.Should().Be("TERM");
        view.Principal.Should().Be(Principal);
        view.RemainingPrincipal.Should().Be(Principal);
        view.FeesOutstanding.Should().Be(Euro(0m));
        view.PastDueInterest.Should().Be(Euro(0m));
        view.CurrentInterest.Should().Be(Euro(0m));
        view.NominalRate.Should().Be(Rate.NominalRate);
        view.DayCountConvention.Should().Be(Rate.DayCountConvention.Name);
        view.Term.Months.Should().Be(24);
        view.DisbursementDate.Should().Be(DisbursedOn);
        view.FirstPaymentDate.Should().Be(FirstDueDate);
        view.AccruedThroughDate.Should().Be(DisbursedOn);
        view.PastDueThreshold.Should().Be(PastDueThreshold);
        view.DefaultThreshold.Should().Be(DefaultThreshold);
        view.State.Should().Be(LoanState.Disbursed);
        view.DaysPastDue.Should().BeNull();
        row.StreamVersion.Should().Be(1);
    }

    [Theory]
    [InlineData(LoanState.Active)]
    [InlineData(LoanState.PastDue)]
    [InlineData(LoanState.Defaulted)]
    [InlineData(LoanState.PaidOff)]
    public void Apply_LoanInAState_CopiesStateDaysAndVersion(LoanState state)
    {
        var loan = CreateLoan(state);
        var row = new LoanRow();

        ServicingRowMapper.Apply(row, loan);

        row.State.Should().Be(state);
        row.DaysPastDue.Should().Be(loan.DaysPastDue);
        row.StreamVersion.Should().Be(loan.Version);
        row.RemainingPrincipal.Should().Be(loan.RemainingPrincipal.Amount);
        row.AccruedThroughDate.Should().Be(loan.AccruedThroughDate);
        row.AccrualSegmentStart.Should().Be(loan.AccrualSegmentStart);
    }

    [Fact]
    public void ToSchedule_RowsOfASchedule_RebuildTheSameScheduleInAnyOrder()
    {
        var rows = ServicingRowMapper.ToScheduleRows(Id, Schedule);

        rows.Should().HaveCount(Schedule.Rows.Count);
        ServicingRowMapper.ToSchedule(rows.Reverse().ToList()).Should().Be(Schedule);
    }

    [Fact]
    public void ToSchedule_NoRows_Throws()
    {
        var act = () => ServicingRowMapper.ToSchedule([]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToIntent_PendingAndDisbursedRows_RestoreTheIntent()
    {
        var pending = NewIntent();
        var disbursed = NewIntent();
        Succeeded(disbursed.MarkDisbursed(NewLoanId()));
        var pendingRow = new DisbursementIntentRow();
        var disbursedRow = new DisbursementIntentRow();
        ServicingRowMapper.Apply(pendingRow, pending);
        ServicingRowMapper.Apply(disbursedRow, disbursed);

        var restoredPending = ServicingRowMapper.ToIntent(pendingRow);
        var restoredDisbursed = ServicingRowMapper.ToIntent(disbursedRow);

        restoredPending.State.Should().Be(DisbursementIntentState.Pending);
        restoredPending.LoanId.Should().BeNull();
        restoredPending.Terms.Should().Be(pending.Terms);
        restoredDisbursed.State.Should().Be(DisbursementIntentState.Disbursed);
        restoredDisbursed.LoanId.Should().Be(disbursed.LoanId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToIntent_StateAndLoanThatNoCommandProduces_Throws(bool pendingWithLoan)
    {
        var row = new DisbursementIntentRow();
        ServicingRowMapper.Apply(row, NewIntent());
        if (pendingWithLoan)
        {
            row.LoanId = NewLoanId();
        }
        else
        {
            row.State = DisbursementIntentState.Disbursed;
        }

        var act = () => ServicingRowMapper.ToIntent(row);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no command produces*");
    }

    [Fact]
    public void ToIntentView_CarriesTheKeyTheTermsAndTheLoan()
    {
        var intent = NewIntent(termsVersion: 3);
        var loanId = NewLoanId();
        Succeeded(intent.MarkDisbursed(loanId));
        var row = new DisbursementIntentRow();
        ServicingRowMapper.Apply(row, intent);

        var view = ServicingRowMapper.ToIntentView(row);

        view.Should().Be(new DisbursementIntentView(
            intent.Id, intent.SourceReference, 3, intent.Product, intent.Terms, DisbursementIntentState.Disbursed, loanId));
    }

    [Fact]
    public void Succeeded_FailedResult_Throws()
    {
        var act = () => Succeeded(Result<int, DomainError>.Failure(new ValidationError("x", "bad")));

        act.Should().Throw<InvalidOperationException>();
    }
}
