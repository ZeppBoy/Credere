using Credere.Modules.Servicing.Contracts;
using Credere.Shared.Commands;
using NodaTime;

namespace Credere.Modules.Servicing.Application.Commands.Activate;

/// <summary>Activates a disbursed loan on its first eligible business day.</summary>
/// <param name="CommandId">The idempotency identifier for the command.</param>
/// <param name="Id">The loan identifier.</param>
/// <param name="EffectiveDate">The date on which this operation takes effect.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
public sealed record Activate(Guid CommandId, LoanId Id, LocalDate EffectiveDate, Guid CorrelationId) : ICommand;
