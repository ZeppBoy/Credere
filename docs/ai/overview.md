# AI layer — overview

Credere's AI layer is one module (`Credere.Modules.Ai`) plus one host (`Credere.Mcp`) plus one executable (`Credere.Ai.Evals`). This document describes what the pieces are, how they fit together, and the invariants the module as a whole must satisfy.

## Pieces

| Piece | Project | Purpose |
|---|---|---|
| Ai module | `Credere.Modules.Ai` | Agents, MCP tool implementations, policy guardrails, Mongo artifact stores. The only module that talks to an LLM. |
| MCP host | `Credere.Mcp` | Thin stdio (M5) / HTTP (M6) adapter over `IAiModule.InvokeMcpToolAsync`. No logic beyond transport. |
| Evals executable | `Credere.Ai.Evals` | Runs golden datasets through the module's agents and judges the outputs. Also exposes an xunit collection so evals can run inside `dotnet test` when needed. |

## Flow for a typical agent call

```
Banker UI → Credere.Api POST /api/v1/applications/{id}/ai-credit-decision
         ↓
         IAiModule.RecommendCreditDecisionAsync(cmd)
         ↓
      1. Load application + policy inputs via IReportingModule.
      2. Create ai.agent_sessions doc with inputs digest.
      3. Enter agent loop (AgentHost):
           - IChatClient.GetResponseAsync(system + conv, tools)
           - If toolCalls: dispatch each via Credere.Modules.Ai.Mcp.*Tool,
             append ToolResult, continue.
           - Else: PolicyFilter.Apply(output).
             - Blocked: persist refusal, return refusal.
             - Pass: persist final, return structured recommendation.
      4. Append AgentRecommendationProduced to ai.Outbox in the same tx
         that finalises the session doc (two-phase: Mongo persist first,
         SQL outbox + session metadata commit second).
      5. Return Result<CreditRecommendation, DomainError> to the host.
         ↓
Credere.Api serialises to HTTP 200 (success) or 422 (refusal).
         ↓
Banker UI renders recommendation + rationale.
```

Langfuse spans wrap every LLM call. OTEL trace ids propagate from the API request through the agent into the LLM call so banker-UI → model-vendor traces compose.

## Agents

- **CreditDecisioning** — recommends Approve / Decline / HumanReview with rationale. Deterministic policy rules decide; LLM explains.
- **EarlyWarning** — nightly sweep; produces watchlist entries with reasons.
- **Collections** — drafts outreach text for banker-approved sending; subject to `PolicyFilter`.

Full details in [`agents.md`](agents.md).

## MCP tools

Six tools, exposed by `Credere.Mcp`, all delegated to the Ai module's internal dispatcher:

- `get_loan_by_id`
- `search_loans`
- `calculate_payoff`
- `get_schedule`
- `get_aging_status`
- `list_overdue`

Full spec in [`mcp-tools.md`](mcp-tools.md).

## Guardrails

A single declarative rule engine — `PolicyFilter` — applied to every agent output before it leaves the module. Rules cover banned phrases, mandatory regulatory disclosures, after-hours contact restrictions, third-party disclosure, PII in free text. Full catalog in [`guardrails.md`](guardrails.md).

## Evaluations

Golden datasets with ≥200 scenarios per agent, stored in Mongo and mirrored as JSON lines in `docs/ai/golden/`. Scored by a versioned LLM judge (model configured; see [`evals.md`](evals.md)). A regression >5% on any metric fails CI. Full process in [`evals.md`](evals.md).

## Hard rules

These apply to any change in the Ai module:

1. **No bypassing modules.** Agents and tools read via `IReportingModule`; writes go through the owning module's public contract.
2. **No agent decides without policy rules.** For CreditDecisioning, deterministic rules are the authority; the model explains, it does not decide.
3. **Every LLM call is observable.** Langfuse span + `ai.llm_calls` row + OTEL trace.
4. **No tool is anonymous.** Every MCP tool declares a required scope; the dispatcher validates.
5. **Prompts live on disk.** `src/Credere.Modules.Ai/Prompts/*.md`, versioned, with a `CHANGELOG.md`.
6. **Evals are mandatory for prompt or model changes.** CI blocks the PR on ≥5% regression.

## Non-goals

- No in-house ML model training. If credit-scoring arrives as an ML model, it becomes its own module.
- No multi-provider LLM fallback / routing. One primary (Anthropic Claude), swapped via configuration if needed.
- No vector database in M5–M7. If agent memory needs retrieval later, a new ADR introduces one.
