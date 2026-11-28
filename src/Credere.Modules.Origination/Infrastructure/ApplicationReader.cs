using Credere.Modules.Origination.Application.Queries.GetApplication;
using Credere.Modules.Origination.Application.Queries.ListApplications;
using Credere.Modules.Origination.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Credere.Modules.Origination.Infrastructure;

/// <summary>The read side of the facade: views built from the current-state rows, with no tracking.</summary>
internal sealed class ApplicationReader
{
    private readonly OriginationDbContext _db;

    /// <summary>Initializes a new instance of the <see cref="ApplicationReader"/> class.</summary>
    /// <param name="db">The context to read from.</param>
    public ApplicationReader(OriginationDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <summary>Gets one application's view.</summary>
    /// <param name="id">The application id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The view, or <see langword="null"/> when no application has that id.</returns>
    public async Task<ApplicationView?> GetAsync(LoanApplicationId id, CancellationToken cancellationToken)
    {
        var row = await _db.LoanApplications
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : LoanApplicationRowMapper.ToView(row);
    }

    /// <summary>Lists application views, newest submission first.</summary>
    /// <param name="filters">
    /// The filters, all optional. A <see langword="null"/> state list matches every state and an empty one matches none.
    /// <c>SubmittedAfter</c> is inclusive and <c>SubmittedBefore</c> is exclusive, so adjacent windows do not overlap.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching views, ordered by submission time and then by id. The list is not paged.</returns>
    public async Task<IReadOnlyList<ApplicationView>> ListAsync(
        ApplicationListFilters filters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filters);

        var query = _db.LoanApplications.AsNoTracking();
        if (filters.States is { } states)
        {
            var stateList = states.ToArray();
            query = query.Where(row => stateList.Contains(row.State));
        }

        if (filters.Product is { } product)
        {
            var code = product.Value;
            query = query.Where(row => row.ProductCode == code);
        }

        if (filters.SubmittedAfter is { } after)
        {
            query = query.Where(row => row.SubmittedAt >= after);
        }

        if (filters.SubmittedBefore is { } before)
        {
            query = query.Where(row => row.SubmittedAt < before);
        }

        var rows = await query
            .OrderByDescending(row => row.SubmittedAt)
            .ThenBy(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(LoanApplicationRowMapper.ToView).ToList();
    }
}
