using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Core.Schedules;

/// <summary>
/// One payment of an <see cref="AmortizationSchedule"/>. Its consistency with the neighbouring rows is enforced by
/// <see cref="AmortizationSchedule.Create"/>, not here (docs/domain/schedules.md, "Public contract").
/// </summary>
/// <param name="Index">The 1-based position of the row.</param>
/// <param name="PaymentDate">The date the payment is due.</param>
/// <param name="OpeningBalance">The outstanding balance at the start of the period.</param>
/// <param name="CapitalisedInterest">Period interest added to the balance, or zero in the row's currency.</param>
/// <param name="InterestDue">The interest payable in cash on <paramref name="PaymentDate"/>.</param>
/// <param name="PrincipalDue">The principal repaid by the payment.</param>
/// <param name="TotalPayment">The amount due: interest plus principal.</param>
/// <param name="ClosingBalance">The balance after capitalising interest and applying any principal payment.</param>
public sealed record ScheduleRow(
    int Index,
    LocalDate PaymentDate,
    Money OpeningBalance,
    Money CapitalisedInterest,
    Money InterestDue,
    Money PrincipalDue,
    Money TotalPayment,
    Money ClosingBalance);
