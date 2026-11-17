# Guardrails — `PolicyFilter`

`PolicyFilter` is a **deterministic** policy engine applied to every agent output before it leaves the Ai module. It is not another LLM. It is a small, unit-tested, declarative rule set. Guardrails are the **enforcement** layer; the prompt is the **suggestion** layer. Never trust a prompt alone for compliance.

## Public contract

```csharp
internal sealed class PolicyFilter
{
    public PolicyDecision Apply(AgentOutput output, PolicyContext ctx);
}

public sealed record PolicyDecision(
    bool Allowed,
    IReadOnlyList<RuleViolation> Violations);

public sealed record PolicyContext(
    string AgentName,
    LocalDateTime LocalTime,
    DateTimeZone BorrowerZone,
    OutreachChannel? Channel,
    IReadOnlyList<string> RequiredDisclosures);
```

A rule is:

```csharp
internal interface IPolicyRule
{
    string Name { get; }
    RuleViolation? Evaluate(AgentOutput output, PolicyContext ctx);
}
```

Rules are registered via DI in a stable order. Each rule either returns `null` (passed) or a `RuleViolation` with a code and human-readable message.

## Rule catalog (baseline, applied to every agent)

### `R1-BannedPhrases`
Blocks explicit threats or discriminatory language. Hard-coded list in `Rules/BannedPhrases.cs`, loaded at startup, case-insensitive regex match. Example triggers: "we will take you to court tomorrow", slurs, pressure tactics ("you have 24 hours or else"), etc.

### `R2-PiiInText`
Rejects outputs containing applicant personal data beyond what the context requires. Flags: national ID numbers, bank account numbers, phone numbers, email addresses appearing in free text (not in explicit structured fields). The "beyond what the context requires" check is driven by the context's `ExpectedPiiReferences` set; anything outside this set is a violation.

### `R3-NoThirdPartyDisclosure`
Rejects outputs that mention named parties other than the borrower. Detection uses a simple proper-noun heuristic plus a dictionary of known adjacent-parties (co-signer names from the application, if present) — if any name outside the borrower's own identifier appears, the output is flagged.

### `R4-NoForwardLookingGuarantee`
Blocks sentences promising future outcomes the lender cannot guarantee. Regex patterns for "you will qualify", "we will approve", "guaranteed", "promise", etc.

### `R5-StructuredOutputShape`
Validates that the output matches the agent's declared JSON schema (if any). Malformed structured outputs are a violation.

## Collections-specific rules

Applied only when `ctx.AgentName == "Collections"`:

### `C1-AfterHoursBlock`
Reject if `ctx.LocalTime` is outside `[AfterHoursStart, AfterHoursEnd]` in `ctx.BorrowerZone`. Defaults 08:00–20:00. Configurable per-locale. Weekends (Saturday/Sunday) blocked by default unless explicitly opted into.

### `C2-MandatoryDisclosures`
Rejects if the output does not contain all `ctx.RequiredDisclosures`. For the US-style debt-collection context, this includes the "mini-Miranda" disclosure. For the EU context, it includes the Credit Information Statement and the data-protection notice. Matching is phrase-level; fuzzy matching is disabled to prevent LLM paraphrasing that drops regulatory words.

### `C3-NoLegalThreat`
Specific legal-threat phrase list beyond R1. Rejects "lawsuit", "court", "garnish", "lien", "attorney" unless the output is a legal-notice template (currently out of scope).

### `C4-ChannelConstraint`
Rejects if the output format is inconsistent with the channel:
- SMS: subject must be empty; body ≤ 320 chars.
- Email: must contain subject + body; HTML allowed.
- Letter: must contain a formal salutation and signature block.

## Early-warning-specific rules

Applied only when `ctx.AgentName == "EarlyWarning"`:

### `E1-SignalCitation`
Each reason in the output must cite one of the registered signal categories (`PaymentBehavior`, `Employment`, `BureauDelta`, `Macroeconomic`). Free-form reasons that don't cite a category are rejected.

### `E2-ConfidenceBounded`
`confidence` must be in `[0.0, 1.0]`. Outside this range → violation.

## Credit-decisioning-specific rules

Applied only when `ctx.AgentName == "CreditDecisioning"`:

### `D1-NoOverrideOfDeterministicRule`
If a deterministic rule fired pre-model with decision `X`, the model output's `outcome` must equal `X`. Any divergence is rejected.

### `D2-NoProtectedClassMention`
Rationale must not mention protected classes (age, gender, race, religion, national origin, marital status, disability). Driven by a keyword list + context flag; regex-based detection.

## Enforcement path

1. Agent completes its final turn and produces `AgentOutput`.
2. `AgentHost` calls `PolicyFilter.Apply(output, ctx)`.
3. If `Allowed == false`, the agent returns `AgentOutcome.Refused` with the `Violations` list. The session doc is updated with status `refused` and the violations are persisted.
4. Refusals surface to the banker UI with the specific rules cited. The banker can either edit the inputs (e.g., different time of day for a Collections draft) or escalate manually.

## Testing

Each rule has:

- Unit tests with ≥5 positive (should pass) and ≥5 negative (should fail) examples.
- A property-based test where applicable (for structural rules).

The ensemble is tested against the AI eval golden dataset (see [`evals.md`](evals.md)) — specifically the "compliance" split, which contains known-bad outputs the filter must catch.

## Why not just prompt the model better?

Prompts are suggestions. A well-aligned model will usually comply; a mis-aligned or jailbroken one will not. For money-moving or borrower-facing output, we need a deterministic last line of defence. The prompt tells the model what we want; `PolicyFilter` enforces what is non-negotiable.

## Adding a new rule

1. Implement `IPolicyRule` in `Credere.Modules.Ai/PolicyFilter/Rules/`.
2. Register in `PolicyFilterExtensions.AddCrederePolicyFilter`.
3. Add ≥10 unit tests.
4. Add the rule to the appropriate "Rule catalog" section in this doc.
5. Add scenarios to the compliance golden split.
