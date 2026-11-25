using Credere.Core.Aprc;
using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using NodaTime;
using Xunit;

namespace Credere.Core.Tests.Properties;

/// <summary>
/// Checks the fee-free annuity round trip in docs/domain/aprc.md, “Property-based invariants”.
/// Directive (EU) 2023/2225, Article 30 and Annex III Part I, equate the drawdown with the
/// discounted repayments. Remark (c) treats a year as 12 equal months.
/// </summary>
public sealed class AprcRoundTripTests
{
    // The production solver's bracket is narrower than 1e-10. Allow 1e-9 for its
    // residual stop and the conversion at the production discount-power boundary.
    private const decimal SolverTolerance = 0.000000001m;
    private const int MonthsPerYear = 12;

    private static readonly AnnuityScheduleGenerator Generator = new(new InterestCalculator());
    private static readonly AprcCalculator Calculator = new();

    [Property(MaxTest = 250)]
    public Property Calculate_roundedMonthlyAnnuity_matchesIndependentMonthlyDiscounting(
        uint principalCents, ushort rate, ushort term, ushort yearOffset, byte day)
    {
        var principal = Euro(1_000m + (principalCents % 1_000_000_000u) / 100m);
        var nominalRate = rate % 10_001 / 100m;
        var months = 1 + (term % LoanTerm.MaxMonths);
        var disbursement = new LocalDate(2026, 1, 1).PlusMonths(yearOffset % 120).PlusDays(day % 28);
        var generated = GenerateAnnuity(principal, nominalRate, months, disbursement);
        if (!generated.IsSuccess)
        {
            // A rounded instalment can repay a long, high-rate loan early. M1-23 rejects
            // that request, so there are no scheduled cash flows for this property.
            var error = Assert.IsType<ValidationError>(generated.Error);
            Assert.Equal("principal", error.Field);
            Assert.EndsWith("principal due must be less than the opening balance before maturity",
                error.Reason, StringComparison.Ordinal);
            return true.When(false);
        }

        var payments = AnnuityPayments(generated.Value, months, disbursement);
        var expected = SolveInMonthlyUnits(principal.Amount, payments);

        var actual = Calculate(principal, disbursement, payments);

        Assert.InRange(actual, expected - SolverTolerance, expected + SolverTolerance);
        return true.ToProperty();
    }

    [Fact]
    public void Calculate_roundedOneMonthAnnuity_matchesItsActualCashFlow()
    {
        // M1-20 counterexample to a universal one-basis-point tolerance on the ideal rate.
        // EUR 1000 at 99.99% for one equal month accrues EUR 83.325, rounded half-even to EUR 83.32.
        var principal = Euro(1_000m);
        var disbursement = new LocalDate(2026, 1, 15);
        var generated = GenerateAnnuity(principal, 99.99m, 1, disbursement);
        Assert.True(generated.IsSuccess, generated.IsSuccess ? string.Empty : generated.Error.ToString());
        var payments = AnnuityPayments(generated.Value, 1, disbursement);
        Assert.Equal(Euro(1_083.32m), Assert.Single(payments).Amount);

        var actual = Calculate(principal, disbursement, payments);
        var expected = Annualise(1_083.32m / 1_000m);

        Assert.InRange(actual, expected - SolverTolerance, expected + SolverTolerance);
    }

    private static Result<AmortizationSchedule, DomainError> GenerateAnnuity(
        Money principal, decimal ratePercent, int months, LocalDate disbursement)
    {
        var rate = FixedRate.Create(new Percentage(ratePercent), new ThirtyE360()).Value;
        var request = ScheduleRequest.Create(principal, rate, LoanTerm.FromMonths(months), disbursement,
            disbursement.PlusMonths(1), PaymentFrequency.Monthly).Value;
        return Generator.Generate(request);
    }

    private static CashFlow[] AnnuityPayments(AmortizationSchedule schedule, int months, LocalDate disbursement)
    {
        Assert.Equal(months, schedule.Rows.Count);
        return schedule.Rows.Select((row, index) =>
        {
            Assert.Equal(disbursement.PlusMonths(index + 1), row.PaymentDate);
            return new CashFlow(row.TotalPayment, row.PaymentDate);
        }).ToArray();
    }

    private static decimal Calculate(Money principal, LocalDate disbursement, CashFlow[] payments)
    {
        var result = Calculator.Calculate(new AprcRequest([new CashFlow(principal, disbursement)], payments));
        Assert.True(result.IsSuccess, result.IsSuccess ? string.Empty : result.Error.ToString());
        return result.Value.Rate;
    }

    private static decimal SolveInMonthlyUnits(decimal drawdown, CashFlow[] payments)
    {
        // Solve for the monthly discount factor using a backwards present-value recurrence.
        // The production calculator instead solves in annual units and raises to fractional powers.
        // These monthly payments bracket a factor between 1 and 2. The upper factor is deliberately loose.
        var lower = 1m;
        var upper = 2m;
        Assert.True(PresentValue(payments, lower) >= drawdown);
        Assert.True(PresentValue(payments, upper) <= drawdown);

        for (var iteration = 0; iteration < 60; iteration++)
        {
            var midpoint = (lower + upper) / 2m;
            if (PresentValue(payments, midpoint) > drawdown)
            {
                lower = midpoint;
            }
            else
            {
                upper = midpoint;
            }
        }

        return Annualise((lower + upper) / 2m);
    }

    private static decimal PresentValue(CashFlow[] payments, decimal monthlyFactor)
    {
        var presentValue = 0m;
        for (var index = payments.Length - 1; index >= 0; index--)
        {
            presentValue = (presentValue + payments[index].Amount.Amount) / monthlyFactor;
        }

        return presentValue;
    }

    private static decimal Annualise(decimal monthlyFactor)
    {
        var annualFactor = 1m;
        for (var month = 0; month < MonthsPerYear; month++)
        {
            annualFactor *= monthlyFactor;
        }

        return annualFactor - 1m;
    }

    private static Money Euro(decimal amount) => Money.Create(amount, "EUR").Value;
}
