namespace Credere.Modules.Origination.Infrastructure;

/// <summary>The SQL Server schema and table names of the Origination module (docs/modules/origination.md, "Persistence").</summary>
internal static class OriginationSchema
{
    /// <summary>The schema every Origination table lives in, and the schema of its migration history table.</summary>
    internal const string Name = "origination";

    /// <summary>The current-state table, one row per application.</summary>
    internal const string LoanApplications = "LoanApplications";

    /// <summary>The append-only history table, keyed by <c>(StreamId, StreamVersion)</c> (ADR-0004).</summary>
    internal const string LoanApplicationEvents = "LoanApplicationEvents";

    /// <summary>The transactional outbox table (ADR-0005).</summary>
    internal const string Outbox = "Outbox";

    /// <summary>
    /// The handlers that keep a command receipt table (ADR-0007), one for each public command of the facade.
    /// </summary>
    internal static readonly IReadOnlyList<string> ReceiptHandlers =
    [
        ReceiptHandler.SubmitApplication,
        ReceiptHandler.DecideApplication,
        ReceiptHandler.AcceptTerms,
        ReceiptHandler.WithdrawApplication,
    ];

    /// <summary>Gets the name of a handler's receipt table, <c>CommandReceipts_&lt;Handler&gt;</c>.</summary>
    /// <param name="handler">The handler name, one of <see cref="ReceiptHandlers"/>.</param>
    /// <returns>The table name without its schema.</returns>
    internal static string ReceiptTable(string handler) => $"CommandReceipts_{handler}";
}
