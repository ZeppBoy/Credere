namespace Credere.Modules.Servicing.Contracts;

/// <summary>The lifecycle state of a serviced loan.</summary>
public enum LoanState
{
    /// <summary>Disbursement was confirmed and activation is pending.</summary>
    Disbursed,

    /// <summary>The loan is performing.</summary>
    Active,

    /// <summary>The loan is overdue but within the grace period.</summary>
    InGrace,

    /// <summary>The loan has reached the past-due threshold.</summary>
    PastDue,

    /// <summary>The loan has reached the default threshold.</summary>
    Defaulted,

    /// <summary>The loan has been written off.</summary>
    WrittenOff,

    /// <summary>The loan has been restructured.</summary>
    Restructured,

    /// <summary>The defaulted loan has been recovered.</summary>
    Recovered,

    /// <summary>A payment closed all outstanding balances.</summary>
    PaidOff,
}
