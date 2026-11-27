using System.Text.Json;
using System.Text.Json.Serialization;
using Credere.Core.ValueObjects;

namespace Credere.Modules.Servicing.Infrastructure.Serialization;

/// <summary>Writes a <see cref="LoanTerm"/> as its number of months.</summary>
internal sealed class LoanTermJsonConverter : JsonConverter<LoanTerm>
{
    /// <inheritdoc />
    public override LoanTerm Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = LoanTerm.Create(reader.GetInt32());
        return result.IsSuccess ? result.Value : throw new JsonException(result.Error.Message);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, LoanTerm value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Months);
}
