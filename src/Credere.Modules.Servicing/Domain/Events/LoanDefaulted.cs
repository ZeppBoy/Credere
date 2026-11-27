using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Domain.Events;

[EventVersion(1)]
internal sealed record LoanDefaulted(LoanEventContext Context, LoanState PreviousState, int Days)
    : LoanAgingEvent(Context, PreviousState, LoanState.Defaulted, Days);
