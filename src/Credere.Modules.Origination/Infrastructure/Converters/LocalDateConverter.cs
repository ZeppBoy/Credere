using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Credere.Modules.Origination.Infrastructure.Converters;

/// <summary>
/// Stores a <see cref="LocalDate"/> as a SQL Server <c>date</c>, which is a proleptic Gregorian date. A date in
/// another calendar is first converted to the same day in the ISO calendar, so the stored day is the day that was
/// meant, and what comes back is that day as an ISO date.
/// </summary>
internal sealed class LocalDateConverter : ValueConverter<LocalDate, DateOnly>
{
    /// <summary>Initializes a new instance of the <see cref="LocalDateConverter"/> class.</summary>
    public LocalDateConverter()
        : base(
            date => ToDateOnly(date),
            stored => new LocalDate(stored.Year, stored.Month, stored.Day))
    {
    }

    private static DateOnly ToDateOnly(LocalDate date)
    {
        var iso = date.WithCalendar(CalendarSystem.Iso);
        return new DateOnly(iso.Year, iso.Month, iso.Day);
    }
}
