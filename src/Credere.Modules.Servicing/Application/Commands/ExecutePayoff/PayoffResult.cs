using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Application.Commands.ExecutePayoff;

/// <summary>Describes a completed payoff payment.</summary>
/// <param name="PaymentId">The payment identifier.</param>
/// <param name="Allocation">The payment allocation and remaining balances.</param>
public sealed record PayoffResult(PaymentId PaymentId, PaymentAllocation Allocation);
