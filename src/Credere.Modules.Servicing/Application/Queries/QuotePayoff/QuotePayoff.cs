using Credere.Modules.Servicing.Contracts;
using NodaTime;

namespace Credere.Modules.Servicing.Application.Queries.QuotePayoff;

/// <summary>Requests the amount that would close a loan on a specific date.</summary>
/// <param name="Id">The loan identifier.</param>
/// <param name="PayoffDate">The business date for the payoff.</param>
public sealed record QuotePayoff(LoanId Id, LocalDate PayoffDate);
