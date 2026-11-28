using Credere.Core.Results;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain.Events;
using Credere.Modules.Servicing.Infrastructure;
using Credere.Modules.Servicing.Infrastructure.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;
using static Credere.Modules.Servicing.Tests.Domain.Loans.LoanSamples;
using static Credere.Modules.Servicing.Tests.Infrastructure.PersistenceSamples;
using Published = Credere.Modules.Servicing.IntegrationEvents;

namespace Credere.Modules.Servicing.Tests.Infrastructure;

[Collection(SqlServerTestGroup.Name)]
[Trait("Requires", "Docker")]
public sealed class ServicingRepositoryTests
{
    private static readonly Instant WrittenAt = Instant.FromUtc(2026, 11, 27, 8, 15, 30);

    private readonly SqlServerFixture _fixture;
    private readonly FakeClock _clock = new(WrittenAt);
    private readonly ScenarioDriver _driver;

    public ServicingRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _driver = new ScenarioDriver(fixture, _clock);
    }

    public static TheoryData<string> Handlers()
    {
        var data = new TheoryData<string>();
        foreach (var handler in ServicingSchema.ReceiptHandlers)
        {
            data.Add(handler);
        }

        return data;
    }

    [Fact]
    public async Task Disburse_StoresTheStateRowTheHistoryTheScheduleAndMarksTheIntent()
    {
        var (loanId, intentId, product) = await _driver.DisburseAsync();

        await using var db = _fixture.CreateContext();
        var row = await db.Loans.AsNoTracking().SingleAsync(candidate => candidate.Id == loanId);
        var history = await db.LoanEvents.AsNoTracking().Where(candidate => candidate.StreamId == loanId).ToListAsync();
        var intent = await db.DisbursementIntents.AsNoTracking().SingleAsync(candidate => candidate.Id == intentId);
        row.State.Should().Be(LoanState.Disbursed);
        row.StreamVersion.Should().Be(1);
        row.RowVersion.Should().NotBeEmpty();
        row.DisbursementIntentId.Should().Be(intentId);
        row.Product.Should().Be(product);
        history.Should().ContainSingle();
        history[0].Type.Should().Be("LoanDisbursed");
        history[0].EventVersion.Should().Be(1);
        history[0].OccurredAt.Should().Be(DisbursedOn);
        history[0].RecordedAt.Should().Be(WrittenAt);
        (await db.LoanSchedules.CountAsync(candidate => candidate.LoanId == loanId)).Should().Be(Schedule.Rows.Count);
        intent.State.Should().Be(DisbursementIntentState.Disbursed);
        intent.LoanId.Should().Be(loanId);
    }

    [Fact]
    public async Task FindLoanAsync_AfterAccrualAPaymentAndAnotherAccrual_RebuildsTheSameLoanAndWritesBothLedgers()
    {
        // IA-14 of the approved M2-03 vectors: from the state at 2026-02-16 (past-due 41.67, current 1.39, 43.06 posted),
        // a payment of 41.67 + 1.39 + 1000.00 starts a new segment, and accrual to 2026-02-20 posts exactly 5.00.
        var (loanId, _, _) = await _driver.DisburseAsync();
        var paymentId = new PaymentId(Guid.NewGuid());
        await _driver.ChangeLoanAsync(loanId, loan => Succeeded(loan.Activate(ActivateOn(loanId), new Calendar())));
        await _driver.ChangeLoanAsync(
            loanId, loan => Succeeded(loan.AccrueInterest(AccrueOn(loanId, Date("2026-02-16")), Calculator)));
        await _driver.ChangeLoanAsync(
            loanId, loan => Succeeded(loan.RecordPayment(PayOn(loanId, Euro(1043.06m), Date("2026-02-16")), paymentId, Calculator)));
        await _driver.ChangeLoanAsync(
            loanId, loan => Succeeded(loan.AccrueInterest(AccrueOn(loanId, Date("2026-02-20")), Calculator)));

        await using var unit = _driver.Unit();
        var restored = (await unit.Repository.FindLoanAsync(loanId, CancellationToken.None))!;

        restored.State.Should().Be(LoanState.Active);
        restored.Version.Should().Be(5);
        restored.RemainingPrincipal.Should().Be(Euro(9000m));
        restored.CurrentInterest.Should().Be(Euro(5.00m));
        restored.PastDueInterest.Should().Be(Euro(0m));
        restored.AccruedThroughDate.Should().Be(Date("2026-02-20"));
        restored.UncommittedEvents.Should().BeEmpty();
        var history = await unit.Db.LoanEvents.AsNoTracking()
            .Where(candidate => candidate.StreamId == loanId)
            .OrderBy(candidate => candidate.StreamVersion)
            .ToListAsync();
        history.Select(row => row.Type).Should().Equal(
            "LoanDisbursed", "LoanActivated", "InterestAccrued", "PaymentReceived", "InterestAccrued");
        var accruals = await unit.Db.AccrualLedger.AsNoTracking()
            .Where(row => row.LoanId == loanId)
            .OrderBy(row => row.BusinessDate)
            .ToListAsync();
        accruals.Select(row => (row.BusinessDate, row.PeriodStart, row.Amount)).Should().Equal(
            (Date("2026-02-16"), DisbursedOn, 43.06m),
            (Date("2026-02-20"), Date("2026-02-16"), 5.00m));
        var payment = await unit.Db.PaymentLedger.AsNoTracking().SingleAsync(row => row.PaymentId == paymentId);
        payment.LoanId.Should().Be(loanId);
        payment.EffectiveDate.Should().Be(Date("2026-02-16"));
        payment.Amount.Should().Be(1043.06m);
        payment.PastDueInterestAllocated.Should().Be(41.67m);
        payment.CurrentInterestAllocated.Should().Be(1.39m);
        payment.PrincipalAllocated.Should().Be(1000m);
        payment.FeesAllocated.Should().Be(0m);
        payment.Excess.Should().Be(0m);

        // The ledger invariant of docs/modules/servicing.md: what was accrued is what is still owed plus what payments
        // took as interest. The sums are taken here because the columns are exact text.
        var interestPaid = payment.PastDueInterestAllocated + payment.CurrentInterestAllocated;
        accruals.Sum(row => row.Amount).Should().Be(restored.CurrentInterest.Amount + restored.PastDueInterest.Amount + interestPaid);
    }

    [Fact]
    public async Task AccrueInterest_SameDateAgainInANewScope_IsANoOpAndAddsNoLedgerRow()
    {
        var (loanId, _, _) = await _driver.DisburseAsync();
        await _driver.ChangeLoanAsync(loanId, loan => Succeeded(loan.Activate(ActivateOn(loanId), new Calendar())));
        await _driver.ChangeLoanAsync(
            loanId, loan => Succeeded(loan.AccrueInterest(AccrueOn(loanId, Date("2026-02-16")), Calculator)));
        var changed = true;
        await _driver.ChangeLoanAsync(
            loanId, loan => changed = Succeeded(loan.AccrueInterest(AccrueOn(loanId, Date("2026-02-16")), Calculator)).Changed);

        await using var db = _fixture.CreateContext();
        changed.Should().BeFalse();
        (await db.AccrualLedger.CountAsync(row => row.LoanId == loanId)).Should().Be(1);
        (await db.LoanEvents.CountAsync(row => row.StreamId == loanId)).Should().Be(3);
    }

    [Fact]
    public async Task Commit_StaleWriter_ReturnsConcurrencyConflictAndKeepsTheFirstWritersState()
    {
        var (loanId, _, _) = await _driver.DisburseAsync();
        await using var first = _driver.Unit();
        await using var second = _driver.Unit();
        var byFirst = (await first.Repository.FindLoanAsync(loanId, CancellationToken.None))!;
        var bySecond = (await second.Repository.FindLoanAsync(loanId, CancellationToken.None))!;
        Succeeded(byFirst.Activate(ActivateOn(loanId), new Calendar()));
        Succeeded(bySecond.Activate(ActivateOn(loanId), new Calendar()));

        first.Repository.UpdateLoan(byFirst);
        var firstResult = await first.Repository.CommitAsync("Loan", loanId.Value, CancellationToken.None);
        second.Repository.UpdateLoan(bySecond);
        var secondResult = await second.Repository.CommitAsync("Loan", loanId.Value, CancellationToken.None);

        firstResult.IsSuccess.Should().BeTrue();
        secondResult.Error.Should().BeOfType<ConcurrencyConflict>().Which.Id.Should().Be(loanId.Value);
        await using var db = _fixture.CreateContext();
        (await db.Loans.AsNoTracking().SingleAsync(row => row.Id == loanId)).State.Should().Be(LoanState.Active);
        (await db.LoanEvents.CountAsync(row => row.StreamId == loanId)).Should().Be(2);
    }

    [Fact]
    public async Task Commit_ReceiptAlreadyStored_RollsBackTheStateAndHistoryToo()
    {
        var (loanId, _, _) = await _driver.DisburseAsync();
        var commandId = Guid.NewGuid();
        await using (var earlier = _driver.Unit())
        {
            earlier.Repository.AddReceipt(ReceiptHandler.Activate, commandId, "{}");
            (await earlier.Repository.CommitAsync("Loan", loanId.Value, CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        await using var duplicate = _driver.Unit();
        var loan = (await duplicate.Repository.FindLoanAsync(loanId, CancellationToken.None))!;
        Succeeded(loan.Activate(ActivateOn(loanId), new Calendar()));
        duplicate.Repository.UpdateLoan(loan);
        duplicate.Repository.AddReceipt(ReceiptHandler.Activate, commandId, "{}");

        var result = await duplicate.Repository.CommitAsync("Loan", loanId.Value, CancellationToken.None);

        result.Error.Should().BeOfType<ConcurrencyConflict>();
        await using var db = _fixture.CreateContext();
        (await db.Loans.AsNoTracking().SingleAsync(row => row.Id == loanId)).State.Should().Be(LoanState.Disbursed);
        (await db.LoanEvents.CountAsync(row => row.StreamId == loanId)).Should().Be(1);
        loan.UncommittedEvents.Should().HaveCount(1, "a failed commit must not mark the events as stored");
    }

    [Fact]
    public async Task Commit_DisburseWithAReceiptThatAlreadyExists_RollsBackTheLoanTheScheduleAndTheIntent()
    {
        var intent = await _driver.RegisterIntentAsync();
        var commandId = Guid.NewGuid();
        await using (var earlier = _driver.Unit())
        {
            earlier.Repository.AddReceipt(ReceiptHandler.Disburse, commandId, "{}");
            (await earlier.Repository.CommitAsync("DisbursementIntent", intent.Id.Value, CancellationToken.None)).IsSuccess
                .Should().BeTrue();
        }

        var loanId = NewLoanId();
        await using var unit = _driver.Unit();
        var loaded = (await unit.Repository.FindIntentAsync(intent.Id, CancellationToken.None))!;
        var loan = NewLoan(loanId, loaded);
        Succeeded(loaded.MarkDisbursed(loanId));
        unit.Repository.AddLoan(loan);
        unit.Repository.UpdateIntent(loaded);
        unit.Repository.AddReceipt(ReceiptHandler.Disburse, commandId, "{}");

        var result = await unit.Repository.CommitAsync("Loan", loanId.Value, CancellationToken.None);

        result.Error.Should().BeOfType<ConcurrencyConflict>();
        await using var db = _fixture.CreateContext();
        (await db.DisbursementIntents.AsNoTracking().SingleAsync(row => row.Id == intent.Id)).State
            .Should().Be(DisbursementIntentState.Pending, "the intent is confirmed in the transaction of the loan, or not at all");
        (await db.Loans.AnyAsync(row => row.Id == loanId)).Should().BeFalse();
        (await db.LoanEvents.AnyAsync(row => row.StreamId == loanId)).Should().BeFalse();
        (await db.LoanSchedules.AnyAsync(row => row.LoanId == loanId)).Should().BeFalse();
    }

    [Fact]
    public async Task AccrualLedger_SecondRowForTheSameLoanAndDate_IsRefusedByTheDatabase()
    {
        var (loanId, _, _) = await _driver.DisburseAsync();
        await _driver.ChangeLoanAsync(loanId, loan => Succeeded(loan.Activate(ActivateOn(loanId), new Calendar())));
        await _driver.ChangeLoanAsync(
            loanId, loan => Succeeded(loan.AccrueInterest(AccrueOn(loanId, Date("2026-02-16")), Calculator)));
        await using var db = _fixture.CreateContext();
        db.AccrualLedger.Add(new AccrualLedgerRow
        {
            LoanId = loanId,
            BusinessDate = Date("2026-02-16"),
            PeriodStart = DisbursedOn,
            Currency = "EUR",
            Amount = 1m,
            CommandId = Guid.NewGuid(),
            RecordedAt = WrittenAt,
        });

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task PaymentLedger_SecondRowForTheSamePaymentId_IsRefusedByTheDatabase()
    {
        var (loanId, _, _) = await _driver.DisburseAsync();
        await _driver.ChangeLoanAsync(loanId, loan => Succeeded(loan.Activate(ActivateOn(loanId), new Calendar())));
        var paymentId = new PaymentId(Guid.NewGuid());
        await _driver.ChangeLoanAsync(
            loanId, loan => Succeeded(loan.RecordPayment(PayOn(loanId, Euro(10m), ActivatedOn), paymentId, Calculator)));
        await using var db = _fixture.CreateContext();
        db.PaymentLedger.Add(new PaymentLedgerRow
        {
            PaymentId = paymentId,
            LoanId = loanId,
            EffectiveDate = ActivatedOn,
            Currency = "EUR",
            Amount = 1m,
            CommandId = Guid.NewGuid(),
            RecordedAt = WrittenAt,
        });

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task AddIntent_SameSourceAndTermsVersionTwice_SecondIsAConflict()
    {
        var first = await _driver.RegisterIntentAsync(NewIntent(termsVersion: 2));
        await using var unit = _driver.Unit();
        unit.Repository.AddIntent(NewIntent(sourceReference: first.SourceReference, termsVersion: 2));

        var result = await unit.Repository.CommitAsync("DisbursementIntent", first.Id.Value, CancellationToken.None);

        result.Error.Should().BeOfType<ConcurrencyConflict>();
    }

    [Fact]
    public async Task AddIntent_SameSourceWithAnotherTermsVersion_IsAnotherIntent()
    {
        var first = await _driver.RegisterIntentAsync(NewIntent(termsVersion: 1));

        var second = await _driver.RegisterIntentAsync(NewIntent(sourceReference: first.SourceReference, termsVersion: 2));

        second.Id.Should().NotBe(first.Id);
    }

    [Fact]
    public async Task FindIntentBySourceAsync_StoredKey_ReturnsTheIntentAndAnUnknownKeyReturnsNull()
    {
        var stored = await _driver.RegisterIntentAsync(NewIntent(termsVersion: 4));
        await using var unit = _driver.Unit();

        var found = await unit.Repository.FindIntentBySourceAsync(stored.SourceReference, 4, CancellationToken.None);
        var other = await unit.Repository.FindIntentBySourceAsync(stored.SourceReference, 5, CancellationToken.None);

        found!.Id.Should().Be(stored.Id);
        found.Terms.Should().Be(stored.Terms);
        found.State.Should().Be(DisbursementIntentState.Pending);
        other.Should().BeNull();
    }

    [Fact]
    public async Task AddLoan_SecondLoanFromTheSameIntent_IsRefusedByTheUniqueIntentOnTheLoan()
    {
        var (_, intentId, _) = await _driver.DisburseAsync();
        var otherLoanId = NewLoanId();
        await using var unit = _driver.Unit();
        unit.Repository.AddLoan(NewLoan(otherLoanId, NewIntent(id: intentId)));

        var result = await unit.Repository.CommitAsync("Loan", otherLoanId.Value, CancellationToken.None);

        result.Error.Should().BeOfType<ConcurrencyConflict>();
    }

    [Fact]
    public async Task RegisterIntent_AcceptedTermsAtTheLimits_ComeBackUnchanged()
    {
        var terms = new AcceptedLoanTerms(
            Euro(decimal.MaxValue), new Percentage(99.99999999999999999999999999m), LoanTerm.FromMonths(600));
        var stored = await _driver.RegisterIntentAsync(NewIntent(terms: terms));

        await using var unit = _driver.Unit();
        var view = await unit.Reader.GetIntentAsync(stored.Id, CancellationToken.None);

        view!.Terms.Should().Be(terms);
    }

    [Theory]
    [MemberData(nameof(Handlers))]
    public async Task Receipts_EveryHandlerKeepsItsOwn(string handler)
    {
        var commandId = Guid.NewGuid();
        await using (var unit = _driver.Unit())
        {
            unit.Repository.AddReceipt(handler, commandId, "{\"stored\":true}");
            (await unit.Repository.CommitAsync("Loan", Guid.NewGuid(), CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        await using var retry = _driver.Unit();
        (await retry.Repository.FindReceiptOutcomeAsync(handler, commandId, CancellationToken.None))
            .Should().Be("{\"stored\":true}");
        foreach (var other in ServicingSchema.ReceiptHandlers.Where(candidate => candidate != handler))
        {
            (await retry.Repository.FindReceiptOutcomeAsync(other, commandId, CancellationToken.None))
                .Should().BeNull("{0} must not see the receipt of {1}", other, handler);
        }
    }

    [Fact]
    public async Task Enqueue_IntegrationEvent_IsStoredPendingAndComesBackEqual()
    {
        var (loanId, _, _) = await _driver.DisburseAsync();
        var published = new Published.LoanActivated(Guid.NewGuid(), loanId, ActivatedOn, CorrelationId);
        await using (var unit = _driver.Unit())
        {
            unit.Repository.Enqueue(published);
            (await unit.Repository.CommitAsync("Loan", loanId.Value, CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        await using var db = _fixture.CreateContext();
        var outbox = await db.Outbox.AsNoTracking().SingleAsync(row => row.Id == published.EventId);
        outbox.Type.Should().Be("LoanActivated");
        outbox.EventVersion.Should().Be(1);
        outbox.OccurredAt.Should().Be(WrittenAt);
        outbox.DispatchedAt.Should().BeNull();
        outbox.AttemptCount.Should().Be(0);
        IntegrationEventSerializer.Deserialize(outbox.Type, outbox.EventVersion, outbox.PayloadJson).Should().Be(published);
    }

    [Fact]
    public async Task FindLoanAsync_UnknownId_ReturnsNull()
    {
        await using var unit = _driver.Unit();

        (await unit.Repository.FindLoanAsync(NewLoanId(), CancellationToken.None)).Should().BeNull();
        (await unit.Repository.FindIntentAsync(NewIntentId(), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task FindLoanAsync_HistoryWithAGap_Throws()
    {
        var (loanId, _, _) = await _driver.DisburseAsync();
        await _driver.ChangeLoanAsync(loanId, loan => Succeeded(loan.Activate(ActivateOn(loanId), new Calendar())));
        await using (var db = _fixture.CreateContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE servicing.LoanEvents SET StreamVersion = 3 WHERE StreamId = {loanId.Value} AND StreamVersion = 2");
        }

        await using var unit = _driver.Unit();
        var act = () => unit.Repository.FindLoanAsync(loanId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*history holds 1 events*");
    }

    [Fact]
    public async Task FindLoanAsync_EventStoredAtAnotherVersion_Throws()
    {
        var (loanId, _, _) = await _driver.DisburseAsync();
        await using (var db = _fixture.CreateContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE servicing.LoanEvents SET EventVersion = 2 WHERE StreamId = {loanId.Value}");
        }

        await using var unit = _driver.Unit();
        var act = () => unit.Repository.FindLoanAsync(loanId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no upcaster*");
    }

    [Fact]
    public async Task FindLoanAsync_PayloadNamingAnotherLoan_Throws()
    {
        var (loanId, _, _) = await _driver.DisburseAsync();
        var other = NewLoanId();
        await using (var db = _fixture.CreateContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE servicing.LoanEvents SET PayloadJson = REPLACE(PayloadJson, {loanId.Value.ToString()}, {other.Value.ToString()}) WHERE StreamId = {loanId.Value}");
        }

        await using var unit = _driver.Unit();
        var act = () => unit.Repository.FindLoanAsync(loanId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*holds LoanDisbursed for loan {other.Value}*");
    }

    [Fact]
    public async Task UpdateLoan_LoanNotLoadedInThisUnitOfWork_Throws()
    {
        var (loanId, _, _) = await _driver.DisburseAsync();
        var loan = NewLoan(loanId, NewIntent());
        await using var unit = _driver.Unit();

        var act = () => unit.Repository.UpdateLoan(loan);

        act.Should().Throw<InvalidOperationException>().WithMessage("*was not loaded in this unit of work*");
    }

    [Fact]
    public async Task AddLoan_LoanThatAlreadyHasCommittedHistory_Throws()
    {
        var committed = (await _driver.DisburseAsync()).LoanId;
        await using var unit = _driver.Unit();
        var loaded = (await unit.Repository.FindLoanAsync(committed, CancellationToken.None))!;
        Succeeded(loaded.Activate(ActivateOn(committed), new Calendar()));

        var act = () => unit.Repository.AddLoan(loaded);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no committed history*");
    }

    [Fact]
    public async Task UpdateLoan_NothingChanged_WritesNothingAndKeepsTheRowVersion()
    {
        var (loanId, _, _) = await _driver.DisburseAsync();
        await using var unit = _driver.Unit();
        var loaded = (await unit.Repository.FindLoanAsync(loanId, CancellationToken.None))!;
        var before = unit.Db.Loans.Local.Single().RowVersion.ToArray();

        unit.Repository.UpdateLoan(loaded);
        (await unit.Repository.CommitAsync("Loan", loanId.Value, CancellationToken.None)).IsSuccess.Should().BeTrue();

        await using var db = _fixture.CreateContext();
        (await db.Loans.AsNoTracking().SingleAsync(row => row.Id == loanId)).RowVersion.Should().Equal(before);
    }

    [Fact]
    public async Task Receipts_UnknownHandler_Throws()
    {
        await using var unit = _driver.Unit();

        var act = () => unit.Repository.AddReceipt("NoSuchHandler", Guid.NewGuid(), "{}");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task FindLoanAsync_WrittenOffLoan_KeepsTheBalanceComponentsOfTheWriteOff()
    {
        var (loanId, _, _) = await _driver.DisburseAsync();
        await _driver.ChangeLoanAsync(loanId, loan => Succeeded(loan.Activate(ActivateOn(loanId), new Calendar())));
        await _driver.ChangeLoanAsync(loanId, loan => Succeeded(loan.ApplyAging(AgeOn(loanId, DefaultThreshold))));
        await _driver.ChangeLoanAsync(
            loanId, loan => Succeeded(loan.WriteOff(WriteOffOn(loanId, FirstDueDate.PlusDays(1)), Calculator)));

        await using var unit = _driver.Unit();
        var restored = (await unit.Repository.FindLoanAsync(loanId, CancellationToken.None))!;

        restored.State.Should().Be(LoanState.WrittenOff);
        var history = await unit.Db.LoanEvents.AsNoTracking()
            .Where(row => row.StreamId == loanId && row.Type == "LoanWrittenOff")
            .SingleAsync();
        var writeOff = (LoanWrittenOff)LoanEventSerializer.Deserialize(history.Type, history.EventVersion, history.PayloadJson);
        writeOff.RemainingPrincipal.Should().Be(Principal);
        writeOff.PastDueInterest.Should().Be(Euro(41.67m));
        writeOff.CurrentInterest.Should().Be(Euro(1.39m));
        writeOff.WrittenOffBalance.Should().Be(Euro(10043.06m));
    }
}
