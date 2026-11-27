using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Domain.Events;

internal abstract record LoanAgingEvent(
    LoanEventContext Context, LoanState PreviousState, LoanState NewState, int? DaysPastDue) : LoanEvent(Context);
