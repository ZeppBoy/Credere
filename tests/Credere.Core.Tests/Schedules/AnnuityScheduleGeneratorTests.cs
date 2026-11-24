using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using Xunit;
using static Credere.Core.Tests.Schedules.AnnuityScheduleGeneratorVectorRequests;

namespace Credere.Core.Tests.Schedules;

/// <summary>
/// Tests <see cref="AnnuityScheduleGenerator"/> against the reference schedules in
/// <see cref="AnnuityScheduleGeneratorVectors"/>: every row's date and every amount. The vectors were recomputed by a
/// reviewer from another vendor and approved by the owner on PR #51 before the generator was written
/// (docs/domain/schedules.md, "1. Annuity"; ISDA 2006 Definitions §4.16 for the day counts).
/// </summary>
public sealed class AnnuityScheduleGeneratorTests
{
    private static readonly AnnuityScheduleGenerator Generator = new(new InterestCalculator());

    /// <summary>Gets the vector names.</summary>
    public static TheoryData<string> Names => AnnuityScheduleGeneratorVectorTests.Names;

    [Theory]
    [MemberData(nameof(Names))]
    public void Generate_matchesTheReferenceSchedule_row_by_row(string name)
    {
        var vector = AnnuityScheduleGeneratorVectors.All.Single(v => string.Equals(v.Name, name, StringComparison.Ordinal));
        var request = RequestOf(vector).Value;

        var result = Generator.Generate(request);

        Assert.True(result.IsSuccess, result.IsSuccess ? string.Empty : result.Error.ToString());
        var rows = result.Value.Rows;
        Assert.Equal(vector.Rows.Count, rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var expected = vector.Rows[i];
            var actual = rows[i];
            var where = $"{name} row {i + 1}";

            Assert.True(i + 1 == actual.Index, where + " index");
            Assert.True(Date(expected.PaymentDate) == actual.PaymentDate, where + " date");
            Assert.True(expected.OpeningBalance == actual.OpeningBalance.Amount, where + " opening");
            Assert.True(expected.InterestDue == actual.InterestDue.Amount, where + $" interest {actual.InterestDue.Amount} vs {expected.InterestDue}");
            Assert.True(expected.PrincipalDue == actual.PrincipalDue.Amount, where + $" principal {actual.PrincipalDue.Amount} vs {expected.PrincipalDue}");
            Assert.True(expected.TotalPayment == actual.TotalPayment.Amount, where + $" total {actual.TotalPayment.Amount} vs {expected.TotalPayment}");
            Assert.True(expected.ClosingBalance == actual.ClosingBalance.Amount, where + " closing");
            Assert.Equal("EUR", actual.TotalPayment.Currency);
        }
    }

    [Fact]
    public void Generate_instalmentAboveDecimalMaxValue_returnsAValidationErrorOnPrincipal_insteadOfOverflowing()
    {
        // One row at 100%: the instalment is principal x (1 + 100% / 12), which no decimal can hold.
        var result = Generator.Generate(MaxPrincipalRequest(months: 1, ratePercent: 100m));

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal("principal", error.Field);
    }

    [Fact]
    public void Generate_amountsTooLargeForExactCents_returnsAValidationError_insteadOfAWrongSchedule()
    {
        // Nothing overflows here, but the amounts are too large to keep cents, so the schedule's own exact row check rejects it
        // (docs/domain/schedules.md, "Public contract").
        var result = Generator.Generate(MaxPrincipalRequest(months: 12, ratePercent: 12m));

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public void Generate_oneZeroRateRowAtDecimalMaxValue_paysTheWholePrincipalExactly()
    {
        // decimal.MaxValue is representable, so a single zero-rate payment of exactly that amount is a valid schedule. Converting
        // the cents with (decimal)cents / 100 would overflow here (Codex review of PR #51, at 04c23cb).
        var result = Generator.Generate(MaxPrincipalRequest(months: 1, ratePercent: 0m));

        Assert.True(result.IsSuccess, result.IsSuccess ? string.Empty : result.Error.ToString());
        var row = Assert.Single(result.Value.Rows);
        Assert.Equal(decimal.MaxValue, row.TotalPayment.Amount);
        Assert.Equal(decimal.MaxValue, row.PrincipalDue.Amount);
        Assert.Equal(0m, row.ClosingBalance.Amount);
    }

    /// <summary>Gets the names of the requests that must be rejected.</summary>
    public static TheoryData<string> FailureNames
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var failure in AnnuityScheduleGeneratorFailureVectors.All)
            {
                names.Add(failure.Name);
            }

            return names;
        }
    }

    [Theory]
    [MemberData(nameof(FailureNames))]
    public void Generate_instalmentWouldRepayTheBalanceBeforeTheLastRow_returnsAValidationErrorOnPrincipal(string name)
    {
        // Owner decision on PR #51 and M1-23: a regular row whose principal reaches its opening balance (>=, so no
        // zero-payment last row) makes the request a ValidationError on "principal".
        var failure = AnnuityScheduleGeneratorFailureVectors.All.Single(v => string.Equals(v.Name, name, StringComparison.Ordinal));
        var request = RequestOf(failure).Value;

        var result = Generator.Generate(request);

        Assert.False(result.IsSuccess, name);
        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal("principal", error.Field);
    }

    [Fact]
    public void Generate_longFirstPeriod_allowsANegativeFirstPrincipal()
    {
        // 38,974 at 40% (Act/360), first payment five months after disbursement: the first period's interest exceeds the
        // instalment, so row 1 adds to the balance. M1-23 keeps this allowed; only principal >= opening balance on a regular
        // row is rejected. Only the sign is asserted; the amounts are not a reviewed vector.
        var request = ScheduleRequest.Create(
            Eur(38974m),
            FixedRate.Create(new Percentage(40m), new Act360()).Value,
            LoanTerm.FromMonths(24),
            Date("2026-10-13"),
            Date("2027-03-13"),
            PaymentFrequency.Monthly).Value;

        var result = Generator.Generate(request);

        Assert.True(result.IsSuccess, result.IsSuccess ? string.Empty : result.Error.ToString());
        Assert.True(result.Value.Rows[0].PrincipalDue.Amount < 0m);
        Assert.True(result.Value.Rows[0].ClosingBalance.Amount > result.Value.Rows[0].OpeningBalance.Amount);
    }

    private static ScheduleRequest MaxPrincipalRequest(int months, decimal ratePercent) =>
        ScheduleRequest.Create(
            Money.Create(decimal.MaxValue, "EUR").Value,
            FixedRate.Create(new Percentage(ratePercent), new ThirtyE360()).Value,
            LoanTerm.FromMonths(months),
            Date("2026-01-15"),
            Date("2026-02-15"),
            PaymentFrequency.Monthly).Value;

    [Fact]
    public void Generate_throughTheInterface_returnsTheSameSchedule()
    {
        var viaInterface = Assert.IsAssignableFrom<IScheduleGenerator>(Generator);
        var request = RequestOf(AnnuityScheduleGeneratorVectors.All[0]).Value;

        Assert.Equal(Generator.Generate(request).Value, viaInterface.Generate(request).Value);
    }

    [Fact]
    public void Generate_nullRequest_throws()
    {
        Assert.Throws<ArgumentNullException>(() => Generator.Generate(null!));
    }

    [Fact]
    public void Constructor_nullCalculator_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AnnuityScheduleGenerator(null!));
    }
}
