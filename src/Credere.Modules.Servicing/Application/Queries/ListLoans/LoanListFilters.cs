using Credere.Modules.Servicing.Contracts;
using NodaTime;

namespace Credere.Modules.Servicing.Application.Queries.ListLoans;

/// <summary>Filters the loans returned by the module.</summary>
/// <param name="States">The states to include, or null for any state.</param>
/// <param name="Product">The product key.</param>
/// <param name="DisbursedFrom">The optional lower bound for disbursement dates.</param>
/// <param name="DisbursedTo">The optional upper bound for disbursement dates.</param>
public sealed record LoanListFilters(
    IReadOnlyList<LoanState>? States,
    string? Product,
    LocalDate? DisbursedFrom,
    LocalDate? DisbursedTo);
