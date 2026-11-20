using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Core.Aprc;

/// <summary>A dated cash flow used in the APRC calculation.</summary>
/// <param name="Amount">The amount in the currency of the credit agreement.</param>
/// <param name="Date">The date of the drawdown or borrower payment.</param>
public sealed record CashFlow(Money Amount, LocalDate Date);
