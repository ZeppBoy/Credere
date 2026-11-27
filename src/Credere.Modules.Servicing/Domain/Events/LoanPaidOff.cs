using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Domain.Events;

[EventVersion(1)]
internal sealed record LoanPaidOff(LoanEventContext Context, PaymentId FinalPaymentId) : LoanEvent(Context);
