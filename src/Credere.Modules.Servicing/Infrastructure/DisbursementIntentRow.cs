using Credere.Modules.Servicing.Contracts;
using NodaTime;

namespace Credere.Modules.Servicing.Infrastructure;

/// <summary>
/// The persisted handoff from Origination (docs/modules/servicing.md, "Handoff from Origination"). Unique on
/// <c>(SourceReference, AcceptedTermsVersion)</c>, and at most one loan is created from it.
/// </summary>
internal sealed class DisbursementIntentRow
{
    /// <summary>Gets or sets the intent id.</summary>
    public DisbursementIntentId Id { get; set; }

    /// <summary>Gets or sets the opaque reference of the accepted application.</summary>
    public Guid SourceReference { get; set; }

    /// <summary>Gets or sets the accepted terms version.</summary>
    public int AcceptedTermsVersion { get; set; }

    /// <summary>Gets or sets the product key.</summary>
    public string Product { get; set; } = string.Empty;

    /// <summary>Gets or sets the currency of the principal.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the accepted principal.</summary>
    public decimal Principal { get; set; }

    /// <summary>Gets or sets the accepted nominal rate in percent.</summary>
    public decimal NominalRate { get; set; }

    /// <summary>Gets or sets the accepted term in months.</summary>
    public int TermMonths { get; set; }

    /// <summary>Gets or sets the intent state.</summary>
    public DisbursementIntentState State { get; set; }

    /// <summary>Gets or sets the loan created from the intent, or <see langword="null"/> while it is pending.</summary>
    public LoanId? LoanId { get; set; }

    /// <summary>Gets or sets when the intent was stored.</summary>
    public Instant RegisteredAt { get; set; }

    /// <summary>Gets or sets the concurrency token the database maintains.</summary>
    public byte[] RowVersion { get; set; } = [];
}
