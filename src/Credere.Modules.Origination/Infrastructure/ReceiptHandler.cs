namespace Credere.Modules.Origination.Infrastructure;

/// <summary>The names of the command handlers that keep a receipt table (ADR-0007).</summary>
internal static class ReceiptHandler
{
    /// <summary>The handler of <c>SubmitApplication</c>.</summary>
    internal const string SubmitApplication = "SubmitApplication";

    /// <summary>The handler of <c>DecideApplication</c>.</summary>
    internal const string DecideApplication = "DecideApplication";

    /// <summary>The handler of <c>AcceptTerms</c>.</summary>
    internal const string AcceptTerms = "AcceptTerms";

    /// <summary>The handler of <c>WithdrawApplication</c>.</summary>
    internal const string WithdrawApplication = "WithdrawApplication";
}
