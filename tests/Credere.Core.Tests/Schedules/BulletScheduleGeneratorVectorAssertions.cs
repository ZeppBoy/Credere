using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using NodaTime;
using NodaTime.Text;
using Xunit;

namespace Credere.Core.Tests.Schedules;

internal static class BulletScheduleGeneratorVectorAssertions
{
    public static ScheduleRequest Request(
        decimal principal, decimal ratePercent, string dayCount, int months, PaymentFrequency frequency,
        LocalDate disbursement, LocalDate firstPayment)
    {
        DayCountConvention convention = dayCount switch
        {
            "ACT/360" => new Act360(),
            "ACT/365 Fixed" => new Act365Fixed(),
            "ACT/ACT (ISDA)" => new ActActIsda(),
            "30/360 ISDA" => new Thirty360Isda(),
            "30E/360" => new ThirtyE360(),
            _ => throw new ArgumentOutOfRangeException(nameof(dayCount), dayCount, "Unknown reference convention."),
        };

        var rate = FixedRate.Create(new Percentage(ratePercent), convention).Value;
        return ScheduleRequest.Create(Euro(principal), rate, LoanTerm.FromMonths(months),
            disbursement, firstPayment, frequency).Value;
    }

    public static AmortizationSchedule RequireSchedule(IScheduleGenerator generator, ScheduleRequest request)
    {
        var result = generator.Generate(request);
        Assert.True(result.IsSuccess, result.IsSuccess ? string.Empty : result.Error.ToString());
        return result.Value;
    }

    public static Task VerifySnapshot(string name, ScheduleRequest request, IReadOnlyList<ScheduleRow> rows)
    {
        var header = FormattableString.Invariant(
            $"{name}: {request.Principal.Currency} {request.Principal.Amount:F2} at {request.Rate.NominalRate.Value}% ({request.Rate.DayCountConvention.Name}), {request.Term.Months} months, {request.PaymentFrequency}");
        var lines = rows.Select(row => FormattableString.Invariant(
            $"{row.Index} | {LocalDatePattern.Iso.Format(row.PaymentDate)} | {row.OpeningBalance.Amount:F2} | {row.CapitalisedInterest.Amount:F2} | {row.InterestDue.Amount:F2} | {row.PrincipalDue.Amount:F2} | {row.TotalPayment.Amount:F2} | {row.ClosingBalance.Amount:F2}"));
        var snapshot = header + "\nindex | date | opening | capitalised | interest due | principal due | total | closing\n"
            + string.Join('\n', lines) + "\n";

        return Verifier.Verify(snapshot, "txt")
            .UseDirectory("Snapshots")
            .UseFileName($"BulletScheduleGenerator.{name}");
    }

    public static LocalDate Date(string iso) => LocalDatePattern.Iso.Parse(iso).Value;

    public static Money Euro(decimal amount) => Money.Create(amount, "EUR").Value;
}
