using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain.Events;
using NodaTime;

namespace Credere.Modules.Servicing.Domain;

/// <summary>The current state and fixed schedule needed to restore a loan for commands (ADR-0004).</summary>
internal sealed record LoanSnapshot(
    LoanId Id,
    DisbursementIntentId DisbursementIntentId,
    Guid SourceReference,
    int TermsVersion,
    string Product,
    AcceptedLoanTerms AcceptedTerms,
    ProductTerms ProductTerms,
    LocalDate DisbursementDate,
    LocalDate FirstPaymentDate,
    AmortizationSchedule Schedule,
    Money RemainingPrincipal,
    Money FeesOutstanding,
    InterestAccrual.Position Interest,
    LoanState State,
    int? DaysPastDue,
    long Version)
{
    internal static LoanSnapshot FromDisbursement(LoanDisbursed disbursed)
    {
        var date = disbursed.Context.EffectiveDate;
        var principal = disbursed.AcceptedTerms.Principal;
        var zero = Money.Zero(principal.Currency);
        return new LoanSnapshot(
            disbursed.Context.LoanId, disbursed.IntentId, disbursed.SourceReference, disbursed.AcceptedTermsVersion,
            disbursed.Product, disbursed.AcceptedTerms, disbursed.ProductTerms, date, disbursed.FirstPaymentDate,
            disbursed.Schedule, principal, zero, new InterestAccrual.Position(date, date, zero, zero),
            LoanState.Disbursed, DaysPastDue: null, Version: 1);
    }
}
