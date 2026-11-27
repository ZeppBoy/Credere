using System.Text.Json;
using System.Text.Json.Serialization;
using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Infrastructure.Serialization;

/// <summary>Writes a <see cref="LoanId"/> as the string form of its <see cref="Guid"/>.</summary>
internal sealed class LoanIdJsonConverter : JsonConverter<LoanId>
{
    /// <inheritdoc />
    public override LoanId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetGuid());

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, LoanId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
