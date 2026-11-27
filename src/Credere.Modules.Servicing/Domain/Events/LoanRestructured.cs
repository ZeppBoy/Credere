namespace Credere.Modules.Servicing.Domain.Events;

[EventVersion(1)]
internal sealed record LoanRestructured(LoanEventContext Context, string Plan) : LoanEvent(Context);
