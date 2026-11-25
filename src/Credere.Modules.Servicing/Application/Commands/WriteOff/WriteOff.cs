using Credere.Modules.Servicing.Contracts;
using Credere.Shared.Commands;
using NodaTime;

namespace Credere.Modules.Servicing.Application.Commands.WriteOff;

/// <summary>Writes off a defaulted loan.</summary>
/// <param name="CommandId">The idempotency identifier for the command.</param>
/// <param name="Id">The loan identifier.</param>
/// <param name="BusinessDate">The business date supplied by the caller.</param>
/// <param name="Reason">The reason supplied for the operation.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
public sealed record WriteOff(
    Guid CommandId,
    LoanId Id,
    LocalDate BusinessDate,
    string Reason,
    Guid CorrelationId) : ICommand;
