using System.Text.Json;
using System.Text.Json.Serialization;
using Credere.Core.ValueObjects;

namespace Credere.Modules.Origination.Infrastructure.Serialization;

/// <summary>Writes a <see cref="Percentage"/> as its percent number, for example <c>5.25</c> for 5.25%.</summary>
internal sealed class PercentageJsonConverter : JsonConverter<Percentage>
{
    /// <inheritdoc />
    public override Percentage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = Percentage.Create(reader.GetDecimal());
        return result.IsSuccess ? result.Value : throw new JsonException(result.Error.Message);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Percentage value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}
