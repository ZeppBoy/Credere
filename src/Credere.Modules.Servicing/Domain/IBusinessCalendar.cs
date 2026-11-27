using NodaTime;

namespace Credere.Modules.Servicing.Domain;

/// <summary>Identifies business days for the loan's configured calendar.</summary>
internal interface IBusinessCalendar
{
    bool IsBusinessDay(LocalDate date);
}
