using System.Globalization;
using System.Text;

namespace Credere.Core.Tests.Schedules;

/// <summary>
/// Renders an annuity schedule as the text of its Verify snapshot. The snapshot files are rendered from the approved
/// reference vectors, never from the generator's output, so the generator is checked against the oracle; the test then
/// renders the generator's schedule with the same function.
/// </summary>
internal static class AnnuityScheduleGeneratorSnapshotText
{
    /// <summary>Renders the request header and one line per row; amounts have exactly two decimals, culture-invariant.</summary>
    /// <param name="vector">The vector that describes the request.</param>
    /// <param name="rows">The rows to render: the vector's own rows, or the generator's.</param>
    /// <returns>The snapshot text.</returns>
    public static string Render(
        AnnuityScheduleGeneratorVector vector,
        IEnumerable<(int Index, string PaymentDate, decimal Opening, decimal Interest, decimal Principal, decimal Total, decimal Closing)> rows)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"{vector.Name}: EUR {vector.Principal:F2} at {vector.RatePercent}% ({vector.Convention}), ");
        text.Append(CultureInfo.InvariantCulture, $"{vector.Months} months, {vector.Frequency}, ");
        text.Append(CultureInfo.InvariantCulture, $"disbursed {vector.Disbursement}, first payment {vector.FirstPayment}\n");
        text.Append("index | date | opening | interest | principal | total | closing\n");
        foreach (var row in rows)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"{row.Index} | {row.PaymentDate} | {row.Opening:F2} | {row.Interest:F2} | {row.Principal:F2} | {row.Total:F2} | {row.Closing:F2}\n");
        }

        return text.ToString();
    }

    /// <summary>Renders the reference rows of a vector.</summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The snapshot text.</returns>
    public static string RenderVector(AnnuityScheduleGeneratorVector vector) =>
        Render(
            vector,
            vector.Rows.Select((r, i) => (i + 1, r.PaymentDate, r.OpeningBalance, r.InterestDue, r.PrincipalDue, r.TotalPayment, r.ClosingBalance)));
}
