using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Core.Schedules;

/// <summary>
/// One payment of an <see cref="AmortizationSchedule"/>. Its consistency with the neighbouring rows is enforced by
/// <see cref="AmortizationSchedule.Create"/>, not here (docs/domain/schedules.md, "Public contract").
/// </summary>
/// <param name="Index">The 1-based position of the row.</param>
/// <param name="PaymentDate">The date the payment is due.</param>
/// <param name="OpeningBalance">The principal outstanding before the payment.</param>
/// <param name="InterestDue">The interest of the period that ends on <paramref name="PaymentDate"/>.</param>
/// <param name="PrincipalDue">The principal repaid by the payment.</param>
/// <param name="TotalPayment">The amount due: interest plus principal.</param>
/// <param name="ClosingBalance">The principal outstanding after the payment.</param>
public sealed record ScheduleRow(
    int Index,
    LocalDate PaymentDate,
    Money OpeningBalance,
    Money InterestDue,
    Money PrincipalDue,
    Money TotalPayment,
    Money ClosingBalance);
