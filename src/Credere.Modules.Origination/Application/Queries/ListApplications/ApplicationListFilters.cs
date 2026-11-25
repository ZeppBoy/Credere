using Credere.Modules.Origination.Contracts;
using NodaTime;

namespace Credere.Modules.Origination.Application.Queries.ListApplications;

/// <summary>Filters the applications returned by the module.</summary>
/// <param name="States">The states to include, or null for any state.</param>
/// <param name="Product">The product key.</param>
/// <param name="SubmittedAfter">The optional lower bound for the submission timestamp.</param>
/// <param name="SubmittedBefore">The optional upper bound for the submission timestamp.</param>
public sealed record ApplicationListFilters(
    IReadOnlyList<LoanApplicationState>? States,
    ProductCode? Product,
    Instant? SubmittedAfter,
    Instant? SubmittedBefore);
