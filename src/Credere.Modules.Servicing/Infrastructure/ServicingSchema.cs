namespace Credere.Modules.Servicing.Infrastructure;

/// <summary>The SQL Server schema and table names of the Servicing module (docs/modules/servicing.md, "Persistence").</summary>
internal static class ServicingSchema
{
    /// <summary>The schema every Servicing table lives in, and the schema of its migration history table.</summary>
    internal const string Name = "servicing";

    /// <summary>The current-state table, one row per loan.</summary>
    internal const string Loans = "Loans";

    /// <summary>The append-only history table, keyed by <c>(StreamId, StreamVersion)</c> (ADR-0004).</summary>
    internal const string LoanEvents = "LoanEvents";

    /// <summary>The persisted handoff from Origination.</summary>
    internal const string DisbursementIntents = "DisbursementIntents";

    /// <summary>The transactional outbox table (ADR-0005).</summary>
    internal const string Outbox = "Outbox";

    /// <summary>The amortization schedule snapshot taken at disbursement, one row per schedule row.</summary>
    internal const string LoanSchedules = "LoanSchedules";

    /// <summary>The accrual ledger, one row per accrual that changed a balance.</summary>
    internal const string AccrualLedger = "AccrualLedger";

    /// <summary>The payment ledger, one row per recorded payment or payoff.</summary>
    internal const string PaymentLedger = "PaymentLedger";

    /// <summary>The handlers that keep a command receipt table (ADR-0007), one for each public command of the facade.</summary>
    internal static readonly IReadOnlyList<string> ReceiptHandlers =
    [
        ReceiptHandler.RegisterDisbursementIntent,
        ReceiptHandler.Disburse,
        ReceiptHandler.Activate,
        ReceiptHandler.AccrueInterest,
        ReceiptHandler.RecordPayment,
        ReceiptHandler.ApplyAging,
        ReceiptHandler.ExecutePayoff,
        ReceiptHandler.WriteOff,
        ReceiptHandler.Restructure,
        ReceiptHandler.Recover,
    ];

    /// <summary>Gets the name of a handler's receipt table, <c>CommandReceipts_&lt;Handler&gt;</c>.</summary>
    /// <param name="handler">The handler name, one of <see cref="ReceiptHandlers"/>.</param>
    /// <returns>The table name without its schema.</returns>
    internal static string ReceiptTable(string handler) => $"CommandReceipts_{handler}";
}
