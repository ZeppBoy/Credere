using Credere.Core.DayCounts;
using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using NodaTime;
using Xunit;

namespace Credere.Core.Tests.Schedules;

/// <summary>
/// Tests <see cref="ScheduleRequest.Create"/> against the rules the owner decided on PR #43 (2026-11-20): the first
/// payment date is strictly after the disbursement date; a quarterly term is a multiple of 3 months; there is no
/// maximum length of the first period in M1.
/// </summary>
public sealed class ScheduleRequestTests
{
    private static readonly LocalDate Disbursement = new(2026, 1, 15);
    private static readonly LocalDate FirstPayment = new(2026, 2, 28);

    private static readonly Money Principal = Money.Create(10_000m, "EUR").Value;
    private static readonly InterestRate Rate = FixedRate.Create(new Percentage(5m), new Thirty360Isda()).Value;

    private static Result<ScheduleRequest, DomainError> Create(
        int termMonths = 24,
        PaymentFrequency frequency = PaymentFrequency.Monthly,
        LocalDate? disbursement = null,
        LocalDate? firstPayment = null) =>
        ScheduleRequest.Create(
            Principal,
            Rate,
            LoanTerm.FromMonths(termMonths),
            disbursement ?? Disbursement,
            firstPayment ?? FirstPayment,
            frequency);

    [Fact]
    public void Create_validMonthlyRequest_exposesTheInputs()
    {
        var result = Create();

        Assert.True(result.IsSuccess);
        var request = result.Value;
        Assert.Equal(Principal, request.Principal);
        Assert.Equal(Rate, request.Rate);
        Assert.Equal(24, request.Term.Months);
        Assert.Equal(Disbursement, request.DisbursementDate);
        Assert.Equal(FirstPayment, request.FirstPaymentDate);
        Assert.Equal(PaymentFrequency.Monthly, request.PaymentFrequency);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(600)]
    public void Create_monthly_acceptsAnyTerm(int termMonths)
    {
        Assert.True(Create(termMonths).IsSuccess);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(12)]
    [InlineData(600)]
    public void Create_quarterly_acceptsAMultipleOfThreeMonths(int termMonths)
    {
        Assert.True(Create(termMonths, PaymentFrequency.Quarterly).IsSuccess);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(599)]
    public void Create_quarterly_termThatIsNotAMultipleOfThree_returnsAValidationErrorOnTerm(int termMonths)
    {
        var result = Create(termMonths, PaymentFrequency.Quarterly);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal("term", error.Field);
        Assert.Contains("multiple of 3 months", error.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_firstPaymentOnTheDisbursementDate_returnsAValidationErrorOnFirstPaymentDate()
    {
        var result = Create(firstPayment: Disbursement);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal("firstPaymentDate", error.Field);
        Assert.Contains("after the disbursement date", error.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_firstPaymentBeforeTheDisbursementDate_returnsAValidationErrorOnFirstPaymentDate()
    {
        var result = Create(firstPayment: Disbursement.PlusDays(-1));

        Assert.False(result.IsSuccess);
        Assert.Equal("firstPaymentDate", Assert.IsType<ValidationError>(result.Error).Field);
    }

    [Fact]
    public void Create_firstPaymentTheDayAfterTheDisbursementDate_isAccepted()
    {
        Assert.True(Create(firstPayment: Disbursement.PlusDays(1)).IsSuccess);
    }

    [Fact]
    public void Create_veryLongFirstPeriod_isAccepted_becauseThereIsNoMaximumInM1()
    {
        Assert.True(Create(firstPayment: Disbursement.PlusYears(5)).IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    public void Create_undefinedFrequency_returnsAValidationErrorOnPaymentFrequency(int value)
    {
        var result = Create(frequency: (PaymentFrequency)value);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal("paymentFrequency", error.Field);
        Assert.Contains("Monthly or Quarterly", error.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_nullArguments_throw()
    {
        var term = LoanTerm.FromMonths(12);

        Assert.Throws<ArgumentNullException>(
            () => ScheduleRequest.Create(null!, Rate, term, Disbursement, FirstPayment, PaymentFrequency.Monthly));
        Assert.Throws<ArgumentNullException>(
            () => ScheduleRequest.Create(Principal, null!, term, Disbursement, FirstPayment, PaymentFrequency.Monthly));
        Assert.Throws<ArgumentNullException>(
            () => ScheduleRequest.Create(Principal, Rate, null!, Disbursement, FirstPayment, PaymentFrequency.Monthly));
    }

    [Fact]
    public void Equality_isByValue()
    {
        Assert.Equal(Create().Value, Create().Value);
        Assert.NotEqual(Create().Value, Create(termMonths: 12).Value);
        Assert.NotEqual(Create().Value, Create(frequency: PaymentFrequency.Quarterly).Value);
    }
}
