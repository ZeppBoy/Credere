using Credere.Core.Results;
using Credere.Core.ValueObjects;

namespace Credere.Modules.Servicing.Contracts;

/// <summary>Indicates that the quoted payoff total differs from the recomputed total.</summary>
/// <param name="QuotedTotal">The total supplied from the payoff quote.</param>
/// <param name="CurrentTotal">The total recomputed for the quoted date.</param>
public sealed record PayoffQuoteStale(Money QuotedTotal, Money CurrentTotal)
    : DomainError("payoff-quote-stale", "The payoff quote no longer matches the loan.");
