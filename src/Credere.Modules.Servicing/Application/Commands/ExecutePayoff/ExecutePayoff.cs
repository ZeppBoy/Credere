using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using Credere.Shared.Commands;
using NodaTime;

namespace Credere.Modules.Servicing.Application.Commands.ExecutePayoff;

/// <summary>Revalidates and executes a payoff for the quoted date.</summary>
/// <param name="CommandId">The idempotency identifier for the command.</param>
/// <param name="Id">The loan identifier.</param>
/// <param name="PayoffDate">The business date for the payoff.</param>
/// <param name="QuotedTotal">The total from a prior quote for this payoff date.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
public sealed record ExecutePayoff(
    Guid CommandId,
    LoanId Id,
    LocalDate PayoffDate,
    Money QuotedTotal,
    Guid CorrelationId) : ICommand;
