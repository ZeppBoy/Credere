using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Text;

namespace Credere.Modules.Origination.Infrastructure.Serialization;

/// <summary>
/// Writes a <see cref="LocalDate"/> as an ISO 8601 date, for example <c>2026-11-25</c>. A date in another calendar is
/// first converted to the same day in the ISO calendar, so the text names the day that was meant, and what is read
/// back is that day as an ISO date.
/// </summary>
internal sealed class LocalDateJsonConverter : JsonConverter<LocalDate>
{
    /// <inheritdoc />
    public override LocalDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString() ?? throw new JsonException("A date must be a string.");
        var result = LocalDatePattern.Iso.Parse(text);
        return result.Success ? result.Value : throw new JsonException($"'{text}' is not an ISO 8601 date.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, LocalDate value, JsonSerializerOptions options) =>
        writer.WriteStringValue(LocalDatePattern.Iso.Format(value.WithCalendar(CalendarSystem.Iso)));
}
