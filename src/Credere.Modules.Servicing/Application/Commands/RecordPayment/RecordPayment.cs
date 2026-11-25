using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using Credere.Shared.Commands;
using NodaTime;

namespace Credere.Modules.Servicing.Application.Commands.RecordPayment;

/// <summary>Records and allocates a borrower payment on its effective date.</summary>
/// <param name="CommandId">The idempotency identifier for the command.</param>
/// <param name="Id">The loan identifier.</param>
/// <param name="Amount">The amount received from the borrower.</param>
/// <param name="EffectiveDate">The date on which this operation takes effect.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
public sealed record RecordPayment(
    Guid CommandId,
    LoanId Id,
    Money Amount,
    LocalDate EffectiveDate,
    Guid CorrelationId) : ICommand;
