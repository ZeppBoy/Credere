using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Modules.Servicing.Domain.Events;

[EventVersion(1)]
internal sealed record InterestAccrued(
    LoanEventContext Context,
    LocalDate PeriodStart,
    Money Amount,
    LocalDate SegmentStart,
    Money CurrentInterest,
    Money PastDueInterest) : LoanEvent(Context);
