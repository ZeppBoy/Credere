using Credere.Core.Results;

namespace Credere.Modules.Origination.Contracts;

/// <summary>Identifies a loan product by its stable code.</summary>
public sealed record ProductCode
{
    private const int MaximumLength = 32;

    private ProductCode(string value)
    {
        Value = value;
    }

    /// <summary>Gets the product code.</summary>
    public string Value { get; }

    /// <summary>Creates a product code from input supplied by a caller.</summary>
    /// <param name="value">The code, consisting of 1 to 32 ASCII uppercase letters, digits, hyphens or underscores.</param>
    /// <returns>The code or a validation error for an invalid value.</returns>
    public static Result<ProductCode, DomainError> Create(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaximumLength)
        {
            return InvalidCode();
        }

        foreach (var character in value)
        {
            if (character is not (>= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_'))
            {
                return InvalidCode();
            }
        }

        return Result<ProductCode, DomainError>.Success(new ProductCode(value));
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    private static Result<ProductCode, DomainError> InvalidCode() =>
        Result<ProductCode, DomainError>.Failure(
            new ValidationError("value", "the product code must contain 1 to 32 uppercase ASCII letters, digits, hyphens or underscores"));
}
