# Documentation

This directory is the authoritative description of Credere's intent, architecture, and status. Start here.

## Navigation

### Overview
- [`architecture.md`](architecture.md) — modular-monolith principles, module map, dependency rules, integration-event flow.
- [`frontend.md`](frontend.md) — Next.js app architecture and conventions.
- [`roadmap.md`](roadmap.md) — milestones and their acceptance criteria.
- [`status.md`](status.md) — one living document describing what actually works today.
- [`open-questions.md`](open-questions.md) — undecided design questions. Replaces "proposed" ADRs.
- [`parallel-development.md`](parallel-development.md) — how several agents work at once: worktrees, task claims, file ownership, merging. (Proposed)
- [`plans/`](plans/README.md) — milestone task breakdowns with dependencies and owned paths. (Proposed)

### Domain — shared banking concepts
- [`domain/value-objects.md`](domain/value-objects.md) — Money, Percentage, LoanTerm.
- [`domain/rates.md`](domain/rates.md) — fixed, floating, tiered, effective.
- [`domain/day-counts.md`](domain/day-counts.md) — ISDA day-count conventions.
- [`domain/schedules.md`](domain/schedules.md) — annuity, differentiated, bullet amortization.
- [`domain/aprc.md`](domain/aprc.md) — APRC under Directive (EU) 2023/2225 Annex III.
- [`domain/loan-lifecycle.md`](domain/loan-lifecycle.md) — state machine + transitions.
- [`domain/payment-allocation.md`](domain/payment-allocation.md) — allocation waterfall.
- [`domain/ifrs9-staging.md`](domain/ifrs9-staging.md) — Stage 1/2/3 rules.

### Modules — one per bounded context
- [`modules/origination.md`](modules/origination.md)
- [`modules/servicing.md`](modules/servicing.md)
- [`modules/documents.md`](modules/documents.md)
- [`modules/reporting.md`](modules/reporting.md)
- [`modules/ai.md`](modules/ai.md)

### Architecture decisions
- [`adr/`](adr/) — accepted decisions only. See [`adr/README.md`](adr/README.md) for the index.

### AI layer
- [`ai/overview.md`](ai/overview.md) — MCP + agent loop + guardrails + evals + Langfuse + Mongo artifact stores.
- [`ai/mcp-tools.md`](ai/mcp-tools.md) — tool catalog.
- [`ai/agents.md`](ai/agents.md) — credit-decisioning, early-warning, collections.
- [`ai/guardrails.md`](ai/guardrails.md) — deterministic policy filter.
- [`ai/evals.md`](ai/evals.md) — golden datasets and regression gating.

## Conventions

- Documentation describes what exists today. Planned work is explicitly marked **Planned (M<n>)**. If a doc says "Credere does X" it means it does X in the current working tree.
- All financial content cites an authoritative source (ISDA, EU Directive text, IFRS 9 standard, ECB illustrative examples).
- All domain diagrams are kept in sync with code in the same PR that changes either.
- Updates to this directory require a PR and reviewer sign-off.
