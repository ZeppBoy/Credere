using Credere.Modules.Servicing.Contracts;
using Credere.Shared.Commands;
using NodaTime;

namespace Credere.Modules.Servicing.Application.Commands.Disburse;

/// <summary>Confirms disbursement for a registered intent.</summary>
/// <param name="CommandId">The idempotency identifier for the command.</param>
/// <param name="IntentId">The registered disbursement intent identifier.</param>
/// <param name="DisbursementDate">The business date of the disbursement.</param>
/// <param name="FirstPaymentDate">The date of the first scheduled payment.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
public sealed record Disburse(
    Guid CommandId,
    DisbursementIntentId IntentId,
    LocalDate DisbursementDate,
    LocalDate FirstPaymentDate,
    Guid CorrelationId) : ICommand;
