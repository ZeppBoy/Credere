using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Domain.Events;

[EventVersion(1)]
internal sealed record PaymentExcessRecorded(LoanEventContext Context, PaymentId PaymentId, Money Excess) : LoanEvent(Context);
