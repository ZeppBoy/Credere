using Credere.Core.Results;
using Credere.Core.ValueObjects;
using NodaTime;
using NodaTime.Text;
using Xunit;

namespace Credere.Core.Tests.ValueObjects;

/// <summary>Tests <see cref="LoanTerm"/>.</summary>
public sealed class LoanTermTests
{
    /// <summary>Gets an ISO start date, a term in months, and the ISO date the term ends.</summary>
    // Formula-derived, worked by hand on the calendar: the day of the month is kept; when the target month is
    // shorter the date moves to its last day (NodaTime LocalDate.PlusMonths; the interim month-end rule of
    // docs/domain/aprc.md, pending open question #9).
    public static TheoryData<string, int, string> AddToVectors => new()
    {
        { "2024-01-15", 1, "2024-02-15" }, // Ordinary month.
        { "2024-01-15", 12, "2025-01-15" }, // One year.
        { "2024-01-31", 1, "2024-02-29" }, // 31 Jan to a leap February: last day, 29th.
        { "2023-01-31", 1, "2023-02-28" }, // 31 Jan to a common February: last day, 28th.
        { "2024-03-31", 1, "2024-04-30" }, // 31 Mar to a 30-day month.
        { "2024-12-31", 2, "2025-02-28" }, // Across a year end into a common February.
        { "2024-02-29", 12, "2025-02-28" }, // Leap day plus one year: last day of a common February.
        { "2024-02-29", 48, "2028-02-29" }, // Leap day plus four years: the leap day again.
        { "2024-01-30", 1, "2024-02-29" }, // 30 Jan to a leap February.
        { "2000-01-01", 600, "2050-01-01" }, // The longest term, 50 years.
        { "2024-05-31", 1, "2024-06-30" }, // 31 May to a 30-day month.
        { "2024-08-31", 6, "2025-02-28" }, // Six months from 31 Aug.
    };

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(600)]
    public void Create_acceptsMonthsFromOneToSixHundred(int months)
    {
        var result = LoanTerm.Create(months);

        Assert.True(result.IsSuccess);
        Assert.Equal(months, result.Value.Months);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(601)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Create_outsideTheRange_returnsAValidationError(int months)
    {
        var result = LoanTerm.Create(months);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal("months", error.Field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(601)]
    [InlineData(-12)]
    public void FromMonths_outsideTheRange_throws(int months)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanTerm.FromMonths(months));
    }

    [Theory]
    [InlineData(1, 12)]
    [InlineData(5, 60)]
    [InlineData(50, 600)]
    public void FromYears_isTwelveTimesTheYearsInMonths(int years, int months)
    {
        Assert.Equal(months, LoanTerm.FromYears(years).Months);
        Assert.Equal(LoanTerm.FromMonths(months), LoanTerm.FromYears(years));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void FromYears_outsideOneToFifty_throws_withoutOverflow(int years)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanTerm.FromYears(years));
    }

    [Theory]
    [MemberData(nameof(AddToVectors))]
    public void AddTo_movesTheDateByCalendarMonths_clampingToTheLastDay(string start, int months, string expected)
    {
        var startDate = LocalDatePattern.Iso.Parse(start).Value;
        var expectedDate = LocalDatePattern.Iso.Parse(expected).Value;

        Assert.Equal(expectedDate, LoanTerm.FromMonths(months).AddTo(startDate));
    }

    [Fact]
    public void Equality_isByMonths()
    {
        Assert.Equal(LoanTerm.FromMonths(24), LoanTerm.FromYears(2));
        Assert.NotEqual(LoanTerm.FromMonths(24), LoanTerm.FromMonths(25));
    }

    [Fact]
    public void ToString_readsAsMonths()
    {
        Assert.Equal("36 months", LoanTerm.FromMonths(36).ToString());
    }

    [Fact]
    public void Bounds_areOneAndSixHundredMonths()
    {
        Assert.Equal(1, LoanTerm.MinMonths);
        Assert.Equal(600, LoanTerm.MaxMonths);
        Assert.Equal(new LocalDate(2025, 1, 1), LoanTerm.FromMonths(1).AddTo(new LocalDate(2024, 12, 1)));
    }
}
