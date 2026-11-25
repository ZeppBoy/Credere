# Milestone plans

**Status:** Approved (2026-11-19).

Each milestone in the [roadmap](../roadmap.md) gets a plan here before work starts, as AGENTS.md §13 requires for milestone-sized work. A plan breaks the milestone into tasks that several agents can run at once under [`parallel-development.md`](../parallel-development.md).

| Milestone | Plan | Status |
|---|---|---|
| M1 — Core math + shared primitives | [`m1.md`](m1.md) | Approved |
| M2 — Origination + Servicing | [`m2.md`](m2.md) | Proposed |
| M3–M7 | [Outline below](#later-milestones-outline) | Outline |

## The shape of every milestone plan

1. **Decisions.** Open questions and doc conflicts that block tasks are listed first, each with a recommendation, the task it gates, and a column for the owner's decision.
2. **Contract tasks (serial).** A short sequence lands the public types, interfaces, ADRs and doc fixes that everything else compiles against. These tasks own the [serialized files](../parallel-development.md#serialized-files).
3. **Fan-out tasks (parallel).** Each owns disjoint paths and depends only on merged contract tasks or other merged fan-out tasks. Financial tasks write their reference vectors first, for owner approval.
4. **Close-out (serial, coordinator).** Mutation and coverage gates, the architecture tests, `docs/status.md`, the roadmap acceptance check, and the milestone tag.

Each task row gives: id, goal, dependencies, gating decision, owned paths, relative size, and whether vectors come first. The [dependency graph](m1.md#dependency-graph) shows the critical path, so implementers and the owner know which work and which reviews to prioritise.

## Later milestones (outline)

This outline shows where each milestone can run in parallel and what has to be settled first. It is not a task list. Contract gaps already visible in the docs are listed, so the contract task can fix them before fan-out.

### M3 — Documents and the banker UI

- **Decisions:** open questions #3 (identity provider), #5 (document storage) and #6 (TypeScript client generation). `modules/documents.md` still cites Directive 2008/48/EC Article 10 and checks the APRC "to four decimals". Both need updating to 2023/2225 and to the two-place disclosure in `domain/aprc.md`.
- **Contract task:** API endpoints and the OpenAPI document.
- **Fan-out:** Documents module (QuestPDF, Mongo blobs) ∥ API endpoints ∥ web UI, built against the OpenAPI contract.
- **Serial at the end:** the Playwright end-to-end test.

### M4 — Reporting, Batch and Outbox

- **Decisions:** open question #4 (batch scheduling).
- **Contract task:** projection tables and the dispatcher's handler-registration contract.
- **Fan-out:** Reporting projections split by publishing module (one implementer each for Origination's and Servicing's events) ∥ `OutboxDispatcher` and the daily accrual and aging job.
- **Serial at the end:** the batch-idempotency tests (Docker).

### M5 — MCP server

- **Decisions:** open questions #1 (how Ai reads other modules), #2 (MCP transport) and #7 (whether the `Microsoft.Extensions.AI` provider supports what Sonnet 5.5 requires). #7 is settled by a spike. A spike is M5 work, so running it early, in an isolated worktree that never merges, needs the owner's go-ahead (AGENTS.md §1).
- **Contract task:** tool parameter and result records, authorization, and the call-logging pipeline.
- **Fan-out:** the six tools, two per implementer.

### M6 — AI agents

- **Contract task:** the `AgentHost` loop and session persistence.
- **Fan-out:** credit decisioning ∥ early warning ∥ collections ∥ `PolicyFilter` rules, then the banker-UI panels.

### M7 — Evals and observability

- **Serial:** the CI pipeline. `.github/` becomes a serialized path.
- **Fan-out:** golden datasets and judge prompts, one implementer per agent.
