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
/// The product is evaluated left to right in <see cref="decimal"/>, principal times rate first, and rounded once
/// when the result <see cref="Money"/> is created. Multiplying the rate by the year fraction first can land a
/// hair below an exact half-cent tie, so a tie that must round up would round down. The final rounding goes through
/// <see cref="InterestRate.ToMoney"/>, which also removes the noise that a repeating day-count fraction leaves on an
/// exact tie.
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
        var interest = balance.Amount * NominalRate.Fraction * DayCountConvention.Fraction(periodStart, periodEnd);

        return ToMoney(interest, balance.Currency);
    }

    /// <inheritdoc />
    public override string ToString() => $"{NominalRate} ({DayCountConvention.Name})";
}
