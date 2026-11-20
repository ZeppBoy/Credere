using Credere.Core.DayCounts;
using Credere.Core.Results;
using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Core.Rates;

/// <summary>
/// A fixed nominal rate applied as simple interest: <c>interest = balance × rate × dayCountFraction</c>
/// (docs/domain/rates.md, "FixedRate").
/// </summary>
/// <remarks>
/// <para>
/// Reference cases: the four Actual/Actual (ISDA) worked examples of ISDA, "EMU and Market Conventions: Recent
/// Developments" (ISDA-BS:9951.1, November 1998), §4, on £10,000 at 10%, and formula-derived cases for the five
/// conventions; see <c>FixedRateTests</c>.
/// </para>
/// <para>
/// The product is evaluated exactly, as a ratio of integers, and rounded once, half to even, when the result
/// <see cref="Money"/> is created (<see cref="ExactInterest"/>). A <see cref="decimal"/> evaluation would round a
/// repeating year fraction such as 20/360 to 28 digits and put an exact half-cent tie a hair above or below the
/// tie, and the order of the multiplication would change which cent it rounds to.
/// </para>
/// </remarks>
public sealed record FixedRate : InterestRate
{
    private const decimal MaxNominalRate = 100m;

    private FixedRate(Percentage nominalRate, DayCountConvention dayCountConvention)
    {
        NominalRate = nominalRate;
        DayCountConvention = dayCountConvention;
    }

    /// <inheritdoc />
    public override Percentage NominalRate { get; }

    /// <inheritdoc />
    public override DayCountConvention DayCountConvention { get; }

    /// <summary>Creates a fixed rate.</summary>
    /// <param name="nominalRate">The nominal rate, from 0% to 100%.</param>
    /// <param name="dayCountConvention">The day-count convention.</param>
    /// <returns>The rate, or a <see cref="ValidationError"/> on field <c>nominalRate</c> when it exceeds 100%.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>, which is a programmer error.</exception>
    public static Result<FixedRate, DomainError> Create(Percentage nominalRate, DayCountConvention dayCountConvention)
    {
        ArgumentNullException.ThrowIfNull(nominalRate);
        ArgumentNullException.ThrowIfNull(dayCountConvention);

        return nominalRate.Value > MaxNominalRate
            ? Result<FixedRate, DomainError>.Failure(new ValidationError("nominalRate", "must not exceed 100%"))
            : Result<FixedRate, DomainError>.Success(new FixedRate(nominalRate, dayCountConvention));
    }

    /// <inheritdoc />
    internal override Money CalculateInterest(Money balance, LocalDate periodStart, LocalDate periodEnd)
    {
        var yearFraction = DayCountConvention.ExactFraction(periodStart, periodEnd);

        return ExactInterest.Of(balance, NominalRate, yearFraction).ToMoney(balance.Currency);
    }

    /// <inheritdoc />
    public override string ToString() => $"{NominalRate} ({DayCountConvention.Name})";
}
