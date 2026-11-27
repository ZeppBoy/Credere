using Credere.Core.ValueObjects;

namespace Credere.Modules.Servicing.Domain.Events;

[EventVersion(1)]
internal sealed record LoanWrittenOff(LoanEventContext Context, string Reason, Money WrittenOffBalance) : LoanEvent(Context);
