using Credere.Core.Schedules;

namespace Credere.Core.Tests.Schedules;

/// <summary>
/// Reference requests for which <c>AnnuityScheduleGenerator</c> must return a <c>ValidationError</c> on <c>principal</c>:
/// the instalment, rounded once to cents, repays the balance before the last row, so a regular row's principal would
/// exceed its opening balance (owner decision on PR #51: <c>Generate</c> returns a <c>Result</c>; schedules.md
/// "1. Annuity" and "Rounding and residuals").
/// </summary>
/// <remarks>
/// <para>
/// Each case was found with exact rational arithmetic, as for <see cref="AnnuityScheduleGeneratorVectors"/>. The first
/// was reported by the cross-vendor reviewer. The two boundary schedules just below the threshold, which must succeed,
/// are <c>TinyPrincipal_zeroRate_5m</c> and <c>TinyPrincipal_6pct_12m</c> in that set.
/// </para>
/// <para>
/// A negative principal (a long first period) is not a failure here; it is allowed.
/// </para>
/// </remarks>
internal static class AnnuityScheduleGeneratorFailureVectors
{
    /// <summary>Gets every request that must be rejected.</summary>
    public static IReadOnlyList<AnnuityScheduleGeneratorFailureVector> All { get; } =
    [
        // 0.03 EUR at 0%, 5 monthly rows: instalment 0.03 / 5 = 0.006 -> 0.01. Closing balances 0.02, 0.01, 0.00 after rows
        // 1-3; row 4 pays 0.01 against an opening balance of 0.00, so its closing balance would be -0.01.
        new("Tiny_zeroRate_5m", 0.03m, 0m, "30E/360", 5, PaymentFrequency.Monthly, "2026-01-15", "2026-02-15"),

        // The same at quarterly payments: 15 months is 5 quarterly rows, so the same arithmetic applies.
        new("Tiny_zeroRate_5q", 0.03m, 0m, "ACT/360", 15, PaymentFrequency.Quarterly, "2026-01-15", "2026-04-15"),

        // 0.95 EUR at 6%, 13 monthly rows (30E/360, day 15, full first period): r = 0.005, instalment
        // 0.95 x 0.005 x 1.005^13 / (1.005^13 - 1) = 0.075660... -> 0.08. Interest is below one cent in every row, so each
        // regular row repays 0.08 and the balance is below zero after row 12 (0.95 - 12 x 0.08 = -0.01).
        new("Tiny_6pct_13m", 0.95m, 6m, "30E/360", 13, PaymentFrequency.Monthly, "2026-01-15", "2026-02-15"),

    ];
}
