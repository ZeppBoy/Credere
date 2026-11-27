using Credere.Core.Results;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain;
using Credere.Modules.Origination.Infrastructure.Serialization;
using Credere.Shared.IntegrationEvents;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Credere.Modules.Origination.Infrastructure;

/// <summary>
/// The persistence operations of the Origination facade: load an application, stage its new state and history, stage an
/// outbox event and a command receipt, then commit them all in one transaction (ADR-0004, ADR-0005, ADR-0007).
/// </summary>
/// <remarks>
/// <para>
/// One instance serves one command, over one scoped <see cref="OriginationDbContext"/>. After a failed
/// <see cref="CommitAsync"/> the context holds stale rows, so a retry uses a new scope.
/// </para>
/// <para>
/// An aggregate is rebuilt from its history, because the aggregate has no other way in. The state row is read first, and
/// only the history up to its stream version is replayed, so a writer that commits in between cannot slip an
/// unseen event into the aggregate. The tracked state row then carries its <c>RowVersion</c> into the update, which
/// fails with a <see cref="ConcurrencyConflict"/> if anyone else saved first.
/// </para>
/// </remarks>
internal sealed class LoanApplicationRepository
{
    private const string AggregateName = nameof(LoanApplication);
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private readonly OriginationDbContext _db;
    private readonly IClock _clock;
    private readonly List<LoanApplication> _staged = [];

    /// <summary>Initializes a new instance of the <see cref="LoanApplicationRepository"/> class.</summary>
    /// <param name="db">The unit of work.</param>
    /// <param name="clock">The clock that stamps rows with when they were written.</param>
    public LoanApplicationRepository(OriginationDbContext db, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    /// <summary>Loads an application by replaying its history.</summary>
    /// <param name="id">The application id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The application with no uncommitted events, or <see langword="null"/> when none has that id.</returns>
    /// <exception cref="InvalidOperationException">The stored history is incomplete or inconsistent with the state row.</exception>
    public async Task<LoanApplication?> FindAsync(LoanApplicationId id, CancellationToken cancellationToken)
    {
        var row = await _db.LoanApplications
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var history = await _db.LoanApplicationEvents
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
                    $"The history of application {id.Value} has a gap: expected stream version {index + 1}, found {history[index].StreamVersion}.");
            }
        }

        if (history.Count != row.StreamVersion)
        {
            throw new InvalidOperationException(
                $"The state row of application {id.Value} is at stream version {row.StreamVersion}, but its history holds {history.Count} events.");
        }

        var events = history
            .Select(item => LoanApplicationEventSerializer.Deserialize(item.Type, item.EventVersion, item.PayloadJson))
            .ToList();

        // The aggregate takes its id from the first event, so a payload that names another application would load as
        // that application. Every payload must name the stream it is stored in.
        var foreign = events.FirstOrDefault(@event => @event.ApplicationId != id);
        if (foreign is not null)
        {
            throw new InvalidOperationException(
                $"The history of application {id.Value} holds {foreign.GetType().Name} for application {foreign.ApplicationId.Value}.");
        }

        return LoanApplication.Rehydrate(events);
    }

    /// <summary>Stages a newly submitted application: its state row and one history row for each uncommitted event.</summary>
    /// <param name="application">The new application, as <c>Submit</c> returned it.</param>
    /// <exception cref="InvalidOperationException">The application was not just created.</exception>
    public void Add(LoanApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (application.Version != application.UncommittedEvents.Count)
        {
            throw new InvalidOperationException("Only an application with no committed history can be added.");
        }

        var row = new LoanApplicationRow();
        LoanApplicationRowMapper.Apply(row, application);
        _db.LoanApplications.Add(row);
        StageEvents(application);
    }

    /// <summary>
    /// Stages the changes a command made to an application that <see cref="FindAsync"/> loaded in this unit of work.
    /// </summary>
    /// <param name="application">The loaded application, after its command method ran.</param>
    /// <exception cref="InvalidOperationException">The application was not loaded here, or is not the one that was.</exception>
    public void Update(LoanApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var row = _db.LoanApplications.Local.SingleOrDefault(candidate => candidate.Id == application.Id)
            ?? throw new InvalidOperationException(
                $"Application {application.Id.Value} was not loaded in this unit of work, so its concurrency token is unknown.");
        if (row.StreamVersion + application.UncommittedEvents.Count != application.Version)
        {
            throw new InvalidOperationException(
                $"Application {application.Id.Value} is at version {application.Version} with {application.UncommittedEvents.Count} new events, which does not follow the loaded stream version {row.StreamVersion}.");
        }

        if (application.UncommittedEvents.Count == 0)
        {
            return;
        }

        LoanApplicationRowMapper.Apply(row, application);
        StageEvents(application);
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
    /// <param name="handler">The handler, one of <see cref="OriginationSchema.ReceiptHandlers"/>.</param>
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
    /// <param name="handler">The handler, one of <see cref="OriginationSchema.ReceiptHandlers"/>.</param>
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
    /// Writes everything staged in one transaction: state rows, history rows, outbox rows and receipts, or nothing.
    /// </summary>
    /// <param name="id">The application the command was for, named in a conflict.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// Success, or a <see cref="ConcurrencyConflict"/> when another writer saved the application first or the same
    /// command was handled at the same time. The caller retries in a new scope: a repeated command then finds its receipt.
    /// </returns>
    public async Task<Result<Unit, DomainError>> CommitAsync(LoanApplicationId id, CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(id);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Conflict(id);
        }

        foreach (var application in _staged)
        {
            application.MarkEventsCommitted();
        }

        _staged.Clear();
        return Result<Unit, DomainError>.Success(Unit.Value);
    }

    private static Result<Unit, DomainError> Conflict(LoanApplicationId id) =>
        Result<Unit, DomainError>.Failure(new ConcurrencyConflict(AggregateName, id.Value));

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };

    private void StageEvents(LoanApplication application)
    {
        var recordedAt = _clock.GetCurrentInstant();
        var firstVersion = application.Version - application.UncommittedEvents.Count;
        for (var index = 0; index < application.UncommittedEvents.Count; index++)
        {
            var @event = application.UncommittedEvents[index];
            var (type, version, json) = LoanApplicationEventSerializer.Serialize(@event);
            _db.LoanApplicationEvents.Add(new LoanApplicationEventRow
            {
                Id = Guid.NewGuid(),
                StreamId = application.Id,
                StreamVersion = firstVersion + index + 1,
                Type = type,
                EventVersion = version,
                PayloadJson = json,
                OccurredAt = @event.OccurredAt,
                RecordedAt = recordedAt,
                CorrelationId = @event.CorrelationId,
                CommandId = @event.CommandId,
            });
        }

        _staged.Add(application);
    }
}
