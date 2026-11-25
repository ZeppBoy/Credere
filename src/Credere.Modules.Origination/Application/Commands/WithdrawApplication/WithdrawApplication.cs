using Credere.Modules.Origination.Contracts;
using Credere.Shared.Commands;

namespace Credere.Modules.Origination.Application.Commands.WithdrawApplication;

/// <summary>Withdraws a loan application.</summary>
/// <param name="CommandId">The idempotency identifier for the command.</param>
/// <param name="Id">The application identifier.</param>
/// <param name="Reason">The reason supplied for the operation.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
public sealed record WithdrawApplication(
    Guid CommandId,
    LoanApplicationId Id,
    string Reason,
    Guid CorrelationId) : ICommand;
