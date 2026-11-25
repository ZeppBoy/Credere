using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using Credere.Shared.Commands;
using NodaTime;

namespace Credere.Modules.Servicing.Application.Commands.Recover;

/// <summary>Records recovery of a defaulted loan.</summary>
/// <param name="CommandId">The idempotency identifier for the command.</param>
/// <param name="Id">The loan identifier.</param>
/// <param name="BusinessDate">The business date supplied by the caller.</param>
/// <param name="Method">The recovery method.</param>
/// <param name="RecoveredAmount">The amount recovered.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
public sealed record Recover(
    Guid CommandId,
    LoanId Id,
    LocalDate BusinessDate,
    string Method,
    Money RecoveredAmount,
    Guid CorrelationId) : ICommand;
