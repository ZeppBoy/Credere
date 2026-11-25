namespace Credere.Modules.Servicing.Contracts;

/// <summary>The repayment schedule types supported by Servicing in M2.</summary>
public enum RepaymentType
{
    /// <summary>An annuity schedule.</summary>
    Annuity,

    /// <summary>A differentiated principal repayment schedule.</summary>
    Differentiated,

    /// <summary>An interest-only bullet schedule with principal due at maturity.</summary>
    InterestOnlyBullet,
}
