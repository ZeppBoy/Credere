using System.Text.Json;
using System.Text.Json.Serialization;
using Credere.Core.DayCounts;
using Credere.Core.Schedules;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain;

namespace Credere.Modules.Servicing.Infrastructure.Serialization;

/// <summary>
/// Writes <see cref="ProductTerms"/> as an object and rebuilds it through its constructor, which checks the thresholds
/// and the enums. The constructor is internal, so the generic record binding cannot be used.
/// </summary>
internal sealed class ProductTermsJsonConverter : JsonConverter<ProductTerms>
{
    /// <inheritdoc />
    public override ProductTerms Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var shape = JsonSerializer.Deserialize<Shape>(ref reader, options)
            ?? throw new JsonException("Product terms must be an object.");
        try
        {
            return new ProductTerms(
                shape.DayCount, shape.Frequency, shape.Repayment, shape.PastDueThreshold, shape.DefaultThreshold, shape.Calendar);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException($"The stored product terms are invalid: {exception.Message}", exception);
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ProductTerms value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(
            writer,
            new Shape(
                value.DayCount, value.Frequency, value.Repayment, value.PastDueThreshold, value.DefaultThreshold, value.Calendar),
            options);

    private sealed record Shape(
        DayCountConvention DayCount,
        PaymentFrequency Frequency,
        RepaymentType Repayment,
        int PastDueThreshold,
        int DefaultThreshold,
        string Calendar);
}
