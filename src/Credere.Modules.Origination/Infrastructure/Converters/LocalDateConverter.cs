using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Credere.Modules.Origination.Infrastructure.Converters;

/// <summary>Stores a <see cref="LocalDate"/> as a SQL Server <c>date</c>.</summary>
internal sealed class LocalDateConverter : ValueConverter<LocalDate, DateOnly>
{
    /// <summary>Initializes a new instance of the <see cref="LocalDateConverter"/> class.</summary>
    public LocalDateConverter()
        : base(
            date => new DateOnly(date.Year, date.Month, date.Day),
            stored => new LocalDate(stored.Year, stored.Month, stored.Day))
    {
    }
}
