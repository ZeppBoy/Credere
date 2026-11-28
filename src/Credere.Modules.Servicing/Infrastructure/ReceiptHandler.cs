namespace Credere.Modules.Servicing.Infrastructure;

/// <summary>
/// The names of the command handlers that keep a receipt table (ADR-0007). The receipt of
/// <see cref="RegisterDisbursementIntent"/> is also the subscriber receipt of the host handler that turns an
/// <c>ApplicationAccepted</c> event into this command: the event id is the command id (ADR-0011).
/// </summary>
internal static class ReceiptHandler
{
    /// <summary>The handler of <c>RegisterDisbursementIntent</c>.</summary>
    internal const string RegisterDisbursementIntent = "RegisterDisbursementIntent";

    /// <summary>The handler of <c>Disburse</c>.</summary>
    internal const string Disburse = "Disburse";

    /// <summary>The handler of <c>Activate</c>.</summary>
    internal const string Activate = "Activate";

    /// <summary>The handler of <c>AccrueInterest</c>.</summary>
    internal const string AccrueInterest = "AccrueInterest";

    /// <summary>The handler of <c>RecordPayment</c>.</summary>
    internal const string RecordPayment = "RecordPayment";

    /// <summary>The handler of <c>ApplyAging</c>.</summary>
    internal const string ApplyAging = "ApplyAging";

    /// <summary>The handler of <c>ExecutePayoff</c>.</summary>
    internal const string ExecutePayoff = "ExecutePayoff";

    /// <summary>The handler of <c>WriteOff</c>.</summary>
    internal const string WriteOff = "WriteOff";

    /// <summary>The handler of <c>Restructure</c>.</summary>
    internal const string Restructure = "Restructure";

    /// <summary>The handler of <c>Recover</c>.</summary>
    internal const string Recover = "Recover";
}
