namespace Credere.Modules.Origination.Contracts;

/// <summary>The possible underwriting outcomes.</summary>
public enum DecisionOutcome
{
    /// <summary>Approve the application on specified final terms.</summary>
    Approve,

    /// <summary>Decline the application.</summary>
    Decline,

    /// <summary>Send the application to human review.</summary>
    HumanReview,
}
