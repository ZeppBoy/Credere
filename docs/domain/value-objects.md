# Value objects

Credere's shared kernel exposes a small set of immutable value objects. Every domain method that would otherwise accept a `decimal`, `int`, or raw `string` of currency uses one of these instead.

All live in `src/Credere.Core/ValueObjects/`. All are `sealed record`s with get-only properties, and all override `ToString()` for readable logs.

**Construction and failure semantics** follow [ADR-0006](../adr/0006-error-semantics.md):

- **Untrusted input** (anything from a command, request, or file) goes through a static `Create(...)` that returns `Result<T, DomainError>`. An invalid amount, an empty currency, or a currency mismatch at the boundary is an expected failure, so it is reported as a `ValidationError`, never thrown.
- **Constructors and operators** assume validated input. They still check invariants, but a violation there is a programmer error and throws. This covers mixing currencies in `+`, which can only happen inside the domain after validation was skipped.
- **No positional parameters** on a type with invariants. A positional record's `with` expression goes through the copy constructor and would bypass validation.

## `Money`

An amount in a specific currency.

```csharp
public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public static Result<Money, DomainError> Create(decimal amount, string currency);
    public static Money Zero(string currency);

    // Arithmetic (same-currency only; cross-currency is a programmer error and throws).
    public static Money operator +(Money left, Money right);
    public static Money operator -(Money left, Money right);
    public static Money operator *(Money money, decimal factor);
    public static Money operator *(decimal factor, Money money);
    public static Money operator /(Money money, decimal divisor);

    // Comparison (same-currency only).
    public static bool operator <(Money left, Money right);
    public static bool operator >(Money left, Money right);
    public static bool operator <=(Money left, Money right);
    public static bool operator >=(Money left, Money right);
}
```

### Invariants

- `Currency` is non-null, non-empty. ISO 4217 alpha-3 codes expected but not enforced here (callers may use `"EUR"`, `"USD"`, etc.).
- `Amount` is stored to **two decimal places** using banker's rounding (`MidpointRounding.ToEven`). This matches most retail-banking conventions; currencies with other minor-unit scales will require a dedicated subclass or adjustment — deliberately out of scope.
- Because rounding happens on creation, **every place that creates a `Money` is a rounding point**. Intermediate calculation (rate × day-count fraction, per-day accrual) stays in `decimal`, and a `Money` is produced only where a domain doc defines the rounding point.
- Arithmetic across different currencies throws `InvalidOperationException`, because it is a programmer error (see above). A currency mismatch in untrusted input is caught earlier by `Create(...)` or command validation and returned as a `ValidationError`. Cross-currency conversion is explicit and goes through an `IFxConverter` (not in scope for this project — out of the non-goals).

### Properties tested via FsCheck.Xunit

- Addition is associative for same-currency: `(a + b) + c == a + (b + c)`.
- Addition has identity: `a + Money.Zero(c) == a`.
- Addition is commutative: `a + b == b + a`.
- Subtraction cancels: `(a + b) - b == a` within rounding tolerance.
- Multiplication by `1m` is identity.

## `Percentage`

**Status:** Implemented (M1-03).

A dimensionless ratio, with explicit unit awareness.

```csharp
public sealed record Percentage
{
    public decimal Value { get; }        // Stored as the "percent" number: 5.25 means 5.25%.
    public decimal Fraction => Value / 100m;

    public static Result<Percentage, DomainError> Create(decimal value);   // Untrusted input.
    public Percentage(decimal value);                         // Trusted input; throws if negative.
    public static Percentage FromFraction(decimal fraction);  // 0.0525 → Percentage(5.25); throws if negative.
    public static Percentage Zero => new(0m);

    public static Percentage operator +(Percentage left, Percentage right);
    public static Percentage operator -(Percentage left, Percentage right);
    public static bool operator <(Percentage left, Percentage right);
    public static bool operator >(Percentage left, Percentage right);
}
```

### Invariants

- `Value` is non-negative. Negative percentages (credit-side rebates) are out of scope and will be modelled explicitly if needed. `Create` returns a `ValidationError` for a negative value; the constructor and `FromFraction` throw, because a negative value there is a programmer error.
- Addition is same-unit only (both as percent values, no mixing with fractions).
- `-` throws when the difference would be negative, for the same reason as the constructor.
- `Create` reports a negative value as a `ValidationError` on field `value`.
- This type applies no explicit rounding. `Fraction` and the operators keep the precision of `decimal`, which is 28 to 29 significant digits, so a value near that limit can lose digits in `Fraction` (`0.0000000000000000000000000001m / 100m` is `0m`). A caller that needs a rounded percentage (for example the APRC disclosure) rounds explicitly with a stated `MidpointRounding`.
- `ToString()` gives the percent number without trailing zeros and a percent sign: `5.25%`.

### Why store as percent, not fraction?

Human-readable logs and UI labels want `"5.25%"`. Converting at every boundary is noise. We keep the percent form internal and expose `.Fraction` for math.

## `LoanTerm`

**Status:** Implemented (M1-03), except `ToYearsRounded()`, which waits for the owner to define its rounding rule (AGENTS.md §7).

The duration of a loan expressed in whole months.

```csharp
public sealed record LoanTerm
{
    public int Months { get; }

    public static Result<LoanTerm, DomainError> Create(int months);   // Untrusted input.
    public static LoanTerm FromMonths(int months);   // Trusted input; throws outside 1..600.
    public static LoanTerm FromYears(int years);     // = FromMonths(years * 12).

    public LocalDate AddTo(LocalDate start);
    public int ToYearsRounded();
}
```

The constructor is private. C# doesn't allow a static method with the same name as the `Months` property (CS0102), so the factories are named `FromMonths` and `FromYears`.

### Invariants

- `Months >= 1`.
- `Months <= 600` (50-year sanity ceiling; raise with documentation if a real product needs longer).
- `Create` returns a `ValidationError` on field `months` for a value outside this range. The trusted factories throw `ArgumentOutOfRangeException` instead; `FromYears` accepts 1 to 50 years.
- `AddTo(start)` is NodaTime's `LocalDate.PlusMonths(Months)`: it keeps the day of the month and, when the target month is shorter, moves to the last day of that month (31 January plus one month is 28 or 29 February). This is the same interim month-end rule `aprc.md` uses, pending [open question #9](../open-questions.md).

### Why months, not an arbitrary period?

Every amortization schedule generator in Credere works in month-granular steps. The LoanTerm is the single canonical representation; conversions to days (for accrual) are performed per period using the loan's `DayCountConvention`, not by the term itself.

## `LoanId` and other strongly-typed ids

Each aggregate has a strongly-typed id record. Example:

```csharp
public readonly record struct LoanId(Guid Value)
{
    public static LoanId New() => new(Guid.NewGuid());
    public override string ToString() => $"loan_{Value:N}";
}
```

Strongly-typed ids prevent mixing `LoanId` with `LoanApplicationId` at the compiler level. Each module defines its own ids; cross-module APIs use the owning module's id type.

## What is explicitly NOT a value object

- Dates and durations — use NodaTime's `LocalDate`, `LocalDateTime`, `Instant`, `Duration`, `Period` directly. We do not wrap them.
- Email, phone, address — these belong to the Origination module's applicant model; not shared kernel.
- Interest rate — modelled separately in [`rates.md`](rates.md) because a "rate" has internal structure (fixed / floating / tiered / effective).
