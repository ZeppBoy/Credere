using Credere.Core.Schedules;

namespace Credere.Core.Tests.Schedules;

/// <summary>One reference row of an annuity schedule: the ISO payment date and the five amounts (EUR).</summary>
/// <param name="PaymentDate">The ISO payment date.</param>
/// <param name="OpeningBalance">The balance before the payment.</param>
/// <param name="InterestDue">The interest in the payment.</param>
/// <param name="PrincipalDue">The principal in the payment.</param>
/// <param name="TotalPayment">The whole payment.</param>
/// <param name="ClosingBalance">The balance after the payment.</param>
internal sealed record AnnuityScheduleGeneratorRow(
    string PaymentDate,
    decimal OpeningBalance,
    decimal InterestDue,
    decimal PrincipalDue,
    decimal TotalPayment,
    decimal ClosingBalance);

/// <summary>One reference annuity schedule: the request and every expected row.</summary>
/// <param name="Name">The vector name, unique within the set.</param>
/// <param name="Principal">The principal in EUR.</param>
/// <param name="RatePercent">The nominal rate in percent units.</param>
/// <param name="Convention">The day-count convention name, such as <c>ACT/360</c>.</param>
/// <param name="Months">The term in months.</param>
/// <param name="Frequency">The payment frequency.</param>
/// <param name="Disbursement">The ISO disbursement date.</param>
/// <param name="FirstPayment">The ISO first payment date.</param>
/// <param name="Instalment">The regular payment, rounded once.</param>
/// <param name="Rows">Every row of the schedule.</param>
internal sealed record AnnuityScheduleGeneratorVector(
    string Name,
    decimal Principal,
    decimal RatePercent,
    string Convention,
    int Months,
    PaymentFrequency Frequency,
    string Disbursement,
    string FirstPayment,
    decimal Instalment,
    IReadOnlyList<AnnuityScheduleGeneratorRow> Rows);
