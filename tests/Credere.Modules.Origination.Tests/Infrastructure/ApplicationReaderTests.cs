using Credere.Modules.Origination.Application.Queries.GetApplication;
using Credere.Modules.Origination.Application.Queries.ListApplications;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain;
using FluentAssertions;
using NodaTime;
using Xunit;
using static Credere.Modules.Origination.Tests.Domain.ApplicationSamples;
using static Credere.Modules.Origination.Tests.Infrastructure.PersistenceSamples;

namespace Credere.Modules.Origination.Tests.Infrastructure;

[Collection(SqlServerTestGroup.Name)]
[Trait("Requires", "Docker")]
public sealed class ApplicationReaderTests(SqlServerFixture fixture)
{
    private static readonly Instant T0 = Instant.FromUtc(2026, 11, 1, 9, 0);
    private readonly FakeClock _clock = new(Instant.FromUtc(2026, 11, 27, 8, 0));

    [Fact]
    public async Task GetAsync_StoredApplication_ReturnsItsView()
    {
        var id = NewId();
        var product = NewProduct();
        var application = NewSubmitted(id, product);
        Approve(application);
        await SaveAsync(application);

        await using var unit = new UnitOfWork(fixture, _clock);
        var view = await unit.Reader.GetAsync(id, CancellationToken.None);

        view.Should().NotBeNull();
        view!.Id.Should().Be(id);
        view.Applicant.Should().Be(Applicant());
        view.Product.Should().Be(product);
        view.RequestedTerms.Should().Be(RequestedTerms());
        view.DecidedTerms.Should().Be(FinalTerms());
        view.TermsVersion.Should().Be(1);
        view.State.Should().Be(LoanApplicationState.Approved);
        view.LastOutcome.Should().Be(DecisionOutcome.Approve);
        view.LastRationale.Should().Be("Meets the underwriting rules.");
        view.SubmittedAt.Should().Be(SubmittedAt);
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        await using var unit = new UnitOfWork(fixture, _clock);

        (await unit.Reader.GetAsync(NewId(), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ViewAfterEveryStep_FollowsTheStateRow()
    {
        var id = NewId();
        var application = NewSubmitted(id);
        await SaveAsync(application);
        await using (var unit = new UnitOfWork(fixture, _clock))
        {
            var loaded = (await unit.Repository.FindAsync(id, CancellationToken.None))!;
            Decline(loaded);
            unit.Repository.Update(loaded);
            (await unit.Repository.CommitAsync(id, CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        await using var reader = new UnitOfWork(fixture, _clock);
        var view = await reader.Reader.GetAsync(id, CancellationToken.None);

        view!.State.Should().Be(LoanApplicationState.Declined);
        view.LastOutcome.Should().Be(DecisionOutcome.Decline);
        view.DecidedTerms.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_ByProduct_ReturnsNewestSubmissionFirst()
    {
        var product = NewProduct();
        var oldest = await SaveWithAsync(product, T0);
        var newest = await SaveWithAsync(product, T0.Plus(Duration.FromDays(2)));
        var middle = await SaveWithAsync(product, T0.Plus(Duration.FromDays(1)));
        await SaveWithAsync(NewProduct(), T0);

        var views = await ListAsync(new ApplicationListFilters(null, product, null, null));

        views.Select(view => view.Id).Should().Equal(newest, middle, oldest);
    }

    [Fact]
    public async Task ListAsync_ByStates_ReturnsOnlyThoseStates()
    {
        var product = NewProduct();
        var submitted = await SaveWithAsync(product, T0);
        var declinedId = NewId();
        var declined = NewSubmitted(declinedId, product, T0);
        Decline(declined);
        await SaveAsync(declined);
        var approvedId = NewId();
        var approved = NewSubmitted(approvedId, product, T0);
        Approve(approved);
        await SaveAsync(approved);

        var views = await ListAsync(new ApplicationListFilters(
            [LoanApplicationState.Declined, LoanApplicationState.Approved], product, null, null));

        views.Select(view => view.Id).Should().BeEquivalentTo([declinedId, approvedId]);
        views.Select(view => view.Id).Should().NotContain(submitted);
    }

    [Fact]
    public async Task ListAsync_NullStates_MatchesEveryStateAndAnEmptyListMatchesNone()
    {
        var product = NewProduct();
        await SaveWithAsync(product, T0);
        var declined = NewSubmitted(NewId(), product, T0);
        Decline(declined);
        await SaveAsync(declined);

        (await ListAsync(new ApplicationListFilters(null, product, null, null))).Should().HaveCount(2);
        (await ListAsync(new ApplicationListFilters([], product, null, null))).Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_TimeWindow_IncludesTheStartAndExcludesTheEnd()
    {
        var product = NewProduct();
        var atStart = await SaveWithAsync(product, T0);
        var inside = await SaveWithAsync(product, T0.Plus(Duration.FromHours(1)));
        var atEnd = await SaveWithAsync(product, T0.Plus(Duration.FromHours(2)));
        var before = await SaveWithAsync(product, T0.Minus(Duration.FromTicks(1)));

        var views = await ListAsync(new ApplicationListFilters(null, product, T0, T0.Plus(Duration.FromHours(2))));

        views.Select(view => view.Id).Should().Equal(inside, atStart);
        views.Select(view => view.Id).Should().NotContain([atEnd, before]);
    }

    [Fact]
    public async Task ListAsync_AllFiltersEmpty_StillReturnsStoredApplications()
    {
        var product = NewProduct();
        await SaveWithAsync(product, T0);

        var views = await ListAsync(new ApplicationListFilters(null, null, null, null));

        views.Should().Contain(view => view.Product == product);
    }

    private async Task<IReadOnlyList<ApplicationView>> ListAsync(ApplicationListFilters filters)
    {
        await using var unit = new UnitOfWork(fixture, _clock);
        return await unit.Reader.ListAsync(filters, CancellationToken.None);
    }

    private async Task<LoanApplicationId> SaveWithAsync(ProductCode product, Instant submittedAt)
    {
        var id = NewId();
        await SaveAsync(NewSubmitted(id, product, submittedAt));
        return id;
    }

    private async Task SaveAsync(LoanApplication application)
    {
        await using var unit = new UnitOfWork(fixture, _clock);
        unit.Repository.Add(application);
        (await unit.Repository.CommitAsync(application.Id, CancellationToken.None)).IsSuccess.Should().BeTrue();
    }
}
