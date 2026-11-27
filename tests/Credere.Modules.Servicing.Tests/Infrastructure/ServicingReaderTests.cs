using Credere.Modules.Servicing.Application.Queries.GetLoan;
using Credere.Modules.Servicing.Application.Queries.ListLoans;
using Credere.Modules.Servicing.Contracts;
using FluentAssertions;
using NodaTime;
using Xunit;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;
using static Credere.Modules.Servicing.Tests.Domain.Loans.LoanSamples;
using static Credere.Modules.Servicing.Tests.Infrastructure.PersistenceSamples;

namespace Credere.Modules.Servicing.Tests.Infrastructure;

[Collection(SqlServerTestGroup.Name)]
[Trait("Requires", "Docker")]
public sealed class ServicingReaderTests
{
    private readonly FakeClock _clock = new(Instant.FromUtc(2026, 11, 27, 8, 0));
    private readonly ScenarioDriver _driver;

    public ServicingReaderTests(SqlServerFixture fixture)
    {
        _driver = new ScenarioDriver(fixture, _clock);
    }

    [Fact]
    public async Task GetLoanAsync_StoredLoan_ReturnsItsViewAfterEachStep()
    {
        var (loanId, _, product) = await _driver.DisburseAsync();
        await _driver.ChangeLoanAsync(loanId, loan => Succeeded(loan.Activate(ActivateOn(loanId), new Calendar())));
        await _driver.ChangeLoanAsync(
            loanId, loan => Succeeded(loan.AccrueInterest(AccrueOn(loanId, Date("2026-02-16")), Calculator)));

        await using var unit = _driver.Unit();
        var view = await unit.Reader.GetLoanAsync(loanId, CancellationToken.None);

        view.Should().NotBeNull();
        view!.Id.Should().Be(loanId);
        view.Product.Should().Be(product);
        view.TermsVersion.Should().Be(1);
        view.Principal.Should().Be(Principal);
        view.RemainingPrincipal.Should().Be(Principal);
        view.PastDueInterest.Should().Be(Euro(41.67m));
        view.CurrentInterest.Should().Be(Euro(1.39m));
        view.NominalRate.Should().Be(Rate.NominalRate);
        view.DayCountConvention.Should().Be(Rate.DayCountConvention.Name);
        view.AccruedThroughDate.Should().Be(Date("2026-02-16"));
        view.State.Should().Be(LoanState.Active);
        view.DaysPastDue.Should().BeNull();
    }

    [Fact]
    public async Task GetLoanAsync_UnknownId_ReturnsNull()
    {
        await using var unit = _driver.Unit();

        (await unit.Reader.GetLoanAsync(NewLoanId(), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task GetScheduleAsync_StoredLoan_ReturnsTheSnapshotTakenAtDisbursement()
    {
        var (loanId, _, _) = await _driver.DisburseAsync();
        await _driver.ChangeLoanAsync(loanId, loan => Succeeded(loan.Activate(ActivateOn(loanId), new Calendar())));

        await using var unit = _driver.Unit();
        var schedule = await unit.Reader.GetScheduleAsync(loanId, CancellationToken.None);

        schedule.Should().Be(Schedule);
        (await unit.Reader.GetScheduleAsync(NewLoanId(), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task GetIntentAsync_PendingAndDisbursedIntents_ReturnTheirViews()
    {
        var pending = await _driver.RegisterIntentAsync();
        var (loanId, intentId, _) = await _driver.DisburseAsync();

        await using var unit = _driver.Unit();
        var pendingView = await unit.Reader.GetIntentAsync(pending.Id, CancellationToken.None);
        var disbursedView = await unit.Reader.GetIntentAsync(intentId, CancellationToken.None);

        pendingView!.State.Should().Be(DisbursementIntentState.Pending);
        pendingView.LoanId.Should().BeNull();
        pendingView.Terms.Should().Be(pending.Terms);
        disbursedView!.State.Should().Be(DisbursementIntentState.Disbursed);
        disbursedView.LoanId.Should().Be(loanId);
        (await unit.Reader.GetIntentAsync(NewIntentId(), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ListLoansAsync_ByProduct_ReturnsTheMostRecentlyDisbursedFirst()
    {
        var product = NewProduct();
        var oldest = (await _driver.DisburseAsync(product, new LocalDate(2026, 3, 1))).LoanId;
        var newest = (await _driver.DisburseAsync(product, new LocalDate(2026, 3, 20))).LoanId;
        var middle = (await _driver.DisburseAsync(product, new LocalDate(2026, 3, 10))).LoanId;
        await _driver.DisburseAsync(NewProduct(), new LocalDate(2026, 3, 10));

        var views = await ListAsync(new LoanListFilters(null, product, null, null));

        views.Select(view => view.Id).Should().Equal(newest, middle, oldest);
    }

    [Fact]
    public async Task ListLoansAsync_ByStates_ReturnsOnlyThoseStates()
    {
        var product = NewProduct();
        var disbursed = (await _driver.DisburseAsync(product)).LoanId;
        var active = (await _driver.DisburseAsync(product)).LoanId;
        await _driver.ChangeLoanAsync(active, loan => Succeeded(loan.Activate(ActivateOn(active), new Calendar())));

        var onlyActive = await ListAsync(new LoanListFilters([LoanState.Active], product, null, null));
        var both = await ListAsync(new LoanListFilters([LoanState.Active, LoanState.Disbursed], product, null, null));

        onlyActive.Select(view => view.Id).Should().Equal(active);
        both.Select(view => view.Id).Should().BeEquivalentTo([active, disbursed]);
    }

    [Fact]
    public async Task ListLoansAsync_NullStates_MatchesEveryStateAndAnEmptyListMatchesNone()
    {
        var product = NewProduct();
        await _driver.DisburseAsync(product);

        (await ListAsync(new LoanListFilters(null, product, null, null))).Should().HaveCount(1);
        (await ListAsync(new LoanListFilters([], product, null, null))).Should().BeEmpty();
    }

    [Fact]
    public async Task ListLoansAsync_DateWindow_IncludesBothEnds()
    {
        var product = NewProduct();
        var before = (await _driver.DisburseAsync(product, new LocalDate(2026, 4, 9))).LoanId;
        var atStart = (await _driver.DisburseAsync(product, new LocalDate(2026, 4, 10))).LoanId;
        var inside = (await _driver.DisburseAsync(product, new LocalDate(2026, 4, 15))).LoanId;
        var atEnd = (await _driver.DisburseAsync(product, new LocalDate(2026, 4, 20))).LoanId;
        var after = (await _driver.DisburseAsync(product, new LocalDate(2026, 4, 21))).LoanId;

        var views = await ListAsync(new LoanListFilters(null, product, new LocalDate(2026, 4, 10), new LocalDate(2026, 4, 20)));

        views.Select(view => view.Id).Should().Equal(atEnd, inside, atStart);
        views.Select(view => view.Id).Should().NotContain([before, after]);
    }

    private async Task<IReadOnlyList<LoanView>> ListAsync(LoanListFilters filters)
    {
        await using var unit = _driver.Unit();
        return await unit.Reader.ListLoansAsync(filters, CancellationToken.None);
    }
}
