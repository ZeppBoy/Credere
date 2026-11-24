using Credere.Core.Rates;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using Xunit;
using static Credere.Core.Tests.Schedules.AnnuityScheduleGeneratorVectorRequests;

namespace Credere.Core.Tests.Schedules;

/// <summary>
/// Checks the annuity reference schedules (<see cref="AnnuityScheduleGeneratorVectors"/>) against the types that
/// already exist, before <c>AnnuityScheduleGenerator</c> is written: each vector must form a valid request and a valid
/// <see cref="AmortizationSchedule"/>, its dates must be the ones of <c>SchedulePeriods</c>, and its interest must be
/// what <see cref="InterestCalculator"/> returns for the row's period. The instalment itself is not checked here,
/// because that would repeat the generator's formula; the generator tests compare it with the vectors.
/// </summary>
public sealed class AnnuityScheduleGeneratorVectorTests
{
    private static readonly InterestCalculator Calculator = new();

    /// <summary>Gets the vector names.</summary>
    public static TheoryData<string> Names
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var vector in AnnuityScheduleGeneratorVectors.All)
            {
                names.Add(vector.Name);
            }

            return names;
        }
    }

    [Fact]
    public void Vectors_coverTheRequiredCases()
    {
        var all = AnnuityScheduleGeneratorVectors.All;

        Assert.True(all.Count >= 10);
        Assert.Equal(all.Count, all.Select(v => v.Name).Distinct(StringComparer.Ordinal).Count());
        foreach (var convention in new[] { "30E/360", "30/360 ISDA", "ACT/360", "ACT/365 Fixed", "ACT/ACT (ISDA)" })
        {
            Assert.Contains(all, v => v.Convention == convention);
        }

        Assert.Contains(all, v => v.Frequency == PaymentFrequency.Quarterly);
        Assert.Contains(all, v => v.RatePercent == 0m);
        Assert.Contains(all, v => v.Rows.Count == 1);
        Assert.Contains(all, v => v.Rows.Count == 360);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Vector_isAValidRequestAndAValidSchedule(string name)
    {
        var vector = Find(name);

        var request = RequestOf(vector);
        var schedule = AmortizationSchedule.Create(Eur(vector.Principal), RowsOf(vector));

        Assert.True(request.IsSuccess, request.IsSuccess ? string.Empty : request.Error.ToString());
        Assert.True(schedule.IsSuccess, schedule.IsSuccess ? string.Empty : schedule.Error.ToString());
        Assert.Equal(vector.Months * (int)vector.Frequency / 12, vector.Rows.Count);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Vector_datesAndInterestMatchSchedulePeriodsAndTheInterestCalculator(string name)
    {
        var vector = Find(name);
        var rate = FixedRate.Create(new Percentage(vector.RatePercent), Convention(vector.Convention)).Value;
        var periods = SchedulePeriods.Periods(Date(vector.Disbursement), Date(vector.FirstPayment), vector.Frequency, vector.Rows.Count);

        for (var i = 0; i < vector.Rows.Count; i++)
        {
            var row = vector.Rows[i];
            var interest = Calculator.Calculate(rate, Eur(row.OpeningBalance), periods[i].Start, periods[i].End);

            Assert.Equal(Date(row.PaymentDate), periods[i].End);
            Assert.True(interest.Amount == row.InterestDue, $"{name} row {i + 1}: calculator {interest.Amount}, vector {row.InterestDue}");
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Vector_regularRowsPayTheInstalment_andTheLastRowPaysItsInterestAndTheBalance(string name)
    {
        var vector = Find(name);
        var last = vector.Rows[^1];

        foreach (var row in vector.Rows.Take(vector.Rows.Count - 1))
        {
            Assert.Equal(vector.Instalment, row.TotalPayment);
        }

        Assert.Equal(last.OpeningBalance, last.PrincipalDue);
        Assert.Equal(last.InterestDue + last.OpeningBalance, last.TotalPayment);
        Assert.Equal(0m, last.ClosingBalance);
        Assert.Equal(vector.Principal, vector.Rows.Sum(r => r.PrincipalDue));
    }

    [Fact]
    public void FailureVectors_areValidRequests_soOnlyTheGeneratorCanRejectThem()
    {
        Assert.NotEmpty(AnnuityScheduleGeneratorFailureVectors.All);
        foreach (var failure in AnnuityScheduleGeneratorFailureVectors.All)
        {
            var request = RequestOf(failure);

            Assert.True(request.IsSuccess, failure.Name);
        }
    }

    [Fact]
    public void LongVector_reproducesTheResidualExampleOfTheScheduleDoc()
    {
        // docs/domain/schedules.md, "Rounding and residuals": 250,000 at 12%, 360 monthly rows, 30E/360, payment day <= 28.
        var vector = Find("Level30E360_360m");

        Assert.Equal(2571.53m, vector.Instalment);
        Assert.Equal(2576.59m, vector.Rows[^1].TotalPayment);
    }

    private static AnnuityScheduleGeneratorVector Find(string name) =>
        AnnuityScheduleGeneratorVectors.All.Single(v => string.Equals(v.Name, name, StringComparison.Ordinal));

    private static List<ScheduleRow> RowsOf(AnnuityScheduleGeneratorVector vector) =>
        vector.Rows.Select((row, i) => new ScheduleRow(
            i + 1,
            Date(row.PaymentDate),
            Eur(row.OpeningBalance),
            Eur(0m),
            Eur(row.InterestDue),
            Eur(row.PrincipalDue),
            Eur(row.TotalPayment),
            Eur(row.ClosingBalance))).ToList();
}
