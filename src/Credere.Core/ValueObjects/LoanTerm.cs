using System.Globalization;
using Credere.Core.Results;
using NodaTime;

namespace Credere.Core.ValueObjects;

/// <summary>
/// The duration of a loan in whole months, from 1 to 600 (docs/domain/value-objects.md, "LoanTerm").
/// </summary>
public sealed record LoanTerm
{
    /// <summary>The shortest term, in months.</summary>
    public const int MinMonths = 1;

    /// <summary>The longest term, in months: a 50-year sanity ceiling.</summary>
    public const int MaxMonths = 600;

    private const int MonthsPerYear = 12;
    private const int MinYears = 1;
    private const int MaxYears = MaxMonths / MonthsPerYear;

    private LoanTerm(int months)
    {
        Months = months;
    }

    /// <summary>Gets the term in whole months.</summary>
    public int Months { get; }

    /// <summary>Creates a term from untrusted input.</summary>
    /// <param name="months">The number of months.</param>
    /// <returns>
    /// The term, or a <see cref="ValidationError"/> when <paramref name="months"/> is outside 1 to 600.
    /// </returns>
    public static Result<LoanTerm, DomainError> Create(int months) =>
        months is < MinMonths or > MaxMonths
            ? Result<LoanTerm, DomainError>.Failure(new ValidationError(
                "months",
                $"must be between {MinMonths} and {MaxMonths}, but was {months.ToString(CultureInfo.InvariantCulture)}"))
            : Result<LoanTerm, DomainError>.Success(new LoanTerm(months));

    /// <summary>Creates a term from trusted input.</summary>
    /// <param name="months">The number of months.</param>
    /// <returns>The term.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="months"/> is outside 1 to 600, which is a programmer error.</exception>
    public static LoanTerm FromMonths(int months)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(months, MinMonths);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(months, MaxMonths);
        return new LoanTerm(months);
    }

    /// <summary>Creates a term of whole years from trusted input: <c>FromYears(y)</c> is <c>FromMonths(y * 12)</c>.</summary>
    /// <param name="years">The number of years, from 1 to 50.</param>
    /// <returns>The term.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="years"/> gives a term outside 1 to 600 months, which is a programmer error.</exception>
    public static LoanTerm FromYears(int years)
    {
        // Checked against the year bounds first, so years * 12 can't overflow.
        ArgumentOutOfRangeException.ThrowIfLessThan(years, MinYears);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(years, MaxYears);
        return new LoanTerm(years * MonthsPerYear);
    }

    /// <summary>Adds the term to a date: the date that falls <see cref="Months"/> calendar months after <paramref name="start"/>.</summary>
    /// <remarks>
    /// Uses NodaTime's <see cref="LocalDate.PlusMonths(int)"/>, which keeps the day of the month and, when the target
    /// month is shorter, moves to its last day (31 January plus one month is 28 or 29 February). This is the interim
    /// month-end rule that docs/domain/aprc.md also uses, pending open question #9.
    /// </remarks>
    /// <param name="start">The start date, for example the disbursement date.</param>
    /// <returns>The date the term ends.</returns>
    public LocalDate AddTo(LocalDate start) => start.PlusMonths(Months);

    /// <inheritdoc />
    public override string ToString() => $"{Months.ToString(CultureInfo.InvariantCulture)} months";
}
