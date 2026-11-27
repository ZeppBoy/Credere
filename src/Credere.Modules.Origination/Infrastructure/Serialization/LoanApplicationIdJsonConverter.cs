using System.Text.Json;
using System.Text.Json.Serialization;
using Credere.Modules.Origination.Contracts;

namespace Credere.Modules.Origination.Infrastructure.Serialization;

/// <summary>Writes a <see cref="LoanApplicationId"/> as the string form of its <see cref="Guid"/>.</summary>
internal sealed class LoanApplicationIdJsonConverter : JsonConverter<LoanApplicationId>
{
    /// <inheritdoc />
    public override LoanApplicationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetGuid());

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, LoanApplicationId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
