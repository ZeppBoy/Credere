using Credere.Core.Results;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain;
using Credere.Modules.Servicing.Domain.Events;
using Credere.Modules.Servicing.Infrastructure.Serialization;
using Credere.Shared.IntegrationEvents;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Credere.Modules.Servicing.Infrastructure;

/// <summary>
/// The persistence operations of the Servicing facade: load a loan or an intent, stage new state, history, ledger rows,
/// outbox events and command receipts, then commit them all in one transaction (ADR-0004, ADR-0005, ADR-0007).
/// </summary>
/// <remarks>
/// <para>
/// One instance serves one command, over one scoped <see cref="ServicingDbContext"/>. After a failed
/// <see cref="CommitAsync"/> the context holds stale rows, so a retry uses a new scope.
/// </para>
/// <para>
/// A loan is rebuilt from its history, because the aggregate has no other way in. The state row is read first, and
/// only the history up to its stream version is replayed, so a writer that commits in between cannot slip an unseen
/// event into the aggregate. The tracked state row then carries its <c>RowVersion</c> into the update, which fails with
/// a <see cref="ConcurrencyConflict"/> if anyone else saved first.
/// </para>
/// <para>
/// The two ledgers and the schedule snapshot are written from the events a command raised, in the same transaction.
/// The ledger keys make a date or a payment unique, so a repeated delivery cannot post twice.
/// </para>
/// </remarks>
internal sealed class ServicingRepository
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private readonly ServicingDbContext _db;
    private readonly IClock _clock;
    private readonly List<Loan> _staged = [];

    /// <summary>Initializes a new instance of the <see cref="ServicingRepository"/> class.</summary>
    /// <param name="db">The unit of work.</param>
    /// <param name="clock">The clock that stamps rows with when they were written.</param>
    public ServicingRepository(ServicingDbContext db, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    /// <summary>Loads a loan by replaying its history.</summary>
    /// <param name="id">The loan id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The loan with no uncommitted events, or <see langword="null"/> when none has that id.</returns>
    /// <exception cref="InvalidOperationException">The stored history is incomplete or inconsistent with the state row.</exception>
    public async Task<Loan?> FindLoanAsync(LoanId id, CancellationToken cancellationToken)
    {
        var row = await _db.Loans
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var history = await _db.LoanEvents
            .AsNoTracking()
            .Where(candidate => candidate.StreamId == id && candidate.StreamVersion <= row.StreamVersion)
            .OrderBy(candidate => candidate.StreamVersion)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        for (var index = 0; index < history.Count; index++)
        {
            if (history[index].StreamVersion != index + 1)
            {
                throw new InvalidOperationException(
                    $"The history of loan {id.Value} has a gap: expected stream version {index + 1}, found {history[index].StreamVersion}.");
            }
        }

        if (history.Count != row.StreamVersion)
        {
            throw new InvalidOperationException(
                $"The state row of loan {id.Value} is at stream version {row.StreamVersion}, but its history holds {history.Count} events.");
        }

        var events = history
            .Select(item => LoanEventSerializer.Deserialize(item.Type, item.EventVersion, item.PayloadJson))
            .ToList();

        // The aggregate takes its id from the first event, so a payload that names another loan would load as that
        // loan. Every payload must name the stream it is stored in.
        var foreign = events.FirstOrDefault(@event => @event.Context.LoanId != id);
        if (foreign is not null)
        {
            throw new InvalidOperationException(
                $"The history of loan {id.Value} holds {foreign.GetType().Name} for loan {foreign.Context.LoanId.Value}.");
        }

        var loan = Loan.Rehydrate(events);
        if (loan.Version != row.StreamVersion)
        {
            throw new InvalidOperationException(
                $"Loan {id.Value} replayed to version {loan.Version}, but its state row is at {row.StreamVersion}.");
        }

        return loan;
    }

    /// <summary>
    /// Stages a newly disbursed loan: its state row, one history row for each uncommitted event, the schedule snapshot
    /// and any ledger rows those events imply.
    /// </summary>
    /// <param name="loan">The new loan, as <c>Disburse</c> returned it.</param>
    /// <exception cref="InvalidOperationException">The loan was not just created.</exception>
    public void AddLoan(Loan loan)
    {
        ArgumentNullException.ThrowIfNull(loan);

        if (loan.Version != loan.UncommittedEvents.Count)
        {
            throw new InvalidOperationException("Only a loan with no committed history can be added.");
        }

        var row = new LoanRow();
        ServicingRowMapper.Apply(row, loan);
        _db.Loans.Add(row);
        StageEvents(loan);
    }

    /// <summary>
    /// Stages the changes a command made to a loan that <see cref="FindLoanAsync"/> loaded in this unit of work.
    /// </summary>
    /// <param name="loan">The loaded loan, after its command method ran.</param>
    /// <exception cref="InvalidOperationException">The loan was not loaded here, or is not the one that was.</exception>
    public void UpdateLoan(Loan loan)
    {
        ArgumentNullException.ThrowIfNull(loan);

        var row = _db.Loans.Local.SingleOrDefault(candidate => candidate.Id == loan.Id)
            ?? throw new InvalidOperationException(
                $"Loan {loan.Id.Value} was not loaded in this unit of work, so its concurrency token is unknown.");
        if (row.StreamVersion + loan.UncommittedEvents.Count != loan.Version)
        {
            throw new InvalidOperationException(
                $"Loan {loan.Id.Value} is at version {loan.Version} with {loan.UncommittedEvents.Count} new events, which does not follow the loaded stream version {row.StreamVersion}.");
        }

        if (loan.UncommittedEvents.Count == 0)
        {
            return;
        }

        ServicingRowMapper.Apply(row, loan);
        StageEvents(loan);
    }

    /// <summary>Loads a disbursement intent by id, tracked so <see cref="UpdateIntent"/> keeps its concurrency token.</summary>
    /// <param name="id">The intent id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The intent, or <see langword="null"/> when none has that id.</returns>
    public async Task<DisbursementIntent?> FindIntentAsync(DisbursementIntentId id, CancellationToken cancellationToken)
    {
        var row = await _db.DisbursementIntents
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ServicingRowMapper.ToIntent(row);
    }

    /// <summary>Loads the intent for one accepted application and terms version.</summary>
    /// <param name="sourceReference">The opaque reference of the accepted application.</param>
    /// <param name="acceptedTermsVersion">The accepted terms version.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The intent, or <see langword="null"/> when none is stored for that key.</returns>
    public async Task<DisbursementIntent?> FindIntentBySourceAsync(
        Guid sourceReference,
        int acceptedTermsVersion,
        CancellationToken cancellationToken)
    {
        var row = await _db.DisbursementIntents
            .SingleOrDefaultAsync(
                candidate => candidate.SourceReference == sourceReference && candidate.AcceptedTermsVersion == acceptedTermsVersion,
                cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ServicingRowMapper.ToIntent(row);
    }

    /// <summary>Stages a new, pending intent. The unique key on the source and terms version guards a race.</summary>
    /// <param name="intent">The new intent.</param>
    public void AddIntent(DisbursementIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var row = new DisbursementIntentRow { RegisteredAt = _clock.GetCurrentInstant() };
        ServicingRowMapper.Apply(row, intent);
        _db.DisbursementIntents.Add(row);
    }

    /// <summary>Stages the changes made to an intent that <see cref="FindIntentAsync"/> loaded in this unit of work.</summary>
    /// <param name="intent">The loaded intent, after it was marked disbursed.</param>
    /// <exception cref="InvalidOperationException">The intent was not loaded here.</exception>
    public void UpdateIntent(DisbursementIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var row = _db.DisbursementIntents.Local.SingleOrDefault(candidate => candidate.Id == intent.Id)
            ?? throw new InvalidOperationException(
                $"Intent {intent.Id.Value} was not loaded in this unit of work, so its concurrency token is unknown.");
        ServicingRowMapper.Apply(row, intent);
    }

    /// <summary>Stages an integration event in the outbox, to be committed with the state change.</summary>
    /// <param name="integrationEvent">The event. Its <c>EventId</c> becomes the outbox row id.</param>
    public void Enqueue(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var (type, version, json) = IntegrationEventSerializer.Serialize(integrationEvent);
        _db.Outbox.Add(new OutboxMessage
        {
            Id = integrationEvent.EventId,
            OccurredAt = _clock.GetCurrentInstant(),
            Type = type,
            EventVersion = version,
            PayloadJson = json,
        });
    }

    /// <summary>Finds the outcome a handler stored for a command it has already handled.</summary>
    /// <param name="handler">The handler, one of <see cref="ServicingSchema.ReceiptHandlers"/>.</param>
    /// <param name="commandId">The command id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored outcome, or <see langword="null"/> when the command is new.</returns>
    public Task<string?> FindReceiptOutcomeAsync(string handler, Guid commandId, CancellationToken cancellationToken) =>
        _db.Receipts(handler)
            .AsNoTracking()
            .Where(receipt => receipt.CommandId == commandId)
            .Select(receipt => receipt.OutcomeJson)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Stages the receipt of a handled command, to be committed with its state change.</summary>
    /// <param name="handler">The handler, one of <see cref="ServicingSchema.ReceiptHandlers"/>.</param>
    /// <param name="commandId">The command id.</param>
    /// <param name="outcomeJson">The serialized outcome the handler returns for this command.</param>
    public void AddReceipt(string handler, Guid commandId, string outcomeJson)
    {
        ArgumentNullException.ThrowIfNull(outcomeJson);

        _db.Receipts(handler).Add(new CommandReceipt
        {
            CommandId = commandId,
            ReceivedAt = _clock.GetCurrentInstant(),
            OutcomeJson = outcomeJson,
        });
    }

    /// <summary>
    /// Writes everything staged in one transaction: state rows, history rows, ledgers, outbox rows and receipts, or
    /// nothing.
    /// </summary>
    /// <param name="aggregate">The aggregate the command was for, named in a conflict.</param>
    /// <param name="id">The id of that aggregate, named in a conflict.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// Success, or a <see cref="ConcurrencyConflict"/> when another writer saved the aggregate first, the same command
    /// or payment or accrual date was handled at the same time, or the intent key is already taken. The caller retries
    /// in a new scope: a repeated command then finds its receipt.
    /// </returns>
    public async Task<Result<Unit, DomainError>> CommitAsync(string aggregate, Guid id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregate);

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<Unit, DomainError>.Failure(new ConcurrencyConflict(aggregate, id));
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Result<Unit, DomainError>.Failure(new ConcurrencyConflict(aggregate, id));
        }

        foreach (var loan in _staged)
        {
            loan.MarkEventsCommitted();
        }

        _staged.Clear();
        return Result<Unit, DomainError>.Success(Unit.Value);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };

    private void StageEvents(Loan loan)
    {
        var recordedAt = _clock.GetCurrentInstant();
        var firstVersion = loan.Version - loan.UncommittedEvents.Count;
        for (var index = 0; index < loan.UncommittedEvents.Count; index++)
        {
            var @event = loan.UncommittedEvents[index];
            var (type, version, json) = LoanEventSerializer.Serialize(@event);
            _db.LoanEvents.Add(new LoanEventRow
            {
                Id = Guid.NewGuid(),
                StreamId = loan.Id,
                StreamVersion = firstVersion + index + 1,
                Type = type,
                EventVersion = version,
                PayloadJson = json,
                OccurredAt = @event.Context.EffectiveDate,
                RecordedAt = recordedAt,
                CorrelationId = @event.Context.CorrelationId,
                CommandId = @event.Context.CommandId,
            });
            StageProjections(@event, recordedAt);
        }

        _staged.Add(loan);
    }

    private void StageProjections(LoanEvent @event, Instant recordedAt)
    {
        switch (@event)
        {
            case LoanDisbursed disbursed:
                _db.LoanSchedules.AddRange(ServicingRowMapper.ToScheduleRows(disbursed.Context.LoanId, disbursed.Schedule));
                break;
            case InterestAccrued accrued:
                _db.AccrualLedger.Add(new AccrualLedgerRow
                {
                    LoanId = accrued.Context.LoanId,
                    BusinessDate = accrued.Context.EffectiveDate,
                    PeriodStart = accrued.PeriodStart,
                    Currency = accrued.Amount.Currency,
                    Amount = accrued.Amount.Amount,
                    CommandId = accrued.Context.CommandId,
                    RecordedAt = recordedAt,
                });
                break;
            case PaymentReceived payment:
                var allocation = payment.Allocation;
                _db.PaymentLedger.Add(new PaymentLedgerRow
                {
                    PaymentId = payment.PaymentId,
                    LoanId = payment.Context.LoanId,
                    EffectiveDate = payment.Context.EffectiveDate,
                    Currency = payment.Amount.Currency,
                    Amount = payment.Amount.Amount,
                    FeesAllocated = allocation.FeesAllocated.Amount,
                    PastDueInterestAllocated = allocation.PastDueInterestAllocated.Amount,
                    CurrentInterestAllocated = allocation.CurrentInterestAllocated.Amount,
                    PrincipalAllocated = allocation.PrincipalAllocated.Amount,
                    Excess = allocation.Excess.Amount,
                    CommandId = payment.Context.CommandId,
                    RecordedAt = recordedAt,
                });
                break;
        }
    }
}
