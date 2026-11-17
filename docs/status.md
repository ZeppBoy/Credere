# Status

**Last reviewed:** 2026-11-17 (initial scaffolding)

Statuses:
- **Planned** — in the roadmap, not implemented.
- **Implemented** — code exists and compiles.
- **Verified** — automated tests pass.
- **Reviewed** — human review recorded (financial changes require this).

## M1 — Core math + shared primitives

| Area | Status |
|---|---|
| `Credere.Core` project skeleton | Implemented |
| `Credere.Shared` project skeleton | Implemented |
| Value objects (Money, Percentage, LoanTerm) | Planned |
| Interest rates (Fixed, Floating, Tiered, Effective) | Planned |
| Day-count conventions | Planned |
| Schedule generators (annuity, differentiated, bullet) | Planned |
| APRC calculator | Planned |
| `Result<T, E>` + outbox contracts + idempotency | Planned |

## M2 — Origination + Servicing modules

| Area | Status |
|---|---|
| `Credere.Modules.Origination` skeleton | Implemented |
| `Credere.Modules.Servicing` skeleton | Implemented |
| LoanApplication aggregate + underwriting | Planned |
| Loan aggregate + state machine | Planned |
| Payment allocation waterfall | Planned |
| Aging transitions | Planned |
| SQL Server persistence + migrations | Planned |
| Append-only history tables | Planned |
| Per-handler idempotency receipts | Planned |

## M3 — Documents module + Banker UI

| Area | Status |
|---|---|
| `Credere.Modules.Documents` skeleton | Implemented |
| Next.js `web/` skeleton | Implemented |
| QuestPDF agreement/schedule/receipt generation | Planned |
| Mongo blob storage | Planned |
| Banker UI routes | Planned |
| NextAuth + IdentityServer | Planned |

## M4 — Reporting + Batch + Outbox

| Area | Status |
|---|---|
| `Credere.Modules.Reporting` skeleton | Implemented |
| `Credere.Batch` host skeleton | Implemented |
| Reporting projections | Planned |
| `OutboxDispatcher` | Planned |
| Daily accrual + aging job | Planned |

## M5 — MCP server

| Area | Status |
|---|---|
| `Credere.Modules.Ai` skeleton | Implemented |
| `Credere.Mcp` host skeleton | Implemented |
| Six MCP tools | Planned |
| Authorization + Langfuse instrumentation | Planned |

## M6 — AI agents

| Area | Status |
|---|---|
| Credit-decisioning agent | Planned |
| Early-warning agent | Planned |
| Collections agent | Planned |
| `PolicyFilter` deterministic guardrails | Planned |
| Agent-session persistence to Mongo | Planned |
| Banker-UI AI panels | Planned |

## M7 — Evals + full observability

| Area | Status |
|---|---|
| Golden datasets (≥200 scenarios per agent) | Planned |
| LLM-as-judge with versioning | Planned |
| CI regression gate | Planned |
| Aspire OTLP dashboard integration | Planned |

## Cross-cutting

| Area | Status |
|---|---|
| `Credere.ArchitectureTests` (NetArchTest, boundary enforcement) | Implemented |
| `docker-compose.yml` (SQL Server, Mongo, Langfuse, OTEL collector) | Implemented |
| Documentation set (this directory) | Implemented |
| CI pipeline | Planned (M7) |

## Cadence

This document is updated at the end of each milestone and whenever a user asks "what works today?" It is the single source of truth for delivery status. If `README.md` claims something, this document must confirm it.
