using Credere.Core.Schedules;
using Credere.Modules.Servicing.Contracts;
using NodaTime;

namespace Credere.Modules.Servicing.Domain.Events;

[EventVersion(1)]
internal sealed record LoanDisbursed(
    LoanEventContext Context,
    DisbursementIntentId IntentId,
    Guid SourceReference,
    int AcceptedTermsVersion,
    string Product,
    AcceptedLoanTerms AcceptedTerms,
    ProductTerms ProductTerms,
    LocalDate FirstPaymentDate,
    AmortizationSchedule Schedule) : LoanEvent(Context);
