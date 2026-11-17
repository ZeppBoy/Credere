# Agents

Three agents live in `src/Credere.Modules.Ai/Application/Agents/`. Each has a defined role, bounded authority, and deterministic guardrails. Inputs, outputs, and failure modes are explicit.

## Shared agent-loop contract

All agents run on `AgentHost`, a hand-rolled loop:

```csharp
internal sealed class AgentHost
{
    public async Task<Result<AgentOutput, DomainError>> RunAsync(
        AgentDefinition def,
        AgentInputs inputs,
        CancellationToken ct);
}
```

The loop:

1. Creates an `ai.agent_sessions` doc (status: `running`).
2. Executes `def.PreModelPolicy(inputs)` — deterministic rules that may short-circuit the model (CreditDecisioning uses this).
3. Iterates:
   - Call `IChatClient.CompleteAsync(systemPrompt + conversation, def.Tools, ct)`.
   - If tool calls returned: dispatch via internal MCP tool dispatcher; append tool results.
   - Else: run `PolicyFilter.Apply(output)`. On refusal, record and return `Refused`. On pass, record final and return `Succeeded`.
4. Caps at `def.MaxTurns` (default 8). Exceeding the cap is a `RefusedByLoopGuard` outcome, not an exception.
5. Updates the session doc to `succeeded | refused | failed` with timing and cost.

Every agent definition names:

- `SystemPromptFile` (`.md` in `Prompts/`).
- `Tools` (subset of the MCP catalog + agent-internal tools).
- `PreModelPolicy` (optional deterministic decision).
- `GuardrailProfile` (which PolicyFilter rules apply).
- `MaxTurns`.
- `AllowedModels` (default: Anthropic Claude Sonnet 4.6; fallback Haiku 4.5 for cost-sensitive agents).

## 1. Credit-decisioning

**Role.** Produce an underwriting recommendation (`Approve | Decline | HumanReview`) with structured rationale for a submitted loan application. The recommendation is advisory; a human underwriter must confirm before `IOriginationModule.DecideAsync` is called.

**Authority.** Zero write authority on aggregates. The agent produces a recommendation; the banker UI shows it; the banker acts.

**Inputs.**

- `LoanApplicationId` → full application details via `IReportingModule`.
- Fake-bureau score (seeded service): numeric band + up to five explanatory flags.
- Policy context from configuration (max LTV, max DTI, min score, product-specific rules).

**Pre-model policy (deterministic).**

```
if applicant_country ∉ allowed_countries → Decline (hard rule)
if score < min_score_for_product        → Decline (hard rule)
if dti > max_dti_for_product            → Decline (hard rule)
if score ≥ excellent_threshold AND
   dti ≤ excellent_dti_threshold AND
   no adverse_flags                     → Approve (hard rule)
else                                    → let the model recommend HumanReview or Approve-with-conditions
```

The model's output is constrained: if any deterministic rule fired, the model is asked only to **explain** that decision in plain language. The model does not override.

**Output.**

```csharp
public sealed record CreditRecommendation(
    DecisionOutcome Outcome,            // Approve | Decline | HumanReview
    string Rationale,                   // Plain-language, structured by section.
    IReadOnlyList<RationaleFactor> Factors,
    DecisionBasis Basis);               // "DeterministicRule" | "ModelSuggestion"
```

**Failure modes.**

- Missing application → `Result.Failure(new NotFound(...))`.
- Model returns malformed output → re-asked once with stricter instruction; second failure returns `Result.Failure(new AgentOutputMalformed(...))`.
- Hard policy violation detected in model text (e.g., the model suggests Approve when a hard rule declined) → treated as a guardrail violation; refused with explanation.

**Guardrail profile.** Baseline: banned phrases, no PII in the rationale beyond the applicant's id, no forward-looking promises ("you will qualify for...").

## 2. Early-warning

**Role.** Produce a nightly watchlist of loans likely to deteriorate.

