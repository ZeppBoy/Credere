# Module — Ai

**Project:** `src/Credere.Modules.Ai/`
**Schema:** `ai` (SQL, sparse — mostly for `Watchlist` + outbox + receipts)
**Mongo collections:** `ai.agent_sessions`, `ai.llm_calls`, `ai.eval_results`, `ai.eval_golden`, `ai.agent_memory`
**Public contract:** `IAiModule`

## Bounded context

The Ai module hosts three agents and the MCP tool catalog they share. It is the only module with external-model (LLM) dependencies. Agents never bypass domain logic: they call other modules' public interfaces (via `IReportingModule` for reads; via the owning modules for writes that result from a human-approved recommendation).

See [`../ai/overview.md`](../ai/overview.md) for the technical details of the agent loop, guardrails, and evaluations. This file covers the module's bounded context, public contract, persistence, and integration events.

## Public interface

```csharp
public interface IAiModule
{
    Task<Result<CreditRecommendation, DomainError>> RecommendCreditDecisionAsync(
        RecommendCreditDecision cmd, CancellationToken ct);

    Task<Result<Unit, DomainError>> RunEarlyWarningSweepAsync(
        RunEarlyWarningSweep cmd, CancellationToken ct);

    Task<Result<CollectionsOutreach, DomainError>> DraftCollectionsOutreachAsync(
        DraftCollectionsOutreach cmd, CancellationToken ct);

    IReadOnlyList<McpToolDescriptor> GetMcpTools();

    Task<Result<McpToolResult, DomainError>> InvokeMcpToolAsync(
        InvokeMcpTool cmd, CancellationToken ct);

    Task<AgentSessionView?> GetSessionAsync(AgentSessionId id, CancellationToken ct);
}
```

## Integration events

### Published

| Event | When | Payload |
|---|---|---|
| `AgentRecommendationProduced` | Any agent returns a final answer (not a refusal) | Agent name, inputs digest, recommendation, rationale, session id, correlation id |

### Consumed

| Event | Handler |
|---|---|
| `ApplicationSubmitted` | Trigger `RecommendCreditDecisionAsync` for the application (optional; may be banker-initiated). |
| `LoanAged` | Feed signal into the early-warning watchlist. |
| `PaymentRecorded` | Feed signal into the early-warning watchlist. |

## Agents

### 1. Credit-decisioning

- **Input:** application id → read from `IReportingModule`; fake-bureau score from a seeded service.
- **Deterministic rules run first.** If the rules decide the outcome unambiguously, the LLM is asked only to explain in plain language. The LLM **does not override** the rules.
- **Output:** `Approve | Decline | HumanReview` + structured rationale.
- **Rationale structure** lets the banker UI display it inline.

### 2. Early-warning

- **Trigger:** daily, via `RunEarlyWarningSweep` (called from `Credere.Batch`).
- **Input:** portfolio snapshot (current `reporting.Loans`) + seeded external signals (bureau delta, employment status, generic payment-behavior features).
- **Output:** watchlist rows (one per loan found risky) with reasons, stored in `reporting.Watchlist`.
- **Reasoning saved to** `ai.agent_sessions` for audit.

### 3. Collections

- **Input:** loan id + borrower contact state (opt-in channels, hours, prior outreach history).
- **Output:** outreach draft text (SMS, email, letter).
- **Guardrails apply:** `PolicyFilter` enforces no threats, no third-party disclosure, after-hours blocking, mandatory disclosures. Output is marked `RequiresHumanApproval` — no automatic sending.

## Persistence — `ai` schema (SQL, sparse)

| Table | Purpose |
|---|---|
| `Outbox` | Integration events pending dispatch. |
| `ProjectionReceipts_<Event>` | Idempotency for subscribed integration events. |
| `CommandReceipts_<Cmd>` | Command idempotency per handler. |
| `Watchlist` | Early-warning results (duplicated into `reporting.Watchlist` by the Reporting projection). |

