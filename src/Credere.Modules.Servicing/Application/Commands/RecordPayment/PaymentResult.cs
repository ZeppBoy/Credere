using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Application.Commands.RecordPayment;

/// <summary>Describes a recorded payment and the resulting loan state.</summary>
/// <param name="PaymentId">The payment identifier.</param>
/// <param name="Allocation">The payment allocation and remaining balances.</param>
/// <param name="State">The current or resulting state.</param>
public sealed record PaymentResult(PaymentId PaymentId, PaymentAllocation Allocation, LoanState State);
