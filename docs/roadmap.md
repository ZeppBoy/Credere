# Roadmap

Credere is built in **milestones**, not calendar phases. Each milestone ends with something that runs end-to-end, a `status.md` update, and a git tag. No milestone starts before the previous one's acceptance checklist is complete.

## M1 — Core math + shared primitives

**Deliverable:** `Credere.Core` and `Credere.Shared` complete. No module logic yet.

### Acceptance
- `Money`, `Percentage`, `LoanTerm`, `InterestRate` (fixed, floating, tiered, effective), `DayCountConvention` (five conventions) implemented.
- Annuity, differentiated, bullet schedule generators implemented.
- `AprcCalculator` implemented, matching EU Consumer Credit Directive illustrative examples to four decimal places.
- `Result<T, E>`, outbox contracts, idempotency primitives in `Credere.Shared`.
- `Credere.Core.Tests`: ≥90% line coverage, ≥80% mutation score (Stryker.NET).
- Reference sources cited in XML docs and tests (ISDA, ECB, EU 2008/48/EC).
- All property-based invariants pass.

## M2 — Origination + Servicing modules

**Deliverable:** both modules with their public contracts, aggregates, payment allocation, aging. SQL Server-backed, Testcontainers-verified.

### Acceptance
- `IOriginationModule` and `IServicingModule` implemented with their commands, queries, and integration events.
- Loan state machine complete per [`domain/loan-lifecycle.md`](domain/loan-lifecycle.md). Every transition emits a domain event.
- Payment allocation waterfall implemented per [`domain/payment-allocation.md`](domain/payment-allocation.md) and verified with at least five reference cases.
- `ServicingDbContext` and `OriginationDbContext` with per-module schemas and migrations. RowVersion concurrency on every aggregate state table.
- Append-only history tables populated transactionally.
- Per-handler idempotency receipt tables populated. Duplicate-command test proves no double-emit.
- `Credere.Integration.Tests` covers the end-to-end path: SubmitApplication → DecideApplication(Approve) → DisburseLoan → RecordPayment × N → ApplyAging → Payoff.
- `Credere.ArchitectureTests` green. No module references another module.

## M3 — Documents module + Banker UI

**Deliverable:** QuestPDF-generated loan-agreement and schedule PDFs stored in MongoDB. Next.js banker UI covers the M2 happy path end-to-end.

### Acceptance
- `IDocumentsModule` with `GenerateLoanAgreementAsync`, `GenerateScheduleAsync`, `GenerateReceiptAsync`.
- Agreement PDF auto-generated on `LoanDisbursed`.
- Documents stored as Mongo blobs with SQL metadata rows; retrieval endpoint streams the PDF with correct `Content-Type`.
- Next.js banker UI routes implemented per [`frontend.md`](frontend.md) §Routes (M3 column).
- NextAuth + local IdentityServer JWT flow working.
- Playwright E2E test: open application → approve → see loan → see agreement PDF → record payment → see receipt.
- Verify.Xunit snapshots of generated PDFs (via text extraction) lock the layout.

## M4 — Reporting module + Batch + Outbox

**Deliverable:** cross-module projections populated from integration events. Daily batch runs accrual + aging + outbox dispatch end-to-end.

### Acceptance
- `IReportingModule` with dashboard queries (overdue, in-grace, upcoming disbursements, portfolio totals).
- Reporting projections updated from every integration event listed in [`architecture.md`](architecture.md) §6.
- `OutboxDispatcher` hosted service in `Credere.Batch` with idempotent delivery and retries.
- Daily batch job: interest accrual for all active loans, aging transitions, with `CommandId` idempotency.
- Idempotency tested: re-running the batch for the same business date produces no duplicate events.
- OpenTelemetry traces end-to-end from API request to outbox dispatch to projection update.

## M5 — MCP server (Ai module, part 1)

**Deliverable:** `Credere.Modules.Ai` scaffolded. `Credere.Mcp` exposes six loan tools over stdio. Usable from Claude Desktop.

### Acceptance
- Six MCP tools implemented per [`ai/mcp-tools.md`](ai/mcp-tools.md).
- Every tool authorized (JWT bearer + scope check).
- Every invocation logs to Langfuse + Mongo `ai.llm_calls`.
- `Credere.Mcp` host runs; Claude Desktop registration documented.
- `Credere.Modules.Ai.Tests` covers authorization, parameter validation, result shapes.

## M6 — AI agents (Ai module, part 2)

**Deliverable:** credit-decisioning, early-warning, collections agents on a hand-rolled loop. Deterministic policy guardrails. Agent sessions persisted to Mongo. Banker UI shows AI recommendations inline.

### Acceptance
- All three agents implemented per [`ai/agents.md`](ai/agents.md).
- `PolicyFilter` enforces every rule in [`ai/guardrails.md`](ai/guardrails.md). Unit-tested per rule.
- Agent sessions persisted to `ai.agent_sessions`. Langfuse traces attached.
- Credit-decisioning agent surfaced on `/applications/[id]` in the banker UI with rationale.
- Collections agent requires human approval before any outreach is sent.
- Early-warning agent produces a watchlist populated through integration events.
- Banned-phrase and after-hours tests pass.

## M7 — Evals + full observability

**Deliverable:** golden datasets with ≥200 scenarios per agent, LLM-as-judge with versioning, CI regression gate (>5% drop fails). Full OTEL coverage.

### Acceptance
- Golden datasets in `ai.eval_golden` (mirrored as JSON lines in `docs/ai/golden/`).
- `Credere.Ai.Evals` console app runs the full suite.
- Judge model configured (see [`ai/evals.md`](ai/evals.md) § Judge) with a versioned prompt; judge version, judge model, agent model and effort baked into every result row.
- CI config (introduced at this milestone) runs evals on PRs touching `src/Credere.Modules.Ai/**`, `src/Credere.Ai.Evals/**`, or `docs/ai/golden/**`. A 5% drop in the mean of any metric across repeated runs fails the build.
- Aspire OTLP dashboard shows traces, metrics, logs across API, Batch, Mcp, and the agent calls.
