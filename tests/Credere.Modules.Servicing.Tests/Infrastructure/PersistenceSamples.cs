using Credere.Core.Results;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Application.Commands.AccrueInterest;
using Credere.Modules.Servicing.Application.Commands.Activate;
using Credere.Modules.Servicing.Application.Commands.ApplyAging;
using Credere.Modules.Servicing.Application.Commands.Disburse;
using Credere.Modules.Servicing.Application.Commands.RecordPayment;
using Credere.Modules.Servicing.Application.Commands.WriteOff;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain;
using NodaTime;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;
using static Credere.Modules.Servicing.Tests.Domain.Loans.LoanSamples;

namespace Credere.Modules.Servicing.Tests.Infrastructure;

/// <summary>
/// Builders for persistence tests. Every loan and intent gets its own ids and product key, so a test sees only its own
/// rows in the database the whole collection shares. The amounts, rate and schedule are the M1 Level30E360_24m fixture
/// the domain tests use: arbitrary test inputs, with no calculated value asserted here that a domain test does not.
/// </summary>
internal static class PersistenceSamples
{
    internal static LoanId NewLoanId() => new(Guid.NewGuid());

    internal static DisbursementIntentId NewIntentId() => new(Guid.NewGuid());

    internal static string NewProduct() => "P" + Guid.NewGuid().ToString("N")[..20].ToUpperInvariant();

    internal static AcceptedLoanTerms SampleTerms() => new(Principal, Rate.NominalRate, LoanTerm.FromMonths(24));

    internal static DisbursementIntent NewIntent(
        DisbursementIntentId? id = null,
        Guid? sourceReference = null,
        int termsVersion = 1,
        string? product = null,
        AcceptedLoanTerms? terms = null) =>
        new(id ?? NewIntentId(), sourceReference ?? Guid.NewGuid(), termsVersion, product ?? NewProduct(), terms ?? SampleTerms());

    /// <summary>
    /// Disburses a loan from an intent. Without a date it uses the fixture's dates. With one, the first payment is a
    /// month later and the schedule is generated for those dates.
    /// </summary>
    internal static Loan NewLoan(LoanId id, DisbursementIntent intent, LocalDate? disbursedOn = null)
    {
        if (disbursedOn is not { } date)
        {
            return Succeeded(Loan.Disburse(
                new Disburse(Guid.NewGuid(), intent.Id, DisbursedOn, FirstDueDate, CorrelationId), id, intent, Product, Schedule));
        }

        var firstPayment = date.PlusMonths(1);
        var schedule = MakeSchedule(Principal, Rate, date, firstPayment, LoanTerm.FromMonths(24));
        return Succeeded(Loan.Disburse(
            new Disburse(Guid.NewGuid(), intent.Id, date, firstPayment, CorrelationId), id, intent, Product, schedule));
    }

    internal static Activate ActivateOn(LoanId id) => new(Guid.NewGuid(), id, ActivatedOn, CorrelationId);

    internal static AccrueInterest AccrueOn(LoanId id, LocalDate date) => new(Guid.NewGuid(), id, date, CorrelationId);

    internal static RecordPayment PayOn(LoanId id, Money amount, LocalDate date) =>
        new(Guid.NewGuid(), id, amount, date, CorrelationId);

    internal static ApplyAging AgeOn(LoanId id, int days) => new(Guid.NewGuid(), id, FirstDueDate, days, CorrelationId);

    internal static WriteOff WriteOffOn(LoanId id, LocalDate date) =>
        new(Guid.NewGuid(), id, date, "Portion uncollectible", CorrelationId);

    internal static T Succeeded<T>(Result<T, DomainError> result) =>
        result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"The test setup failed: {result.Error.Message}");
}
