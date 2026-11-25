namespace Credere.Modules.Servicing.Contracts;

/// <summary>The state of a registered disbursement intent.</summary>
public enum DisbursementIntentState
{
    /// <summary>Treasury disbursement is pending confirmation.</summary>
    Pending,

    /// <summary>Treasury disbursement was confirmed.</summary>
    Disbursed,
}
