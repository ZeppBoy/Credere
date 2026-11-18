---
paths:
  - "src/Credere.Modules.Ai/**"
  - "src/Credere.Mcp/**"
  - "src/Credere.Ai.Evals/**"
  - "tests/Credere.Modules.Ai.Tests/**"
  - "docs/ai/**"
---

# AI layer work (M5+)

AGENTS.md §9 and `docs/ai/agents.md` § Model requirements hold the rules. Two design questions are still open: how the Ai module reads Servicing and Origination data (open question #1), and the MCP transport (#2). Ask the owner rather than picking one.

## Claude Sonnet 5.5 request shape

Credere's agents run on `claude-sonnet-5-5`. Code written from older Claude examples tends to send shapes this model rejects:

| Older pattern | On Sonnet 5.5 |
|---|---|
| `thinking: { type: "disabled" }` or `budget_tokens` | 400. Use adaptive thinking (the default) with an explicit `effort` per agent. |
| Non-default `temperature` / `top_p` | 400. Evals can't pin sampling, so they compare means across repeated runs. |
| `tool_choice` `any` or `tool` | 400. Use `auto` with `strict: true` tool schemas, steer from the prompt, and check that the call happened. |
| Assistant-turn prefill to force JSON | 400. Use structured outputs (`output_config.format`). |
| Editing the system prompt, the tool list or earlier messages mid-session | Invalidates the thinking blocks, and is a 400 on new accounts. Keep the history append-only and pass thinking blocks back unchanged. |
| Reading `content` without checking `stop_reason` | Check for `refusal` first. Its categories are cyber, bio, frontier_llm, reasoning_extraction and general_harms. |

Size `max_tokens` for thinking plus the reply, because thinking counts toward it.

## Writing agent prompts and tool descriptions

- **Plain register.** Sonnet 5.5 follows prompts literally, so capitalised emphasis ("MUST", "NEVER") makes it over-apply a rule. State each constraint once, with its reason.
- **Tool descriptions are contracts.** Say what the tool does, when to use it and when not, what each parameter means, and what it doesn't return. Don't list tool names in the system prompt; the tool definitions already carry them.
- **Examples.** The model copies an example's length, tone and structure. Add few-shot examples only where wording matters (collections letters), use several varied ones, and label them illustrative.
- **Reasoning.** Never ask the model to reproduce its reasoning. Request a short rationale field, and read thinking summaries (`display: "summarized"`) for the audit trail.

## Finished pieces of work

- **An MCP tool:**
  - an authorized handler (JWT plus scope) with validated parameter and return records
  - a description written as a contract
  - structured logging, a Langfuse span, and an `ai.llm_calls` row, with PII redacted
  - tests in `tests/Credere.Modules.Ai.Tests` for authorization, validation and result shape
  - an entry in `docs/ai/mcp-tools.md`

  The owner verifies the tool in Claude Desktop.
- **A prompt change:** a version bump plus a `Prompts/CHANGELOG.md` entry. Ask before running `Credere.Ai.Evals`, which calls paid models.
