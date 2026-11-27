using Credere.Core.ValueObjects;

namespace Credere.Modules.Servicing.Domain.Events;

[EventVersion(1)]
internal sealed record LoanRecovered(LoanEventContext Context, string Method, Money RecoveredAmount) : LoanEvent(Context);
