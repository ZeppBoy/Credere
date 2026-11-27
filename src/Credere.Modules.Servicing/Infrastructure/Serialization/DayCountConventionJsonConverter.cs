using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using Credere.Core.DayCounts;

namespace Credere.Modules.Servicing.Infrastructure.Serialization;

/// <summary>
/// Writes a <see cref="DayCountConvention"/> as its <see cref="DayCountConvention.Name"/> and reads it back from the
/// five built-in conventions (docs/modules/servicing.md, "Product configuration"). An unknown name is an error, never
/// a default, because the convention changes the interest a loan accrues.
/// </summary>
internal sealed class DayCountConventionJsonConverter : JsonConverter<DayCountConvention>
{
    private static readonly FrozenDictionary<string, DayCountConvention> BuiltIn = new DayCountConvention[]
    {
        new Thirty360Isda(),
        new ThirtyE360(),
        new Act360(),
        new Act365Fixed(),
        new ActActIsda(),
    }.ToFrozenDictionary(convention => convention.Name, StringComparer.Ordinal);

    /// <summary>Gets the built-in conventions by name.</summary>
    internal static IReadOnlyDictionary<string, DayCountConvention> Conventions => BuiltIn;

    /// <inheritdoc />
    public override DayCountConvention Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var name = reader.GetString() ?? throw new JsonException("A day-count convention must be a string.");
        return BuiltIn.TryGetValue(name, out var convention)
            ? convention
            : throw new JsonException($"'{name}' is not a built-in day-count convention.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DayCountConvention value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Name);
}
