using Credere.Modules.Servicing.Contracts;
using NodaTime;

namespace Credere.Modules.Servicing.Infrastructure;

/// <summary>One append-only history row, the stored form of one domain event (ADR-0004).</summary>
internal sealed class LoanEventRow
{
    /// <summary>Gets or sets the row id, generated per event.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the id of the loan whose history this row belongs to.</summary>
    public LoanId StreamId { get; set; }

    /// <summary>Gets or sets the position in the stream, starting at 1. Unique within a stream.</summary>
    public long StreamVersion { get; set; }

    /// <summary>Gets or sets the short name of the event type.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the schema version of the payload, from the event's <c>[EventVersion(n)]</c>.</summary>
    public int EventVersion { get; set; }

    /// <summary>Gets or sets the serialized event body.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the business date the event takes effect on. Servicing events carry a date, not an instant, so
    /// this column is a <c>date</c> (ADR-0004 allows either).
    /// </summary>
    public LocalDate OccurredAt { get; set; }

    /// <summary>Gets or sets when the row was written.</summary>
    public Instant RecordedAt { get; set; }

    /// <summary>Gets or sets the correlation id of the command that produced the event.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the id of the command that produced the event.</summary>
    public Guid CommandId { get; set; }
}
