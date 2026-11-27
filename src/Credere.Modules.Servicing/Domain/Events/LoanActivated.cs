namespace Credere.Modules.Servicing.Domain.Events;

[EventVersion(1)]
internal sealed record LoanActivated(LoanEventContext Context) : LoanEvent(Context);