## Persistence — Mongo

| Collection | Purpose |
|---|---|
| `ai.agent_sessions` | One doc per agent invocation. Full session transcript: inputs digest, tool calls, final output / refusal, guardrail decisions, start/end timestamps, cost, model version. |
| `ai.llm_calls` | One doc per LLM request/response. Prompt, response, token counts, cost, latency, model id, timestamp, session id. |
| `ai.eval_results` | Per-scenario eval results (judge score, pass/fail, rationale). Versioned by judge-prompt version. |
| `ai.eval_golden` | Golden datasets (per agent). |
| `ai.agent_memory` | Optional per-agent, per-session scratchpad. |

## Agent loop — one-paragraph version

The hand-rolled loop (`AgentHost` class) initialises an `ai.agent_sessions` doc, enters a loop: call the chat client with the current conversation + available tools, if tool calls are returned dispatch them (via the internal tool dispatcher, which consults the MCP catalog), append tool results to the conversation, continue. When the model returns a final text response, apply `PolicyFilter`; on refusal, persist the refusal reason; on pass, persist the final response. Return the structured result.

## MCP tools

The module publishes the MCP tool catalog; `Credere.Mcp` is just a host that delegates everything to `IAiModule.InvokeMcpToolAsync`. The six tools are described in [`../ai/mcp-tools.md`](../ai/mcp-tools.md).

## Guardrails

`PolicyFilter` is a declarative rule engine. Rules currently:

- Banned-phrase list (threats, discriminatory language).
- Mandatory disclosure presence check (e.g., "You have the right to request a free credit assessment within 14 days").
- After-hours contact blocking (configurable per locale).
- Third-party disclosure block (if the output mentions a party other than the borrower by name).

Each rule is unit-tested. Full catalog: [`../ai/guardrails.md`](../ai/guardrails.md).

## Observability

- Every LLM call is wrapped in a Langfuse span with session id, cost, latency, and model id.
- Every guardrail decision is tagged on the span.
- OpenTelemetry trace ids are shared with the API host trace so a banker UI request can be traced end-to-end.

## Feature slices (M5 scaffolded, M6 complete)

```
Credere.Modules.Ai/Application/
├── Commands/
│   ├── RecommendCreditDecision/
│   ├── RunEarlyWarningSweep/
│   └── DraftCollectionsOutreach/
├── Mcp/
│   ├── GetLoanByIdTool.cs
│   ├── SearchLoansTool.cs
│   ├── CalculatePayoffTool.cs
│   ├── GetScheduleTool.cs
│   ├── GetAgingStatusTool.cs
│   └── ListOverdueTool.cs
├── Agents/
│   ├── CreditDecisioningAgent.cs
│   ├── EarlyWarningAgent.cs
│   └── CollectionsAgent.cs
├── PolicyFilter/
│   ├── PolicyFilter.cs
│   └── Rules/
└── Infrastructure/
    ├── AiSqlDbContext.cs
    ├── AgentSessionStore.cs   (Mongo)
    ├── LlmCallLog.cs          (Mongo)
    └── OutboxBridge.cs
```

## Open items

- See [`../open-questions.md`](../open-questions.md) #1 (how Ai reads Servicing/Origination — leaning toward reading from Reporting).
- See [`../open-questions.md`](../open-questions.md) #2 (MCP transport — stdio first).

## Non-goals (inside the Ai module)

- No training of ML models (ECL, credit scoring). Inputs come from LLM reasoning or seeded fake services. If a real credit scoring model is added, it belongs in a new Scoring module with its own ADR.
- No multi-model routing / fallback across providers. Primary is Anthropic Claude via `Microsoft.Extensions.AI`; if a specific tool needs OpenAI, swap the IChatClient in configuration.
- No vector database usage in M5–M7. If agent memory needs retrieval, add Qdrant or similar via a new ADR.
