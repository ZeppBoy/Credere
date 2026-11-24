using Credere.Core.Rates;
using Credere.Core.Schedules;
using Xunit;
using static Credere.Core.Tests.Schedules.AnnuityScheduleGeneratorVectorRequests;

namespace Credere.Core.Tests.Schedules;

/// <summary>
/// Locks the full schedule of three canonical loans with Verify.Xunit (docs/domain/schedules.md, "Verification"). The
/// <c>*.verified.txt</c> files were rendered from the approved reference vectors, not from the generator, so a change to
/// the generator that alters a schedule fails here, and a changed snapshot is accepted only after owner review
/// (AGENTS.md §8).
/// </summary>
public sealed class AnnuityScheduleGeneratorSnapshotTests
{
    private static readonly AnnuityScheduleGenerator Generator = new(new InterestCalculator());

    /// <summary>Gets the canonical loans: the doc's EUR 10,000 x 24 x 5% x 30E/360, a leap-year Act/Act loan, and a quarterly loan.</summary>
    public static TheoryData<string> Names => new()
    {
        "Level30E360_24m",
        "ActActIsda_leapYear_12m",
        "Quarterly30E360_48m",
    };

    [Theory]
    [MemberData(nameof(Names))]
    public Task Generate_canonicalLoan_matchesTheSnapshotRenderedFromTheVector(string name)
    {
        var vector = AnnuityScheduleGeneratorVectors.All.Single(v => string.Equals(v.Name, name, StringComparison.Ordinal));
        var schedule = Generator.Generate(RequestOf(vector).Value).Value;

        var text = AnnuityScheduleGeneratorSnapshotText.Render(
            vector,
            schedule.Rows.Select(r => (
                r.Index,
                r.PaymentDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                r.OpeningBalance.Amount,
                r.InterestDue.Amount,
                r.PrincipalDue.Amount,
                r.TotalPayment.Amount,
                r.ClosingBalance.Amount)));

        return Verifier.Verify(text, "txt")
            .UseDirectory("Snapshots")
            .UseFileName($"AnnuityScheduleGenerator.{name}");
    }
}
