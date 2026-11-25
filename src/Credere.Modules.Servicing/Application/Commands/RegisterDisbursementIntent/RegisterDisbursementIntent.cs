using Credere.Modules.Servicing.Contracts;
using Credere.Shared.Commands;

namespace Credere.Modules.Servicing.Application.Commands.RegisterDisbursementIntent;

/// <summary>Registers an accepted application for disbursement.</summary>
/// <param name="CommandId">The idempotency identifier for the command.</param>
/// <param name="SourceReference">The originating application's opaque reference.</param>
/// <param name="AcceptedTermsVersion">The version accepted by the borrower.</param>
/// <param name="Product">The product key.</param>
/// <param name="Terms">The terms accepted by the borrower.</param>
/// <param name="CorrelationId">The identifier that correlates the operation with related work.</param>
public sealed record RegisterDisbursementIntent(
    Guid CommandId,
    Guid SourceReference,
    int AcceptedTermsVersion,
    string Product,
    AcceptedLoanTerms Terms,
    Guid CorrelationId) : ICommand;
