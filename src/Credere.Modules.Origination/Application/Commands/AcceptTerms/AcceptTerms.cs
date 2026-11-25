using Credere.Modules.Origination.Contracts;
using Credere.Shared.Commands;

namespace Credere.Modules.Origination.Application.Commands.AcceptTerms;

/// <summary>Records the borrower's acceptance of the current approved terms.</summary>
/// <param name="CommandId">The idempotency identifier for the command.</param>
/// <param name="Id">The application identifier.</param>
/// <param name="TermsVersion">The version of the approved terms.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
public sealed record AcceptTerms(Guid CommandId, LoanApplicationId Id, int TermsVersion, Guid CorrelationId) : ICommand;
