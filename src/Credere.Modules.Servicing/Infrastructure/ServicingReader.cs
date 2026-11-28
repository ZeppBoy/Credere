using Credere.Core.Schedules;
using Credere.Modules.Servicing.Application.Queries.GetLoan;
using Credere.Modules.Servicing.Application.Queries.ListLoans;
using Credere.Modules.Servicing.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Credere.Modules.Servicing.Infrastructure;

/// <summary>The read side of the facade: views and the schedule snapshot, built with no tracking.</summary>
internal sealed class ServicingReader
{
    private readonly ServicingDbContext _db;

    /// <summary>Initializes a new instance of the <see cref="ServicingReader"/> class.</summary>
    /// <param name="db">The context to read from.</param>
    public ServicingReader(ServicingDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <summary>Gets one loan's view.</summary>
    /// <param name="id">The loan id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The view, or <see langword="null"/> when no loan has that id.</returns>
    public async Task<LoanView?> GetLoanAsync(LoanId id, CancellationToken cancellationToken)
    {
        var row = await _db.Loans
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ServicingRowMapper.ToView(row);
    }

    /// <summary>Lists loan views, most recently disbursed first.</summary>
    /// <param name="filters">
    /// The filters, all optional. A <see langword="null"/> state list matches every state and an empty one matches none.
    /// <c>DisbursedFrom</c> and <c>DisbursedTo</c> are both inclusive, since they are dates.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching views, ordered by disbursement date and then by id. The list is not paged.</returns>
    public async Task<IReadOnlyList<LoanView>> ListLoansAsync(LoanListFilters filters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filters);

        var query = _db.Loans.AsNoTracking();
        if (filters.States is { } states)
        {
            var stateList = states.ToArray();
            query = query.Where(row => stateList.Contains(row.State));
        }

        if (filters.Product is { } product)
        {
            query = query.Where(row => row.Product == product);
        }

        if (filters.DisbursedFrom is { } from)
        {
            query = query.Where(row => row.DisbursementDate >= from);
        }

        if (filters.DisbursedTo is { } to)
        {
            query = query.Where(row => row.DisbursementDate <= to);
        }

        var rows = await query
            .OrderByDescending(row => row.DisbursementDate)
            .ThenBy(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ServicingRowMapper.ToView).ToList();
    }

    /// <summary>Gets the schedule snapshot taken at disbursement.</summary>
    /// <param name="id">The loan id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The schedule, or <see langword="null"/> when no loan has that id.</returns>
    public async Task<AmortizationSchedule?> GetScheduleAsync(LoanId id, CancellationToken cancellationToken)
    {
        var rows = await _db.LoanSchedules
            .AsNoTracking()
            .Where(row => row.LoanId == id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Count == 0 ? null : ServicingRowMapper.ToSchedule(rows);
    }

    /// <summary>Gets one disbursement intent's view.</summary>
    /// <param name="id">The intent id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The view, or <see langword="null"/> when no intent has that id.</returns>
    public async Task<DisbursementIntentView?> GetIntentAsync(DisbursementIntentId id, CancellationToken cancellationToken)
    {
        var row = await _db.DisbursementIntents
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ServicingRowMapper.ToIntentView(row);
    }
}
