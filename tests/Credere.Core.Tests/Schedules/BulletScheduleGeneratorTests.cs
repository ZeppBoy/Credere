using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.Schedules;
using FsCheck;
using FsCheck.Xunit;
using Xunit;
using static Credere.Core.Tests.Schedules.BulletScheduleGeneratorVectorAssertions;

namespace Credere.Core.Tests.Schedules;

/// <summary>Checks the bullet invariants under both modes documented in docs/domain/schedules.md, §3.</summary>
public sealed class BulletScheduleGeneratorTests
{
    private static readonly InterestCalculator Calculator = new();

    [Property]
    public void Generate_validPrincipalAndTerm_preservesTheAccrualAndBalance(
        PositiveInt amount, PositiveInt count, bool quarterly, bool capitaliseInterest)
    {
        var rowCount = 1 + (count.Get % 60);
        var principal = (1 + (amount.Get % 100_000)) / 100m;
        var frequency = quarterly ? PaymentFrequency.Quarterly : PaymentFrequency.Monthly;
        var months = quarterly ? rowCount * 3 : rowCount;
        var request = Request(principal, 5m, "30E/360", months, frequency,
            Date("2026-01-15"), quarterly ? Date("2026-04-15") : Date("2026-02-15"));
        var generator = new BulletScheduleGenerator(Calculator, capitaliseInterest);
        var rows = RequireSchedule(generator, request).Rows;
        var zero = Euro(0m);

        Assert.Equal(rowCount, rows.Count);
        Assert.Equal(principal + rows.Sum(row => row.CapitalisedInterest.Amount), rows.Sum(row => row.PrincipalDue.Amount));
        Assert.Equal(zero, rows[^1].CapitalisedInterest);
        Assert.Equal(zero, rows[^1].ClosingBalance);
        Assert.All(rows.Take(rowCount - 1), row =>
        {
            Assert.Equal(zero, row.PrincipalDue);
            if (capitaliseInterest)
            {
                Assert.Equal(zero, row.InterestDue);
                Assert.Equal(zero, row.TotalPayment);
            }
            else
            {
                Assert.Equal(zero, row.CapitalisedInterest);
                Assert.Equal(request.Principal, row.ClosingBalance);
            }
        });

        var previousDate = request.DisbursementDate;
        foreach (var row in rows)
        {
            var accrual = Calculator.Calculate(request.Rate, row.OpeningBalance, previousDate, row.PaymentDate);
            Assert.Equal(accrual, row.InterestDue + row.CapitalisedInterest);
            previousDate = row.PaymentDate;
        }
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void Generate_oneCentAboveDecimalLimit_returnsAValidationErrorOnPrincipal(bool capitaliseInterest, int months)
    {
        // At the decimal limit, this nominal rate accrues one cent over a full 30E/360 month.
        // That cent cannot be added to the opening amount, either as cash due or as capitalised interest.
        const decimal ratePercent = 0.0000000000000000000000000001m;
        var request = Request(decimal.MaxValue, ratePercent, "30E/360", months, PaymentFrequency.Monthly,
            Date("2026-01-15"), Date("2026-02-15"));
        Assert.Equal(Euro(0.01m),
            Calculator.Calculate(request.Rate, request.Principal, request.DisbursementDate, request.FirstPaymentDate));

        var result = new BulletScheduleGenerator(Calculator, capitaliseInterest).Generate(request);

        Assert.Equal("principal", Assert.IsType<ValidationError>(result.Error).Field);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Generate_zeroRateAtDecimalLimit_paysThePrincipalExactly(bool capitaliseInterest)
    {
        var request = Request(decimal.MaxValue, 0m, "30E/360", 2, PaymentFrequency.Monthly,
            Date("2026-01-15"), Date("2026-02-15"));
        var rows = RequireSchedule(new BulletScheduleGenerator(Calculator, capitaliseInterest), request).Rows;

        Assert.Equal(Euro(0m), rows[0].TotalPayment);
        Assert.Equal(request.Principal, rows[^1].TotalPayment);
        Assert.Equal(Euro(0m), rows[^1].ClosingBalance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Generate_negativeOriginalPrincipal_returnsAValidationErrorOnPrincipal(bool capitaliseInterest)
    {
        var request = Request(-1m, 0m, "30E/360", 1, PaymentFrequency.Monthly,
            Date("2026-01-15"), Date("2026-02-15"));

        var result = new BulletScheduleGenerator(Calculator, capitaliseInterest).Generate(request);

        Assert.Equal("principal", Assert.IsType<ValidationError>(result.Error).Field);
    }
}
