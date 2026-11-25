using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Domain;
using NodaTime;
using NodaTime.Text;

namespace Credere.Modules.Servicing.Tests.Domain;

internal static class AccrualSamples
{
    internal static InterestCalculator Calculator { get; } = new();

    // M1 Level30E360_24m vector in BulletScheduleGeneratorInterestOnlyVectorTests.
    internal static Money Principal { get; } = Euro(10000m);

    internal static FixedRate Rate { get; } = FixedRate.Create(Percentage.Create(5m).Value, new ThirtyE360()).Value;

    internal static AmortizationSchedule Schedule { get; } = MakeSchedule(
        Principal, Rate, Date("2026-01-15"), Date("2026-02-15"), LoanTerm.FromMonths(24));

    internal static InterestAccrual.Position At(
        string postedDate = "2026-01-15",
        string segmentStart = "2026-01-15",
        decimal current = 0m,
        decimal pastDue = 0m) =>
        new(Date(postedDate), Date(segmentStart), Euro(current), Euro(pastDue));

    internal static Money Euro(decimal amount) => Money.Create(amount, "EUR").Value;

    internal static LocalDate Date(string isoDate) => LocalDatePattern.Iso.Parse(isoDate).Value;

    internal static AmortizationSchedule MakeSchedule(
        Money principal, FixedRate rate, LocalDate disbursement, LocalDate firstPayment, LoanTerm term)
    {
        var request = ScheduleRequest.Create(principal, rate, term, disbursement, firstPayment, PaymentFrequency.Monthly).Value;
        return new BulletScheduleGenerator(Calculator, capitaliseInterest: false).Generate(request).Value;
    }
}
