using System.Globalization;
using Credere.Core.Results;

namespace Credere.Core.ValueObjects;

/// <summary>
/// An amount of money in one currency, rounded to two decimal places with banker's rounding
/// (docs/domain/value-objects.md, "Money").
/// </summary>
/// <remarks>
/// <para>
/// Every <see cref="Money"/> that is created, including the result of <c>*</c> and <c>/</c>, is rounded once to two
/// places with <see cref="MidpointRounding.ToEven"/>: an exact tie goes to the even neighbour, so 0.125 becomes 0.12
/// and 0.135 becomes 0.14. This is the round-to-nearest, ties-to-even rule of IEEE 754-2019 §4.3.1
/// (roundTiesToEven), which .NET names <see cref="MidpointRounding.ToEven"/>; Microsoft Learn, "MidpointRounding
/// Enum", tabulates it. Because each creation is a rounding point, intermediate calculations stay in
/// <see cref="decimal"/> and a <see cref="Money"/> is produced only where a domain document says to round.
/// </para>
/// <para>
/// The stored scale is not forced to two: <c>100m</c> stays <c>100m</c>, and <see cref="ToString"/> shows two places.
/// Equality compares the numeric value, so <c>1.5</c> and <c>1.50</c> are equal. Mixing currencies in an operator
/// is a programmer error and throws <see cref="InvalidOperationException"/>; untrusted currencies are checked by
/// <see cref="Create"/>.
/// </para>
/// </remarks>
public sealed record Money
{
    private const int Places = 2;
    private const int CurrencyLength = 3;

    private Money(decimal amount, string currency)
    {
        Amount = Round(amount);
        Currency = currency;
    }

    /// <summary>Gets the amount, rounded to two decimal places.</summary>
    public decimal Amount { get; }

    /// <summary>Gets the currency: three uppercase ASCII letters, for example <c>EUR</c>.</summary>
    public string Currency { get; }

    /// <summary>Creates money from untrusted input, rounding the amount to two places half-even.</summary>
    /// <param name="amount">The amount. More than two decimal places are rounded, not rejected.</param>
    /// <param name="currency">The currency: exactly three uppercase ASCII letters (<c>^[A-Z]{3}$</c>), not normalised.</param>
    /// <returns>The money, or a <see cref="ValidationError"/> on field <c>currency</c> when the currency is invalid.</returns>
    public static Result<Money, DomainError> Create(decimal amount, string currency) =>
        IsValidCurrency(currency)
            ? Result<Money, DomainError>.Success(new Money(amount, currency))
            : Result<Money, DomainError>.Failure(new ValidationError(
                "currency",
                "must be exactly three uppercase letters A-Z, for example EUR"));

    /// <summary>Creates zero in a currency, from trusted input.</summary>
    /// <param name="currency">The currency: three uppercase ASCII letters.</param>
    /// <returns>Zero in <paramref name="currency"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="currency"/> is not three uppercase letters, which is a programmer error.</exception>
    public static Money Zero(string currency)
    {
        if (!IsValidCurrency(currency))
        {
            throw new ArgumentException(
                "The currency must be exactly three uppercase letters A-Z, for example EUR.",
                nameof(currency));
        }

        return new Money(0m, currency);
    }

    /// <summary>Adds two amounts in the same currency. The sum of two-place amounts is exact.</summary>
    /// <param name="left">The first amount.</param>
    /// <param name="right">The second amount.</param>
    /// <returns>The sum.</returns>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    public static Money operator +(Money left, Money right)
    {
        RequireSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    /// <summary>Subtracts an amount in the same currency. The difference of two-place amounts is exact.</summary>
    /// <param name="left">The amount to subtract from.</param>
    /// <param name="right">The amount to subtract.</param>
    /// <returns>The difference, which can be negative.</returns>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    public static Money operator -(Money left, Money right)
    {
        RequireSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    /// <summary>Multiplies an amount by a factor and rounds the product once, half-even, to two places.</summary>
    /// <param name="money">The amount.</param>
    /// <param name="factor">The factor, for example a rate as a fraction.</param>
    /// <returns>The rounded product.</returns>
    public static Money operator *(Money money, decimal factor)
    {
        ArgumentNullException.ThrowIfNull(money);
        return new Money(money.Amount * factor, money.Currency);
    }

    /// <summary>Multiplies an amount by a factor and rounds the product once, half-even, to two places.</summary>
    /// <param name="factor">The factor, for example a rate as a fraction.</param>
    /// <param name="money">The amount.</param>
    /// <returns>The rounded product.</returns>
    public static Money operator *(decimal factor, Money money) => money * factor;

    /// <summary>Divides an amount by a divisor and rounds the quotient once, half-even, to two places.</summary>
    /// <param name="money">The amount.</param>
    /// <param name="divisor">The divisor.</param>
    /// <returns>The rounded quotient.</returns>
    /// <exception cref="DivideByZeroException"><paramref name="divisor"/> is zero.</exception>
    public static Money operator /(Money money, decimal divisor)
    {
        ArgumentNullException.ThrowIfNull(money);
        return new Money(money.Amount / divisor, money.Currency);
    }

    /// <summary>Determines whether <paramref name="left"/> is less than <paramref name="right"/>.</summary>
    /// <param name="left">The first amount.</param>
    /// <param name="right">The second amount.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is smaller.</returns>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    public static bool operator <(Money left, Money right)
    {
        RequireSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    /// <summary>Determines whether <paramref name="left"/> is greater than <paramref name="right"/>.</summary>
    /// <param name="left">The first amount.</param>
    /// <param name="right">The second amount.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is larger.</returns>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    public static bool operator >(Money left, Money right)
    {
        RequireSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    /// <summary>Determines whether <paramref name="left"/> is less than or equal to <paramref name="right"/>.</summary>
    /// <param name="left">The first amount.</param>
    /// <param name="right">The second amount.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not larger.</returns>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    public static bool operator <=(Money left, Money right)
    {
        RequireSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    /// <summary>Determines whether <paramref name="left"/> is greater than or equal to <paramref name="right"/>.</summary>
    /// <param name="left">The first amount.</param>
    /// <param name="right">The second amount.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not smaller.</returns>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    public static bool operator >=(Money left, Money right)
    {
        RequireSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    /// <inheritdoc />
    public override string ToString() =>
        Amount.ToString("F2", CultureInfo.InvariantCulture) + " " + Currency;

    private static decimal Round(decimal amount) => Math.Round(amount, Places, MidpointRounding.ToEven);

    // Checked per character, not with a regex: the .NET "$" anchor also matches before a trailing newline.
    private static bool IsValidCurrency(string? currency)
    {
        if (currency is null || currency.Length != CurrencyLength)
        {
            return false;
        }

        foreach (var letter in currency)
        {
            if (letter is < 'A' or > 'Z')
            {
                return false;
            }
        }

        return true;
    }

    private static void RequireSameCurrency(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (!string.Equals(left.Currency, right.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot combine {left.Currency} with {right.Currency}. Convert explicitly first.");
        }
    }
}
