using Credere.Core.Results;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain;
using Credere.Modules.Origination.Infrastructure;
using Credere.Modules.Origination.Infrastructure.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;
using static Credere.Modules.Origination.Tests.Domain.ApplicationSamples;
using static Credere.Modules.Origination.Tests.Infrastructure.PersistenceSamples;

namespace Credere.Modules.Origination.Tests.Infrastructure;

[Collection(SqlServerTestGroup.Name)]
[Trait("Requires", "Docker")]
public sealed class LoanApplicationRepositoryTests(SqlServerFixture fixture)
{
    private static readonly Instant WrittenAt = Instant.FromUtc(2026, 11, 27, 8, 15, 30);

    private readonly FakeClock _clock = new(WrittenAt);

    [Fact]
    public async Task Commit_NewApplication_StoresOneStateRowAndOneHistoryRow()
    {
        var id = NewId();
        var application = NewSubmitted(id);

        await SaveNewAsync(application);

        await using var db = fixture.CreateContext();
        var row = await db.LoanApplications.AsNoTracking().SingleAsync(candidate => candidate.Id == id);
        var history = await db.LoanApplicationEvents.AsNoTracking().Where(candidate => candidate.StreamId == id).ToListAsync();
        row.State.Should().Be(LoanApplicationState.Submitted);
        row.StreamVersion.Should().Be(1);
        row.RowVersion.Should().NotBeEmpty();
        history.Should().ContainSingle();
        history[0].StreamVersion.Should().Be(1);
        history[0].Type.Should().Be("ApplicationSubmitted");
        history[0].EventVersion.Should().Be(1);
        history[0].OccurredAt.Should().Be(SubmittedAt);
        history[0].RecordedAt.Should().Be(WrittenAt);
        history[0].CorrelationId.Should().Be(CorrelationId);
    }

    [Fact]
    public async Task Commit_AfterSuccess_ClearsTheUncommittedEvents()
    {
        var application = NewSubmitted(NewId());

        await SaveNewAsync(application);

        application.UncommittedEvents.Should().BeEmpty();
        application.Version.Should().Be(1);
    }

