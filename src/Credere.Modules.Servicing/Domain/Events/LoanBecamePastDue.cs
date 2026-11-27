using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Domain.Events;

[EventVersion(1)]
internal sealed record LoanBecamePastDue(LoanEventContext Context, LoanState PreviousState, int Days)
    : LoanAgingEvent(Context, PreviousState, LoanState.PastDue, Days);
