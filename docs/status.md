# Status

**Last reviewed:** 2026-11-27 (M2 progress)

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
| `Result<T, E>`, `Unit`, `DomainError` hierarchy ([ADR-0009](adr/0009-result-and-domainerror-in-core.md)) | Verified |
| Value objects (Money, Percentage, LoanTerm) | Verified |
| Day-count conventions (Act/360, Act/365 Fixed, Act/Act ISDA, 30/360 ISDA, 30E/360) | Verified |
| `IInterestCalculator`, `FixedRate`, `FloatingRate`, `TieredRate` | Verified |
| `EffectiveRate` | Planned (deferred out of M1, [open question #10](open-questions.md)) |
| Schedule generators (annuity, differentiated, bullet) | Verified |
| APRC calculator (`EuCcdAnnexIII`, solver, `AprcCalculator`) | Verified |
| Integration-event and command abstractions in `Credere.Shared` | Verified |
| Outbox and receipt rows, idempotency receipts | Planned (M2, module infrastructure) |
| Coverage (`coverlet.collector`) and mutation (Stryker.NET) tooling | Implemented |
| `Credere.Core` quality gates, measured on `f2afc82` (2026-11-25) | Core line coverage 98.92% (target ≥90%), Shared line coverage 100% (≥90%), Core mutation score 89.47% (≥80%). The APRC files score 87.12% against the plan's 85% target (#62, closed by #79). |

## M2 — Origination + Servicing modules

The solution suite passed all 1,201 tests on [`main` at `a0efde5`](https://github.com/ZeppBoy/Credere/commit/a0efde5ef1a6fac366e46ebf4ae50d0a58fff2b4), including seven architecture tests.

| Area | Status |
|---|---|
| Origination and Servicing facade interfaces, commands, queries, and versioned integration events | Implemented ([M2-C2](https://github.com/ZeppBoy/Credere/pull/84)) |
| EF Core migration tooling and persistence test project wiring | Implemented ([M2-C3](https://github.com/ZeppBoy/Credere/pull/86)) |
| `LoanApplication` aggregate, including underwriting decisions, versioned terms acceptance, withdrawal, and history replay | Verified ([M2-01](https://github.com/ZeppBoy/Credere/pull/90)) |
| Payment allocation waterfall | Verified against the approved reference cases and property tests ([M2-02](https://github.com/ZeppBoy/Credere/pull/88)) |
| Interest accrual and dated payoff calculations | Verified against the approved reference cases and property tests ([M2-03](https://github.com/ZeppBoy/Credere/pull/89)) |
| `Loan` aggregate and lifecycle, including disbursement, activation, aging, payments, payoff, terminal transitions, and history replay | Verified ([M2-04](https://github.com/ZeppBoy/Credere/pull/96)) |
| SQL Server schemas, migrations, append-only history, outbox, and per-handler receipts | Planned ([M2-05](https://github.com/ZeppBoy/Credere/issues/72), [M2-06](https://github.com/ZeppBoy/Credere/issues/73)) |
| Facade implementations and the persisted disbursement handoff | Planned ([M2-07](https://github.com/ZeppBoy/Credere/issues/74), [M2-08](https://github.com/ZeppBoy/Credere/issues/75), [M2-09](https://github.com/ZeppBoy/Credere/issues/76)) |
| End-to-end SQL Server verification from application submission through payoff | Planned ([M2-09](https://github.com/ZeppBoy/Credere/issues/76)) |

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
