using Credere.Modules.Origination.Contracts;
using NodaTime;

namespace Credere.Modules.Origination.Domain;

/// <summary>The most recent underwriting decision recorded on an application.</summary>
/// <param name="Outcome">The underwriting outcome.</param>
/// <param name="Rationale">The short reason for the outcome.</param>
/// <param name="DecidedAt">When the decision was recorded.</param>
internal sealed record UnderwritingDecision(DecisionOutcome Outcome, string Rationale, Instant DecidedAt);