**Authority.** Writes to `ai.Watchlist` (via `IAiModule.RunEarlyWarningSweepAsync`), which the Reporting module projects into `reporting.Watchlist`. No action on the loan itself.

**Inputs.**

- Portfolio snapshot from `IReportingModule.ListLoansAsync` filtered to non-terminal states.
- Seeded signals: payment-behavior features (moving averages, missed payments), employment-status flag (simulated), bureau-score delta (simulated).

**Agent design.**

The agent runs per loan. For each candidate loan it calls the model with a compact signal summary and asks: "is this loan at elevated risk in the next 30 days, and why?" Model output is constrained to a schema:

```json
{
  "risk_elevated": true,
  "severity": "low | medium | high",
  "reasons": ["...", "..."],
  "recommended_action": "monitor | contact | escalate",
  "confidence": 0.78
}
```

**Output.**

```csharp
public sealed record WatchlistEntry(
    LoanId LoanId,
    WatchlistSeverity Severity,
    IReadOnlyList<string> Reasons,
    RecommendedAction Action,
    decimal Confidence,
    AgentSessionId Session);
```

**Failure modes.**

- A loan's model call fails → it is skipped; the sweep continues. The failure is recorded in `ai.agent_sessions` with status `failed`.
- A sweep of 10,000 loans is rate-limited at the model-provider level; the batch chunks calls with concurrency `≤ 4` and cost caps.

**Guardrail profile.** Baseline. Additionally: no PII in reasons; reasons must cite one of the signal categories.

## 3. Collections

**Role.** Draft outreach text (SMS, email, or letter) for a loan in `PastDue` or `Defaulted`.

**Authority.** Zero direct send authority. Produces a draft marked `RequiresHumanApproval`. The banker reviews and approves (or edits). Only after approval does the (future, M6+) `Notifications` module send it.

**Inputs.**

- `LoanId` → aging status via `IReportingModule`.
- Channel (`SMS | Email | Letter`).
- Current local time (for after-hours check).
- Borrower contact-preferences snapshot (simulated / seeded).

**Output.**

```csharp
public sealed record CollectionsOutreach(
    OutreachChannel Channel,
    string Subject,                     // Empty for SMS / Letter.
    string Body,
    IReadOnlyList<string> AppliedDisclosures,
    IReadOnlyList<string> ComplianceFlags,
    AgentApprovalState State);          // Always RequiresHumanApproval initially.
```

**Guardrail profile.** Baseline **plus**:

- No threats, no pressure tactics, no specific legal threats.
- No disclosure of any third party (names, relationships).
- Must include mandatory regulatory disclosures (e.g., "This is an attempt to collect a debt...").
- Blocked if current local time is outside the configured contact window (default 08:00–20:00 local).

A guardrail violation returns `Refused` with the specific rules cited.

**Failure modes.**

- Loan not in a collections-eligible state → `Result.Failure(new InvalidStateTransition(...))`.
- Guardrail refusal → returned to the banker UI as a notice; they decide whether to tweak input or escalate manually.

## Prompts

System prompts live at `src/Credere.Modules.Ai/Prompts/<agent>.md`. Each prompt:

- Names the agent's role explicitly.
- States its authority limits in a bullet list.
- Lists the tools available.
- Defines the output format (JSON schema reference or prose template).
- Includes three to five few-shot examples.
- Carries a version in its frontmatter; the `CHANGELOG.md` in that directory tracks changes.

## Costing and rate limits

- Default model: Anthropic Claude Sonnet 4.6 for CreditDecisioning and Collections; Haiku 4.5 for EarlyWarning.
- Cost cap per sweep configurable in `AiOptions`. Exceeding the cap stops the sweep cleanly with partial results.
- Per-agent concurrency cap defaults to 4; configurable.

## Observability

Each agent invocation produces:

- One `ai.agent_sessions` document.
- N `ai.llm_calls` documents (one per LLM request).
- One Langfuse trace linked to the session id.
- OTEL spans attached to the containing API / batch trace.

The banker UI exposes a "view reasoning" link that opens the session document with sanitized formatting.
