using Credere.Modules.Servicing.Contracts;
using NodaTime;

namespace Credere.Modules.Servicing.Domain.Events;

internal sealed record LoanEventContext(LoanId LoanId, LocalDate EffectiveDate, Guid CommandId, Guid CorrelationId);
