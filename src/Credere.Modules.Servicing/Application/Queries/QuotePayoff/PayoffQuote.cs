using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using NodaTime;

namespace Credere.Modules.Servicing.Application.Queries.QuotePayoff;

/// <summary>Describes the balances and total needed to close a loan on the specified date.</summary>
/// <param name="Id">The loan identifier.</param>
/// <param name="PayoffDate">The business date for the payoff.</param>
/// <param name="Principal">The principal outstanding on the payoff date.</param>
/// <param name="PastDueInterest">The outstanding past-due interest.</param>
/// <param name="CurrentInterest">The outstanding current interest.</param>
/// <param name="Fees">The outstanding fees included in the quote.</param>
/// <param name="Total">The total needed to close all outstanding balances.</param>
public sealed record PayoffQuote(
    LoanId Id,
    LocalDate PayoffDate,
    Money Principal,
    Money PastDueInterest,
    Money CurrentInterest,
    Money Fees,
    Money Total);
