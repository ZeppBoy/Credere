using Credere.Modules.Servicing.Contracts;
using Credere.Shared.Commands;
using NodaTime;

namespace Credere.Modules.Servicing.Application.Commands.ApplyAging;

/// <summary>Applies the days past due calculated for a loan on a business date.</summary>
/// <param name="CommandId">The idempotency identifier for the command.</param>
/// <param name="Id">The loan identifier.</param>
/// <param name="BusinessDate">The business date supplied by the caller.</param>
/// <param name="DaysPastDue">The days past due.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
public sealed record ApplyAging(
    Guid CommandId,
    LoanId Id,
    LocalDate BusinessDate,
    int DaysPastDue,
    Guid CorrelationId) : ICommand;
