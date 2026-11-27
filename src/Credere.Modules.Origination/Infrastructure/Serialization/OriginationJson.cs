using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Credere.Modules.Origination.Infrastructure.Serialization;

/// <summary>
/// The JSON options for every payload the module stores: history events, outbox events and command outcomes.
/// </summary>
/// <remarks>
/// <para>
/// Property names keep their C# spelling. Enums are written as names and never read from numbers, so a reordered enum
/// can't change the meaning of stored rows. Value objects go through explicit converters that rebuild them with their
/// <c>Create</c> methods, so a stored value is validated as it is read.
/// </para>
/// <para>
/// Reading is strict: a payload missing a constructor parameter, or carrying a member the type does not have, throws
/// instead of filling in defaults. Stored history comes from this module, so either case is a bug or corruption.
/// </para>
/// </remarks>
internal static class OriginationJson
{
    /// <summary>The options shared by every serializer in the module.</summary>
    internal static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = false,
        };

        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        options.Converters.Add(new InstantJsonConverter());
        options.Converters.Add(new LocalDateJsonConverter());
        options.Converters.Add(new LoanApplicationIdJsonConverter());
        options.Converters.Add(new ProductCodeJsonConverter());
        options.Converters.Add(new MoneyJsonConverter());
        options.Converters.Add(new PercentageJsonConverter());
        options.Converters.Add(new LoanTermJsonConverter());
        options.MakeReadOnly();
        return options;
    }
}
