using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Domain.Events;

[EventVersion(1)]
internal sealed record LoanBecameActive(LoanEventContext Context, LoanState PreviousState)
    : LoanAgingEvent(Context, PreviousState, LoanState.Active, null);