    [Fact]
    public async Task FindAsync_AfterTheFullLifecycle_RebuildsTheSameApplication()
    {
        var id = NewId();
        var application = NewSubmitted(id);
        await SaveNewAsync(application);
        await using (var unit = new UnitOfWork(fixture, _clock))
        {
            var loaded = (await unit.Repository.FindAsync(id, CancellationToken.None))!;
            Approve(loaded);
            Accept(loaded);
            unit.Repository.Update(loaded);
            (await unit.Repository.CommitAsync(id, CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        await using var reader = new UnitOfWork(fixture, _clock);
        var restored = await reader.Repository.FindAsync(id, CancellationToken.None);

        restored.Should().NotBeNull();
        restored!.State.Should().Be(LoanApplicationState.AcceptedByBorrower);
        restored.Version.Should().Be(3);
        restored.TermsVersion.Should().Be(1);
        restored.DecidedTerms.Should().Be(FinalTerms());
        restored.Applicant.Should().Be(application.Applicant);
        restored.Terms.Should().Be(application.Terms);
        restored.Decision!.Outcome.Should().Be(DecisionOutcome.Approve);
        restored.UncommittedEvents.Should().BeEmpty();
        await using var db = fixture.CreateContext();
        var history = await db.LoanApplicationEvents.AsNoTracking()
            .Where(candidate => candidate.StreamId == id)
            .OrderBy(candidate => candidate.StreamVersion)
            .ToListAsync();
        history.Select(row => row.StreamVersion).Should().Equal(1, 2, 3);
        history.Select(row => row.Type).Should().Equal("ApplicationSubmitted", "ApplicationApproved", "ApplicationAccepted");
    }

    [Fact]
    public async Task Commit_AmountsAndRateAtTheLimitsOfTheColumns_ComeBackUnchanged()
    {
        var id = NewId();
        var rate = 7.1234567890123456789012345678m;
        var application = NewSubmitted(id, at: SubmittedAt.PlusTicks(1234567), terms: new RequestedTerms(Eur(12_345_678_901_234.56m), RequestedTerms().Term));
        Approve(application, FinalTerms(principal: 98_765_432_109_876.54m, ratePercent: rate));
        await SaveNewAsync(application);

        await using var unit = new UnitOfWork(fixture, _clock);
        var view = await unit.Reader.GetAsync(id, CancellationToken.None);

        view!.RequestedTerms.Principal.Should().Be(Eur(12_345_678_901_234.56m));
        view.DecidedTerms!.Principal.Should().Be(Eur(98_765_432_109_876.54m));
        view.DecidedTerms.NominalRate.Value.Should().Be(rate);
        view.SubmittedAt.Should().Be(SubmittedAt.PlusTicks(1234567));
    }

    [Fact]
    public async Task Commit_StaleWriter_ReturnsConcurrencyConflictAndKeepsTheFirstWritersState()
    {
        var id = NewId();
        await SaveNewAsync(NewSubmitted(id));
        await using var first = new UnitOfWork(fixture, _clock);
        await using var second = new UnitOfWork(fixture, _clock);
        var byFirst = (await first.Repository.FindAsync(id, CancellationToken.None))!;
        var bySecond = (await second.Repository.FindAsync(id, CancellationToken.None))!;
        Approve(byFirst);
        Withdraw(bySecond);

        first.Repository.Update(byFirst);
        var firstResult = await first.Repository.CommitAsync(id, CancellationToken.None);
        second.Repository.Update(bySecond);
        var secondResult = await second.Repository.CommitAsync(id, CancellationToken.None);

        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsSuccess.Should().BeFalse();
        secondResult.Error.Should().BeOfType<ConcurrencyConflict>().Which.Id.Should().Be(id.Value);
        await using var db = fixture.CreateContext();
        (await db.LoanApplications.AsNoTracking().SingleAsync(candidate => candidate.Id == id)).State
            .Should().Be(LoanApplicationState.Approved);
        (await db.LoanApplicationEvents.CountAsync(candidate => candidate.StreamId == id)).Should().Be(2);
    }

    [Fact]
    public async Task Commit_StateHistoryOutboxAndReceipt_AreWrittenTogether()
    {
        var id = NewId();
        var application = NewSubmitted(id);
        var commandId = Guid.NewGuid();
        var integrationEvent = Submitted(id, application);
        await using (var unit = new UnitOfWork(fixture, _clock))
        {
            unit.Repository.Add(application);
            unit.Repository.Enqueue(integrationEvent);
            unit.Repository.AddReceipt(ReceiptHandler.SubmitApplication, commandId, "{\"Id\":\"stored\"}");

            (await unit.Repository.CommitAsync(id, CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        await using var db = fixture.CreateContext();
        (await db.LoanApplications.AnyAsync(candidate => candidate.Id == id)).Should().BeTrue();
        (await db.LoanApplicationEvents.CountAsync(candidate => candidate.StreamId == id)).Should().Be(1);
        var outbox = await db.Outbox.AsNoTracking().SingleAsync(candidate => candidate.Id == integrationEvent.EventId);
        outbox.Type.Should().Be("ApplicationSubmitted");
        outbox.EventVersion.Should().Be(1);
        outbox.OccurredAt.Should().Be(WrittenAt);
        outbox.DispatchedAt.Should().BeNull();
        outbox.AttemptCount.Should().Be(0);
        outbox.LastError.Should().BeNull();
        IntegrationEventSerializer.Deserialize(outbox.Type, outbox.EventVersion, outbox.PayloadJson).Should().Be(integrationEvent);
        var receipt = await db.Receipts(ReceiptHandler.SubmitApplication).AsNoTracking().SingleAsync(candidate => candidate.CommandId == commandId);
        receipt.OutcomeJson.Should().Be("{\"Id\":\"stored\"}");
        receipt.ReceivedAt.Should().Be(WrittenAt);
    }

    [Fact]
    public async Task Commit_ReceiptAlreadyStored_RollsBackStateHistoryAndOutboxToo()
    {
        var commandId = Guid.NewGuid();
        await using (var earlier = new UnitOfWork(fixture, _clock))
        {
            earlier.Repository.AddReceipt(ReceiptHandler.SubmitApplication, commandId, "{}");
            (await earlier.Repository.CommitAsync(NewId(), CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        var id = NewId();
        var application = NewSubmitted(id);
        var integrationEvent = Submitted(id, application);
        await using var duplicate = new UnitOfWork(fixture, _clock);
        duplicate.Repository.Add(application);
        duplicate.Repository.Enqueue(integrationEvent);
        duplicate.Repository.AddReceipt(ReceiptHandler.SubmitApplication, commandId, "{}");

        var result = await duplicate.Repository.CommitAsync(id, CancellationToken.None);

        result.Error.Should().BeOfType<ConcurrencyConflict>();
        await using var db = fixture.CreateContext();
        (await db.LoanApplications.AnyAsync(candidate => candidate.Id == id)).Should().BeFalse("the state row rolled back");
        (await db.LoanApplicationEvents.AnyAsync(candidate => candidate.StreamId == id)).Should().BeFalse("the history rolled back");
        (await db.Outbox.AnyAsync(candidate => candidate.Id == integrationEvent.EventId)).Should().BeFalse("the outbox rolled back");
        application.UncommittedEvents.Should().HaveCount(1, "a failed commit must not mark the events as stored");
    }

    [Fact]
    public async Task Commit_UpdateWithAnOutboxEventThatFails_LeavesTheStoredApplicationUntouched()
    {
        var id = NewId();
        await SaveNewAsync(NewSubmitted(id));
        var duplicatedEventId = Guid.NewGuid();
        await using (var earlier = new UnitOfWork(fixture, _clock))
        {
            earlier.Repository.Enqueue(Accepted(id, duplicatedEventId));
            (await earlier.Repository.CommitAsync(id, CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        await using var unit = new UnitOfWork(fixture, _clock);
        var loaded = (await unit.Repository.FindAsync(id, CancellationToken.None))!;
        Approve(loaded);
        unit.Repository.Update(loaded);
        unit.Repository.Enqueue(Accepted(id, duplicatedEventId));

        var result = await unit.Repository.CommitAsync(id, CancellationToken.None);

        result.Error.Should().BeOfType<ConcurrencyConflict>();
        await using var db = fixture.CreateContext();
        (await db.LoanApplications.AsNoTracking().SingleAsync(candidate => candidate.Id == id)).State
            .Should().Be(LoanApplicationState.Submitted);
        (await db.LoanApplicationEvents.CountAsync(candidate => candidate.StreamId == id)).Should().Be(1);
    }

    [Fact]
    public async Task Commit_SameApplicationIdTwice_SecondInsertIsAConflict()
    {
        var id = NewId();
        await SaveNewAsync(NewSubmitted(id));
        await using var unit = new UnitOfWork(fixture, _clock);
        unit.Repository.Add(NewSubmitted(id));

        var result = await unit.Repository.CommitAsync(id, CancellationToken.None);

        result.Error.Should().BeOfType<ConcurrencyConflict>();
    }

    [Fact]
    public async Task FindReceiptOutcome_NewCommand_IsNull()
    {
        await using var unit = new UnitOfWork(fixture, _clock);

        var outcome = await unit.Repository.FindReceiptOutcomeAsync(ReceiptHandler.DecideApplication, Guid.NewGuid(), CancellationToken.None);

        outcome.Should().BeNull();
    }

    [Fact]
    public async Task FindReceiptOutcome_HandledCommand_ReturnsTheStoredOutcomeOnlyForItsOwnHandler()
    {
        var commandId = Guid.NewGuid();
        await using (var unit = new UnitOfWork(fixture, _clock))
        {
            unit.Repository.AddReceipt(ReceiptHandler.DecideApplication, commandId, "{\"State\":\"Approved\"}");
            (await unit.Repository.CommitAsync(NewId(), CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        await using var retry = new UnitOfWork(fixture, _clock);

        (await retry.Repository.FindReceiptOutcomeAsync(ReceiptHandler.DecideApplication, commandId, CancellationToken.None))
            .Should().Be("{\"State\":\"Approved\"}");
        (await retry.Repository.FindReceiptOutcomeAsync(ReceiptHandler.AcceptTerms, commandId, CancellationToken.None))
            .Should().BeNull("every handler keeps its own receipts");
    }

    [Fact]
    public async Task FindAsync_UnknownId_ReturnsNull()
    {
        await using var unit = new UnitOfWork(fixture, _clock);

        var application = await unit.Repository.FindAsync(NewId(), CancellationToken.None);

        application.Should().BeNull();
    }

    [Fact]
    public async Task FindAsync_HistoryWithAGap_Throws()
    {
        var id = NewId();
        var application = NewSubmitted(id);
        Approve(application);
        await SaveNewAsync(application);
        await using (var db = fixture.CreateContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE origination.LoanApplicationEvents SET StreamVersion = 3 WHERE StreamId = {id.Value} AND StreamVersion = 2");
        }

        await using var unit = new UnitOfWork(fixture, _clock);
        var act = () => unit.Repository.FindAsync(id, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*history holds 1 events*");
    }

    [Fact]
    public async Task FindAsync_EventStoredAtAnotherVersion_Throws()
    {
        var id = NewId();
        await SaveNewAsync(NewSubmitted(id));
        await using (var db = fixture.CreateContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE origination.LoanApplicationEvents SET EventVersion = 2 WHERE StreamId = {id.Value}");
        }

        await using var unit = new UnitOfWork(fixture, _clock);
        var act = () => unit.Repository.FindAsync(id, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no upcaster*");
    }

    [Fact]
    public async Task Update_ApplicationNotLoadedInThisUnitOfWork_Throws()
    {
        var id = NewId();
        var application = NewSubmitted(id);
        await SaveNewAsync(application);
        Approve(application);
        await using var unit = new UnitOfWork(fixture, _clock);

        var act = () => unit.Repository.Update(application);

        act.Should().Throw<InvalidOperationException>().WithMessage("*was not loaded in this unit of work*");
    }

    [Fact]
    public async Task Add_ApplicationThatAlreadyHasCommittedHistory_Throws()
    {
        var application = NewSubmitted(NewId());
        await SaveNewAsync(application);
        Approve(application);
        await using var unit = new UnitOfWork(fixture, _clock);

        var act = () => unit.Repository.Add(application);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Update_NothingChanged_WritesNothingAndKeepsTheRowVersion()
    {
        var id = NewId();
        await SaveNewAsync(NewSubmitted(id));
        await using var unit = new UnitOfWork(fixture, _clock);
        var loaded = (await unit.Repository.FindAsync(id, CancellationToken.None))!;
        var before = unit.Db.LoanApplications.Local.Single().RowVersion.ToArray();

        unit.Repository.Update(loaded);
        (await unit.Repository.CommitAsync(id, CancellationToken.None)).IsSuccess.Should().BeTrue();

        await using var db = fixture.CreateContext();
        (await db.LoanApplications.AsNoTracking().SingleAsync(candidate => candidate.Id == id)).RowVersion.Should().Equal(before);
    }

    [Fact]
    public async Task Receipts_UnknownHandler_Throws()
    {
        await using var unit = new UnitOfWork(fixture, _clock);

        var act = () => unit.Repository.AddReceipt("NoSuchHandler", Guid.NewGuid(), "{}");

        act.Should().Throw<ArgumentException>();
    }

    private async Task SaveNewAsync(LoanApplication application)
    {
        await using var unit = new UnitOfWork(fixture, _clock);
        unit.Repository.Add(application);
        var result = await unit.Repository.CommitAsync(application.Id, CancellationToken.None);
        result.IsSuccess.Should().BeTrue("the test setup must store the application");
    }

    private static IntegrationEvents.ApplicationSubmitted Submitted(LoanApplicationId id, LoanApplication application) =>
        new(Guid.NewGuid(), id, application.Applicant, application.Product, application.Terms, SubmittedAt, CorrelationId);

    private static IntegrationEvents.ApplicationAccepted Accepted(LoanApplicationId id, Guid eventId) =>
        new(eventId, id, Product(), 1, FinalTerms(), Later, CorrelationId);
}
