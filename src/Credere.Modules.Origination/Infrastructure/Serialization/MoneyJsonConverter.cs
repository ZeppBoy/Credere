using System.Text.Json;
using System.Text.Json.Serialization;
using Credere.Core.ValueObjects;

namespace Credere.Modules.Origination.Infrastructure.Serialization;

/// <summary>
/// Writes a <see cref="Money"/> as <c>{"Amount":100.50,"Currency":"EUR"}</c>. The amount is a JSON number, so its
/// decimal digits survive exactly, and <c>Create</c> validates it on the way in.
/// </summary>
internal sealed class MoneyJsonConverter : JsonConverter<Money>
{
    /// <inheritdoc />
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var shape = JsonSerializer.Deserialize<Shape>(ref reader, options)
            ?? throw new JsonException("Money must be an object.");
        var result = Money.Create(shape.Amount, shape.Currency);
        return result.IsSuccess ? result.Value : throw new JsonException(result.Error.Message);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, new Shape(value.Amount, value.Currency), options);

    private sealed record Shape(decimal Amount, string Currency);
}
