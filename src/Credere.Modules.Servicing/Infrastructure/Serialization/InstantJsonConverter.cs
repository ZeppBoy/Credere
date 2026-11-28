using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Text;

namespace Credere.Modules.Servicing.Infrastructure.Serialization;

/// <summary>Writes an <see cref="Instant"/> as extended ISO 8601 in UTC, keeping nanoseconds.</summary>
internal sealed class InstantJsonConverter : JsonConverter<Instant>
{
    /// <inheritdoc />
    public override Instant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString() ?? throw new JsonException("An instant must be a string.");
        var result = InstantPattern.ExtendedIso.Parse(text);
        return result.Success ? result.Value : throw new JsonException($"'{text}' is not an ISO 8601 instant.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Instant value, JsonSerializerOptions options) =>
        writer.WriteStringValue(InstantPattern.ExtendedIso.Format(value));
}
