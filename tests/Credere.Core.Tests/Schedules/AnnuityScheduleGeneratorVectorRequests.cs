using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using NodaTime;
using NodaTime.Text;

namespace Credere.Core.Tests.Schedules;

/// <summary>Builds the library types that the annuity reference vectors describe.</summary>
internal static class AnnuityScheduleGeneratorVectorRequests
{
    public static Money Eur(decimal amount) => Money.Create(amount, "EUR").Value;

    public static LocalDate Date(string iso) => LocalDatePattern.Iso.Parse(iso).Value;

    public static DayCountConvention Convention(string name) => name switch
    {
        "ACT/360" => new Act360(),
        "ACT/365 Fixed" => new Act365Fixed(),
        "ACT/ACT (ISDA)" => new ActActIsda(),
        "30/360 ISDA" => new Thirty360Isda(),
        "30E/360" => new ThirtyE360(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown convention in the vector data."),
    };

    public static Result<ScheduleRequest, DomainError> RequestOf(AnnuityScheduleGeneratorVector vector) =>
        Request(
            vector.Principal, vector.RatePercent, vector.Convention, vector.Months, vector.Frequency,
            vector.Disbursement, vector.FirstPayment);

    public static Result<ScheduleRequest, DomainError> RequestOf(AnnuityScheduleGeneratorFailureVector vector) =>
        Request(
            vector.Principal, vector.RatePercent, vector.Convention, vector.Months, vector.Frequency,
            vector.Disbursement, vector.FirstPayment);

    private static Result<ScheduleRequest, DomainError> Request(
        decimal principal, decimal ratePercent, string convention, int months, PaymentFrequency frequency,
        string disbursement, string firstPayment) =>
        ScheduleRequest.Create(
            Eur(principal),
            FixedRate.Create(new Percentage(ratePercent), Convention(convention)).Value,
            LoanTerm.FromMonths(months),
            Date(disbursement),
            Date(firstPayment),
            frequency);
}
