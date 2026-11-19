using Credere.Core.ValueObjects;
using FsCheck.Xunit;

namespace Credere.Core.Tests.ValueObjects;

/// <summary>
/// Checks the algebra of <see cref="Money"/> that docs/domain/value-objects.md lists, over arbitrary two-place amounts
/// in one currency.
/// </summary>
/// <remarks>Amounts are built from whole cents, so every input is exact in <see cref="decimal"/>.</remarks>
public sealed class MoneyPropertiesTests
{
    private const string Eur = "EUR";

    // Cents are kept to about +/- 10^13 so sums of three amounts stay far inside decimal's range.
    private static Money Eu(long cents) => Money.Create((cents % 10_000_000_000_000L) / 100m, Eur).Value;

    [Property]
    public bool Addition_isAssociative(long a, long b, long c)
    {
        var (x, y, z) = (Eu(a), Eu(b), Eu(c));

        return (x + y) + z == x + (y + z);
    }

    [Property]
    public bool Addition_isCommutative(long a, long b)
    {
        var (x, y) = (Eu(a), Eu(b));

        return x + y == y + x;
    }

    [Property]
    public bool Addition_zeroIsTheIdentity(long a)
    {
        var x = Eu(a);

        return x + Money.Zero(Eur) == x && Money.Zero(Eur) + x == x;
    }

    [Property]
    public bool Subtraction_cancelsAddition_exactly(long a, long b)
    {
        var (x, y) = (Eu(a), Eu(b));

        return (x + y) - y == x;
    }

    [Property]
    public bool Subtraction_ofItself_isZero(long a)
    {
        var x = Eu(a);

        return x - x == Money.Zero(Eur);
    }

    [Property]
    public bool Multiplication_byOne_isTheIdentity(long a)
    {
        var x = Eu(a);

        return x * 1m == x && 1m * x == x;
    }

    [Property]
    public bool Multiplication_byZero_isZero(long a)
    {
        return Eu(a) * 0m == Money.Zero(Eur);
    }

    [Property]
    public bool Division_byOne_isTheIdentity(long a)
    {
        var x = Eu(a);

        return x / 1m == x;
    }

    [Property]
    public bool Create_isIdempotent_onceRounded(long thousandths)
    {
        // Thousandths give amounts with a third decimal place, so rounding really happens.
        var once = Money.Create((thousandths % 1_000_000_000_000_000L) / 1000m, Eur).Value;
        var twice = Money.Create(once.Amount, Eur).Value;

        return once == twice;
    }

    [Property]
    public bool Create_roundsToTheNearestCent_withinHalfACent(long thousandths)
    {
        var raw = (thousandths % 1_000_000_000_000_000L) / 1000m;
        var rounded = Money.Create(raw, Eur).Value.Amount;

        return Math.Abs(rounded - raw) <= 0.005m && rounded == Math.Round(rounded, 2, MidpointRounding.ToEven);
    }

    [Property]
    public bool Ordering_isTotalAndConsistentWithAmount(long a, long b)
    {
        var (x, y) = (Eu(a), Eu(b));

        return (x < y) == (x.Amount < y.Amount)
            && (x > y) == (x.Amount > y.Amount)
            && (x <= y) == !(x > y)
            && (x >= y) == !(x < y);
    }
}
