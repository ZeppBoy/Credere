using System.Globalization;
using Credere.Core.Results;

namespace Credere.Core.ValueObjects;

/// <summary>
/// A non-negative percentage, stored as the percent number: <c>5.25</c> means 5.25%
/// (docs/domain/value-objects.md, "Percentage").
/// </summary>
/// <remarks>
/// Passing a fraction such as <c>0.0525m</c> to the constructor would be a 100× error. Use
/// <see cref="FromFraction"/> for fractions and <see cref="Fraction"/> to read one back. No rounding happens in this
/// type: callers that need a rounded percentage round explicitly, with a stated <see cref="MidpointRounding"/>.
/// </remarks>
public sealed record Percentage
{
    private const decimal Hundred = 100m;

    /// <summary>
    /// Initializes a new instance of the <see cref="Percentage"/> class from trusted input.
    /// </summary>
    /// <param name="value">The percent number, for example <c>5.25</c> for 5.25%.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative, which is a programmer error.</exception>
    public Percentage(decimal value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the zero percentage.</summary>
    public static Percentage Zero => new(0m);

    /// <summary>Gets the percent number: 5.25 means 5.25%.</summary>
    public decimal Value { get; }

    /// <summary>Gets the value as a fraction: 5.25% is 0.0525.</summary>
    public decimal Fraction => Value / Hundred;

    /// <summary>Creates a percentage from untrusted input.</summary>
    /// <param name="value">The percent number, for example <c>5.25</c> for 5.25%.</param>
    /// <returns>The percentage, or a <see cref="ValidationError"/> when <paramref name="value"/> is negative.</returns>
    public static Result<Percentage, DomainError> Create(decimal value) =>
        value < 0m
            ? Result<Percentage, DomainError>.Failure(new ValidationError("value", "must not be negative"))
            : Result<Percentage, DomainError>.Success(new Percentage(value));

    /// <summary>Creates a percentage from a fraction: <c>0.0525</c> becomes 5.25%.</summary>
    /// <param name="fraction">The fraction.</param>
    /// <returns>The percentage.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fraction"/> is negative, which is a programmer error.</exception>
    public static Percentage FromFraction(decimal fraction)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fraction);
        return new Percentage(fraction * Hundred);
    }

    /// <summary>Adds two percentages, both as percent numbers.</summary>
    /// <param name="left">The first percentage.</param>
    /// <param name="right">The second percentage.</param>
    /// <returns>The sum.</returns>
    public static Percentage operator +(Percentage left, Percentage right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return new Percentage(left.Value + right.Value);
    }

    /// <summary>Subtracts one percentage from another, both as percent numbers.</summary>
    /// <param name="left">The percentage to subtract from.</param>
    /// <param name="right">The percentage to subtract.</param>
    /// <returns>The difference.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The difference would be negative, which is a programmer error.</exception>
    public static Percentage operator -(Percentage left, Percentage right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return new Percentage(left.Value - right.Value);
    }

    /// <summary>Determines whether <paramref name="left"/> is less than <paramref name="right"/>.</summary>
    /// <param name="left">The first percentage.</param>
    /// <param name="right">The second percentage.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is smaller.</returns>
    public static bool operator <(Percentage left, Percentage right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.Value < right.Value;
    }

    /// <summary>Determines whether <paramref name="left"/> is greater than <paramref name="right"/>.</summary>
    /// <param name="left">The first percentage.</param>
    /// <param name="right">The second percentage.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is larger.</returns>
    public static bool operator >(Percentage left, Percentage right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.Value > right.Value;
    }

    /// <inheritdoc />
    public override string ToString() =>
        // The pattern drops trailing zeros, so 5.250 and 5.25 read the same.
        Value.ToString("0.############################", CultureInfo.InvariantCulture) + "%";
}
