using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Credere.Modules.Servicing.Infrastructure.Converters;

/// <summary>
/// Stores an <see cref="Instant"/> as a UTC <c>datetime2(7)</c>. The column keeps 100 ns ticks, so an instant finer
/// than a tick loses the remainder. Instants from an <see cref="IClock"/> are tick-aligned.
/// </summary>
internal sealed class InstantConverter : ValueConverter<Instant, DateTime>
{
    /// <summary>Initializes a new instance of the <see cref="InstantConverter"/> class.</summary>
    public InstantConverter()
        : base(
            instant => instant.ToDateTimeUtc(),
            stored => Instant.FromDateTimeUtc(DateTime.SpecifyKind(stored, DateTimeKind.Utc)))
    {
    }
}
