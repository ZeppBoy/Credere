using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using FsCheck.Xunit;
using NodaTime;

namespace Credere.Core.Tests.Schedules;

/// <summary>
/// Checks the annuity invariants of docs/domain/schedules.md, "Verification", on generated requests. Principals start at
/// 1,000.00, so the instalment never overpays the balance (see the failure vectors for tiny principals).
/// </summary>
public sealed class AnnuityScheduleGeneratorPropertiesTests
{
    private static readonly InterestCalculator Calculator = new();
    private static readonly AnnuityScheduleGenerator Generator = new(Calculator);
    private static readonly LocalDate Origin = new(2000, 1, 1);

    private static readonly DayCountConvention[] Conventions =
    [
        new Act360(), new Act365Fixed(), new ActActIsda(), new Thirty360Isda(), new ThirtyE360(),
    ];

    // Principals from 1,000.00 to about 10 million; rates 0% to 100% in hundredths of a percent.
    private static ScheduleRequest Request(
        uint principalCents, ushort rate, byte periods, byte convention, bool quarterly, ushort disbursementOffset, byte firstGap)
    {
        var frequency = quarterly ? PaymentFrequency.Quarterly : PaymentFrequency.Monthly;
        var months = (1 + (periods % 100)) * (quarterly ? 3 : 1);
        var disbursement = Origin.PlusDays(disbursementOffset);

        return ScheduleRequest.Create(
            Money.Create(1000m + (principalCents % 1_000_000_000u) / 100m, "EUR").Value,
            FixedRate.Create(new Percentage(rate % 10_001 / 100m), Conventions[convention % Conventions.Length]).Value,
            LoanTerm.FromMonths(months),
            disbursement,
            disbursement.PlusDays(1 + (firstGap % 60)),
            frequency).Value;
    }

    [Property]
    public bool PrincipalDue_sumsToThePrincipal(
        uint principalCents, ushort rate, byte periods, byte convention, bool quarterly, ushort offset, byte gap)
    {
        var request = Request(principalCents, rate, periods, convention, quarterly, offset, gap);
        var schedule = Generator.Generate(request).Value;

        return schedule.Rows.Sum(row => row.PrincipalDue.Amount) == request.Principal.Amount;
    }

    [Property]
    public bool LastRow_closesTheLoanAtExactlyZero(
        uint principalCents, ushort rate, byte periods, byte convention, bool quarterly, ushort offset, byte gap)
    {
        var schedule = Generator.Generate(Request(principalCents, rate, periods, convention, quarterly, offset, gap)).Value;

        return schedule.Rows[^1].ClosingBalance.Amount == 0m;
    }

    [Property]
    public bool RowCount_isTheTermInPaymentPeriods(
        uint principalCents, ushort rate, byte periods, byte convention, bool quarterly, ushort offset, byte gap)
    {
        var request = Request(principalCents, rate, periods, convention, quarterly, offset, gap);
        var schedule = Generator.Generate(request).Value;

        return schedule.Rows.Count == request.Term.Months / (quarterly ? 3 : 1);
    }

    [Property]
    public bool InterestDue_isWhatTheInterestCalculatorReturnsForTheRowsPeriod(
        uint principalCents, ushort rate, byte periods, byte convention, bool quarterly, ushort offset, byte gap)
    {
        var request = Request(principalCents, rate, periods, convention, quarterly, offset, gap);
        var schedule = Generator.Generate(request).Value;
        var rowPeriods = SchedulePeriods.Periods(
            request.DisbursementDate, request.FirstPaymentDate, request.PaymentFrequency, schedule.Rows.Count);

        return schedule.Rows.Select((row, i) =>
                row.InterestDue == Calculator.Calculate(request.Rate, row.OpeningBalance, rowPeriods[i].Start, rowPeriods[i].End)
                && row.PaymentDate == rowPeriods[i].End)
            .All(matches => matches);
    }

    [Property]
    public bool EveryRowButTheLast_paysTheSameTotal(
        uint principalCents, ushort rate, byte periods, byte convention, bool quarterly, ushort offset, byte gap)
    {
        var schedule = Generator.Generate(Request(principalCents, rate, periods, convention, quarterly, offset, gap)).Value;

        return schedule.Rows.Take(schedule.Rows.Count - 1).Select(row => row.TotalPayment).Distinct().Count() <= 1;
    }
}
