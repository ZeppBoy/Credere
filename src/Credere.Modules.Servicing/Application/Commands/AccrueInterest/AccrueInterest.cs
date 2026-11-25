using Credere.Modules.Servicing.Contracts;
using Credere.Shared.Commands;
using NodaTime;

namespace Credere.Modules.Servicing.Application.Commands.AccrueInterest;

/// <summary>Posts accrual through a business date.</summary>
/// <param name="CommandId">The idempotency identifier for the command.</param>
/// <param name="Id">The loan identifier.</param>
/// <param name="BusinessDate">The business date supplied by the caller.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
public sealed record AccrueInterest(Guid CommandId, LoanId Id, LocalDate BusinessDate, Guid CorrelationId) : ICommand;
