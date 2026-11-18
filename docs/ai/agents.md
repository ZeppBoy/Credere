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
   - Call `IChatClient.GetResponseAsync(systemPrompt + conversation, def.Tools, ct)`.
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
- `AllowedModels` and `Effort` — model id and effort level, from configuration (see [Model requirements](#model-requirements-claude-sonnet-55)).

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

System prompts live at `src/Credere.Modules.Ai/Prompts/<agent>.md`, with a version in the frontmatter and changes recorded in `CHANGELOG.md` in that directory.

Each prompt states, in plain language:
- the agent's role and audience
- its authority limits and compliance constraints, each with its reason, because Claude Sonnet 5.5 follows prompts literally and over-applies capitalised emphasis

Prompts don't list tools, because the tool definitions carry that contract. Output shape comes from structured outputs (see [Model requirements](#model-requirements-claude-sonnet-55)), not prose templates. Few-shot examples appear only where wording matters (collections letters). Use several varied ones, labelled illustrative, because the model copies an example's length and tone.

## Costing and rate limits

- Default model: Claude Sonnet 5.5 (`claude-sonnet-5-5`) for all three agents. Effort is set per agent and tuned on the eval suite. Start at `medium` for CreditDecisioning and Collections (multi-step tool use) and at `low` for EarlyWarning (per-loan classification). EarlyWarning may move to a cheaper model only where evals show it holds quality.
- Cost cap per sweep configurable in `AiOptions`. Exceeding the cap stops the sweep cleanly with partial results.
- Per-agent concurrency cap defaults to 4; configurable.

## Model requirements (Claude Sonnet 5.5)

- **Thinking:** adaptive, which is the model's default. Sonnet 5.5 rejects `thinking: {type: "disabled"}`, `budget_tokens`, and non-default `temperature` / `top_p` with a 400. Depth is controlled only through `output_config.effort`.
- **Tool calls:** `tool_choice: auto` with `strict: true` tool schemas, because forced `any` / `tool` is a 400. The prompt says when each tool applies, and `AgentHost` checks that an expected call happened.
- **Structured output:** `output_config.format` with the agent's output schema. Assistant-turn prefill is a 400. `PolicyFilter`'s schema rule stays as the deterministic backstop.
- **History:** the conversation is append-only. Thinking blocks are passed back unchanged, and the system prompt and tool list stay fixed for the session's lifetime; editing either invalidates thinking blocks, and is a 400 on new accounts. Every tool the session may need is declared in the first request.
- **`max_tokens`:** sized for thinking plus the reply. Start at 16,000 for non-streaming requests.
- **Refusals:** `AgentHost` checks `stop_reason` before reading content. A `refusal` (categories cyber, bio, frontier_llm, reasoning_extraction, general_harms) becomes a `Refused` outcome, with its category stored in `ai.agent_sessions`. If a fallback model is configured, the session records which model produced the answer.
- **Audit trail:** the decision's `rationale` field (structured output) plus thinking summaries (`thinking.display: "summarized"`). This is what the banker UI's "view reasoning" link shows. Prompts never ask the model to write out its reasoning, because Sonnet 5.5 declines that as `reasoning_extraction`.
- **Caching:** the system prompt and tool definitions come first and stay byte-stable. Per-request data (loan facts, dates) goes after them.
- **Provider support** for these requirements through `Microsoft.Extensions.AI` is unverified ([open question #7](../open-questions.md)).

## Observability

Each agent invocation produces:

- One `ai.agent_sessions` document.
- N `ai.llm_calls` documents (one per LLM request).
- One Langfuse trace linked to the session id.
- OTEL spans attached to the containing API / batch trace.

The banker UI exposes a "view reasoning" link that opens the session document with sanitized formatting.
