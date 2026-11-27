using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Domain.Events;

[EventVersion(1)]
internal sealed record PaymentReceived(
    LoanEventContext Context,
    PaymentId PaymentId,
    Money Amount,
    PaymentAllocation Allocation,
    LoanState State) : LoanEvent(Context);
