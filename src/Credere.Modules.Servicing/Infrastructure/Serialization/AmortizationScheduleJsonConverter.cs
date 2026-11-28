using System.Text.Json;
using System.Text.Json.Serialization;
using Credere.Core.Schedules;

namespace Credere.Modules.Servicing.Infrastructure.Serialization;

/// <summary>
/// Writes an <see cref="AmortizationSchedule"/> as its array of rows and rebuilds it with <c>Create</c>, so a stored
/// schedule is checked against the schedule invariants as it is read.
/// </summary>
internal sealed class AmortizationScheduleJsonConverter : JsonConverter<AmortizationSchedule>
{
    /// <inheritdoc />
    public override AmortizationSchedule Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var rows = JsonSerializer.Deserialize<ScheduleRow[]>(ref reader, options)
            ?? throw new JsonException("A schedule must be an array of rows.");
        if (rows.Length == 0)
        {
            throw new JsonException("A schedule must contain at least one row.");
        }

        var result = AmortizationSchedule.Create(rows[0].OpeningBalance, rows);
        return result.IsSuccess ? result.Value : throw new JsonException(result.Error.Message);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, AmortizationSchedule value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.Rows.ToArray(), options);
}
