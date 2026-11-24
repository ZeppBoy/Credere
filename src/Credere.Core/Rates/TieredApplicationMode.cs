namespace Credere.Core.Rates;

/// <summary>Determines how a tiered rate applies to the outstanding balance.</summary>
public enum TieredApplicationMode
{
    /// <summary>Applies the rate of the matching tier to the entire balance.</summary>
    OnCurrentBalance,

    /// <summary>Applies each tier's rate to its portion of the balance.</summary>
    Marginal,
}
