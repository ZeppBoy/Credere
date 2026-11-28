using System.Text.Json;
using System.Text.Json.Serialization;
using Credere.Modules.Origination.Contracts;

namespace Credere.Modules.Origination.Infrastructure.Serialization;

/// <summary>Writes a <see cref="ProductCode"/> as its string and validates it with <c>Create</c> on the way in.</summary>
internal sealed class ProductCodeJsonConverter : JsonConverter<ProductCode>
{
    /// <inheritdoc />
    public override ProductCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString() ?? throw new JsonException("A product code must be a string.");
        var result = ProductCode.Create(text);
        return result.IsSuccess ? result.Value : throw new JsonException(result.Error.Message);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ProductCode value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
