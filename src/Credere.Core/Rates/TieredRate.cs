using System.Collections.ObjectModel;
using Credere.Core.DayCounts;
using Credere.Core.Results;
using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Core.Rates;

/// <summary>Calculates simple interest using balance-based tiers.</summary>
/// <remarks>
/// Whole-balance and marginal tiering correspond to Methods A and B in 12 CFR part 1030, Appendix A, Part I.D.
/// Credere uses simple interest with the selected day-count convention and rounds the total to cents.
/// </remarks>
public sealed record TieredRate : InterestRate
{
    private TieredRate(ReadOnlyCollection<Tier> tiers, DayCountConvention dayCountConvention, TieredApplicationMode mode)
    {
        Tiers = tiers;
        DayCountConvention = dayCountConvention;
        Mode = mode;
    }

    /// <summary>Gets the tiers in increasing upper-limit order.</summary>
    public IReadOnlyList<Tier> Tiers { get; }

    /// <summary>Gets the rule used to apply the rates to the balance.</summary>
    public TieredApplicationMode Mode { get; }

    /// <inheritdoc />
    public override DayCountConvention DayCountConvention { get; }

    /// <summary>Gets the quoted rate of the final, unbounded tier.</summary>
    public override Percentage NominalRate => Tiers[^1].Rate;

    /// <summary>Creates a tiered rate from an ordered list of tiers.</summary>
    /// <param name="tiers">The tiers, ending in the unbounded tier. Finite upper limits are inclusive.</param>
    /// <param name="dayCountConvention">The convention used to measure the interest period.</param>
    /// <param name="mode">The rule used to apply the rates to the balance.</param>
    /// <returns>A tiered rate or a validation error for invalid tiers or mode.</returns>
    /// <exception cref="ArgumentNullException">The tier list or day-count convention is null.</exception>
    public static Result<TieredRate, DomainError> Create(
        IReadOnlyList<Tier> tiers, DayCountConvention dayCountConvention, TieredApplicationMode mode)
    {
        ArgumentNullException.ThrowIfNull(tiers);
        ArgumentNullException.ThrowIfNull(dayCountConvention);

        if (!Enum.IsDefined(mode))
        {
            return Result<TieredRate, DomainError>.Failure(new ValidationError("mode", "must be a defined tiering mode"));
        }

        var copy = tiers.ToArray();
        if (copy.Length == 0 || copy.Any(static tier => tier?.Rate is null) || copy[^1].UpperLimit is not null)
        {
            return InvalidTiers("each tier must have a rate and the list must end with an unbounded tier");
        }

        Money? previous = null;
        foreach (var tier in copy.AsSpan(0, copy.Length - 1))
        {
            if (tier.UpperLimit is not { Amount: > 0m } limit)
            {
                return InvalidTiers("each tier before the final tier must have a positive upper limit");
            }

            if (previous is not null && (limit.Currency != previous.Currency || limit.Amount <= previous.Amount))
            {
                return InvalidTiers("finite upper limits must strictly increase and use a single currency");
            }

            previous = limit;
        }

        return Result<TieredRate, DomainError>.Success(new TieredRate(Array.AsReadOnly(copy), dayCountConvention, mode));
    }

    /// <inheritdoc />
    internal override Money CalculateInterest(Money balance, LocalDate periodStart, LocalDate periodEnd)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(balance.Amount, nameof(balance));
        if (Tiers[0].UpperLimit is { } limit && limit.Currency != balance.Currency)
        {
            throw new InvalidOperationException("The balance and tier limits must use the same currency.");
        }

        var fraction = DayCountConvention.ExactFraction(periodStart, periodEnd);
        if (Mode == TieredApplicationMode.Marginal)
        {
            return MarginalInterest(balance, fraction).ToMoney(balance.Currency);
        }

        var tier = Tiers.First(tier => tier.UpperLimit is null || balance.Amount <= tier.UpperLimit.Amount);
        return ExactInterest.Of(balance, tier.Rate, fraction).ToMoney(balance.Currency);
    }

    /// <inheritdoc />
    public bool Equals(TieredRate? other) =>
        other is not null && (ReferenceEquals(this, other)
            || (Mode == other.Mode && DayCountConvention == other.DayCountConvention && Tiers.SequenceEqual(other.Tiers)));

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Mode);
        hash.Add(DayCountConvention);
        foreach (var tier in Tiers)
        {
            hash.Add(tier);
        }

        return hash.ToHashCode();
    }

    private ExactInterest MarginalInterest(Money balance, ExactYearFraction fraction)
    {
        var interest = ExactInterest.Zero;
        var negativeFraction = new ExactYearFraction(-fraction.Numerator, fraction.Denominator);
        Money? lower = null;
        foreach (var tier in Tiers)
        {
            var cap = tier.UpperLimit;
            var upper = cap is not null && cap.Amount < balance.Amount ? cap : balance;
            interest += ExactInterest.Of(upper, tier.Rate, fraction);
            if (lower is not null)
            {
                // Subtract the lower-limit contribution exactly, even when the slice would lose cents as a decimal.
                interest += ExactInterest.Of(lower, tier.Rate, negativeFraction);
            }

            if (upper == balance)
            {
                break;
            }

            lower = upper;
        }

        return interest;
    }

    private static Result<TieredRate, DomainError> InvalidTiers(string reason) =>
        Result<TieredRate, DomainError>.Failure(new ValidationError("tiers", reason));

    /// <summary>Specifies the rate and upper limit of a tier.</summary>
    /// <param name="Rate">The rate applied by this tier.</param>
    /// <param name="UpperLimit">The inclusive upper limit, or null for the final tier.</param>
    public sealed record Tier(Percentage Rate, Money? UpperLimit);
}
