using Credere.Core.Results;
using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using NodaTime;
using Xunit;

namespace Credere.Core.Tests.Schedules;

/// <summary>
/// Tests the invariants of <see cref="AmortizationSchedule.Create"/> (docs/domain/schedules.md, "Public contract"),
/// each accepted once and violated once, and its element-wise equality (AGENTS.md §6). The schedules are small and
/// hand-built: 1,000.00 repaid in two rows.
/// </summary>
public sealed class AmortizationScheduleTests
{
    private static readonly Money Principal = Eur(1000m);

    private static Money Eur(decimal amount) => Money.Create(amount, "EUR").Value;

    private static ScheduleRow Row1() => new(
        1, new LocalDate(2026, 2, 28), Eur(1000m), Eur(10m), Eur(400m), Eur(410m), Eur(600m));

    private static ScheduleRow Row2() => new(
        2, new LocalDate(2026, 3, 31), Eur(600m), Eur(6m), Eur(600m), Eur(606m), Eur(0m));

    private static List<ScheduleRow> ValidRows() => [Row1(), Row2()];

    private static Result<AmortizationSchedule, DomainError> Create(IReadOnlyList<ScheduleRow> rows) =>
        AmortizationSchedule.Create(Principal, rows);

    private static void AssertViolation(IReadOnlyList<ScheduleRow> rows, string expectedPart)
    {
        var result = Create(rows);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal("rows", error.Field);
        Assert.Contains(expectedPart, error.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_validSchedule_exposesTheRowsAndThePrincipal()
    {
        var result = Create(ValidRows());

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidRows(), result.Value.Rows);
        Assert.Equal(Principal, result.Value.Principal);
    }

    [Fact]
    public void Create_singleRowSchedule_isValid()
    {
        var row = new ScheduleRow(1, new LocalDate(2026, 2, 28), Eur(1000m), Eur(10m), Eur(1000m), Eur(1010m), Eur(0m));

        Assert.True(Create([row]).IsSuccess);
    }

    [Fact]
    public void Create_copiesTheRows_soChangingTheSourceListDoesNotChangeTheSchedule()
    {
        var rows = ValidRows();
        var schedule = Create(rows).Value;

        rows.Clear();

        Assert.Equal(2, schedule.Rows.Count);
    }

    [Fact]
    public void Rows_cannotBeChangedThroughTheList()
    {
        var schedule = Create(ValidRows()).Value;

        Assert.True(schedule.Rows is System.Collections.IList { IsReadOnly: true });
    }

    [Fact]
    public void Create_noRows_isRejected()
    {
        AssertViolation([], "at least one row");
    }

    [Fact]
    public void Create_nullRow_isRejected()
    {
        AssertViolation([Row1(), null!], "row 2 is null");
    }

    [Fact]
    public void Create_firstOpeningBalanceDifferentFromThePrincipal_isRejected()
    {
        var rows = ValidRows();
        rows[0] = Row1() with { OpeningBalance = Eur(999.99m), ClosingBalance = Eur(599.99m) };
        rows[1] = Row2() with { OpeningBalance = Eur(599.99m) };

        AssertViolation(rows, "row 1 must equal the principal");
    }

    [Fact]
    public void Create_lastClosingBalanceNotZero_isRejected()
    {
        var rows = ValidRows();
        rows[1] = Row2() with { PrincipalDue = Eur(599.99m), TotalPayment = Eur(605.99m), ClosingBalance = Eur(0.01m) };

        AssertViolation(rows, "last row must be exactly zero");
    }

    [Fact]
    public void Create_closingBalanceThatIsNotOpeningMinusPrincipalDue_isRejected()
    {
        var rows = ValidRows();
        rows[0] = Row1() with { ClosingBalance = Eur(599m) };

        AssertViolation(rows, "row 1: the opening balance minus the principal due");
    }

    [Fact]
    public void Create_totalPaymentThatIsNotInterestPlusPrincipal_isRejected()
    {
        var rows = ValidRows();
        rows[1] = Row2() with { TotalPayment = Eur(607m) };

        AssertViolation(rows, "row 2: the interest due plus the principal due");
    }

    [Fact]
    public void Create_openingBalanceThatIsNotThePreviousClosingBalance_isRejected()
    {
        // Row 2 is internally consistent (700 - 700 = 0, 6 + 700 = 706) but does not continue from row 1's 600.
        var rows = ValidRows();
        rows[1] = Row2() with { OpeningBalance = Eur(700m), PrincipalDue = Eur(700m), TotalPayment = Eur(706m) };

        AssertViolation(rows, "row 2: the opening balance must equal the closing balance of row 1");
    }

    [Fact]
    public void Create_samePaymentDateTwice_isRejected()
    {
        var rows = ValidRows();
        rows[1] = Row2() with { PaymentDate = Row1().PaymentDate };

        AssertViolation(rows, "row 2: the payment date must be after the date of row 1");
    }

    [Fact]
    public void Create_paymentDatesThatDecrease_areRejected()
    {
        var rows = ValidRows();
        rows[1] = Row2() with { PaymentDate = new LocalDate(2026, 1, 31) };

        AssertViolation(rows, "row 2: the payment date must be after the date of row 1");
    }

    [Fact]
    public void Create_amountInAnotherCurrency_isRejected()
    {
        var rows = ValidRows();
        rows[1] = Row2() with { InterestDue = Money.Create(6m, "USD").Value };

        AssertViolation(rows, "row 2 has an amount that is not in EUR");
    }

    // Source for the next four tests: the reproducers in Codex's review of PR #43. Money arithmetic rounds to 28 digits
    // and throws near decimal.MaxValue, so the row equations are checked on exact cents.
    private static readonly Money Max = Eur(decimal.MaxValue);

    private static Result<AmortizationSchedule, DomainError> CreateOneRow(Money interest, Money total) =>
        AmortizationSchedule.Create(
            Max,
            [new ScheduleRow(1, new LocalDate(2026, 2, 28), Max, interest, Max, total, Eur(0m))]);

    [Fact]
    public void Create_totalPaymentOneCentOffAtTheMaximumAmount_isRejected()
    {
        // decimal.MaxValue + 0.01 cannot be held by a decimal, so addition used to drop the cent and accept the total.
        var result = CreateOneRow(Eur(0.01m), Max);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal("rows", error.Field);
        Assert.Contains("row 1: the interest due plus the principal due", error.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_totalPaymentBeyondTheMaximumAmount_isRejectedNotThrown()
    {
        // decimal.MaxValue + 1 used to throw OverflowException from the addition.
        var result = CreateOneRow(Eur(1m), Max);

        Assert.False(result.IsSuccess);
        Assert.Contains("row 1: the interest due plus the principal due", Assert.IsType<ValidationError>(result.Error).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_closingBalanceOneCentOffAtTheMaximumAmount_isRejected()
    {
        // MaxValue - 0.01 used to round back to MaxValue and equal the closing balance.
        var row = new ScheduleRow(1, new LocalDate(2026, 2, 28), Max, Eur(0m), Eur(0.01m), Eur(0.01m), Max);

        var result = AmortizationSchedule.Create(Max, [row]);

        Assert.False(result.IsSuccess);
        Assert.Contains("row 1: the opening balance minus the principal due", Assert.IsType<ValidationError>(result.Error).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_scheduleOfTheMaximumAmount_isValidWhenTheEquationsHoldExactly()
    {
        var result = CreateOneRow(Eur(0m), Max);

        Assert.True(result.IsSuccess);
        Assert.Equal(Max, result.Value.Principal);
    }

    [Fact]
    public void Create_nullArguments_throw()
    {
        Assert.Equal("principal", Assert.Throws<ArgumentNullException>(() => AmortizationSchedule.Create(null!, ValidRows())).ParamName);
        Assert.Equal("rows", Assert.Throws<ArgumentNullException>(() => AmortizationSchedule.Create(Principal, null!)).ParamName);
    }

    [Fact]
    public void Equality_comparesTheRowsOneByOne_notTheListReference()
    {
        var a = Create(ValidRows()).Value;
        var b = Create(ValidRows()).Value;

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_dependsOnTheRows()
    {
        // The hash of an empty HashCode is the baseline; a schedule with rows must not hash to it.
        Assert.NotEqual(new HashCode().ToHashCode(), Create(ValidRows()).Value.GetHashCode());
    }

    [Fact]
    public void Equality_differsWhenARowDiffers()
    {
        var other = ValidRows();
        other[0] = Row1() with { InterestDue = Eur(11m), TotalPayment = Eur(411m) };

        Assert.NotEqual(Create(ValidRows()).Value, Create(other).Value);
    }

    [Fact]
    public void Equality_differsWhenTheRowCountDiffers()
    {
        var single = new ScheduleRow(1, new LocalDate(2026, 2, 28), Eur(1000m), Eur(10m), Eur(1000m), Eur(1010m), Eur(0m));

        Assert.NotEqual(Create(ValidRows()).Value, Create([single]).Value);
    }

    [Fact]
    public void Equality_nullIsNotEqual()
    {
        Assert.False(Create(ValidRows()).Value.Equals(null));
    }

    [Fact]
    public void ToString_summarisesTheScheduleReadably()
    {
        Assert.Equal(
            "2 rows, principal 1000.00 EUR, 2026-02-28 to 2026-03-31",
            Create(ValidRows()).Value.ToString());
    }
}
