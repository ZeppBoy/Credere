using Credere.Modules.Servicing.Contracts;
using Credere.Shared.Commands;
using NodaTime;

namespace Credere.Modules.Servicing.Application.Commands.Restructure;

/// <summary>Records restructuring of a defaulted loan.</summary>
/// <param name="CommandId">The idempotency identifier for the command.</param>
/// <param name="Id">The loan identifier.</param>
/// <param name="BusinessDate">The business date supplied by the caller.</param>
/// <param name="Plan">The restructuring plan.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
public sealed record Restructure(
    Guid CommandId,
    LoanId Id,
    LocalDate BusinessDate,
    string Plan,
    Guid CorrelationId) : ICommand;
