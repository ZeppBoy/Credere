# Credere — AI Agent Operating Manual

> **Audience:** Any AI coding agent working on Credere. Treat this as authoritative. When it conflicts with your training defaults, this file wins.
> **Companion files:** [`CLAUDE.md`](CLAUDE.md) for Claude Code specifics. [`docs/roadmap.md`](docs/roadmap.md) for milestone intent. Individual module docs for bounded-context details.

Re-read the sections relevant to your current task before each significant edit. Do not assume — verify against this manual.

If a rule here seems wrong for your specific task, **stop and ask the human owner** before improvising. Never invent financial semantics.

---

## 1. Project context

Credere is a loan servicing platform with an AI augmentation layer. It is a **portfolio project**, not a product. The target audiences for the code are:

1. Fintech engineering managers evaluating the project owner.
2. Banking-domain practitioners who recognize correct day-count conventions, authentic state transitions, and sensible IFRS 9 staging.
3. AI engineers learning agentic patterns in regulated-looking domains.
4. The .NET community.

If a change would not survive the scrutiny of any of these audiences, do not make it.

---

## 2. Core principles

1. **Correctness over speed in financial logic.** Wrong interest is unrecoverable; slow interest is fixable.
2. **Domain authenticity.** Every concept that exists in real loan servicing must exist here with the right semantics, not a simplified approximation. When we deliberately omit something (e.g., a general ledger), we say so in [`docs/README.md`](docs/README.md).
3. **Make decisions visible.** Architectural choices live in [`docs/adr/`](docs/adr/) as accepted ADRs. Undecided questions live in [`docs/open-questions.md`](docs/open-questions.md). No "proposed" ADRs.
4. **Tests are not optional.** Every piece of domain logic has unit tests. Every module workflow has integration tests against real SQL Server + MongoDB via Testcontainers. AI components have evaluation suites.
5. **Idiomatic modern .NET 10.** Use the newest BCL features when they improve clarity. Avoid retro patterns.
6. **No silent assumptions.** When information is missing, ask. Never fabricate values, identifiers, dates, or rates.
7. **Transparency over cleverness.** Code is read more often than written. Optimize for the reader.
8. **Module boundaries are law.** The architecture tests enforce them. If a test blocks a change, split the module or redesign the dependency — never weaken the test.

---

## 3. Architecture — modular monolith

The project is one deployable artifact composed of **bounded context modules**: Origination, Servicing, Documents, Reporting, Ai. Each module:

- Owns its data (one `DbContext`, one SQL Server schema).
- Owns its outbox table.
- Exposes exactly one public interface (`I<Module>Module`) and a set of DTOs and integration events.
- Has `internal` default visibility for everything else.
- Does not reference another module's project.

Cross-module communication = **integration events** written to the module's outbox in the same transaction as the state change. A background `OutboxDispatcher` relays events to subscribing modules (in-process today; a message broker tomorrow with zero module changes).

Hosts (`Credere.Api`, `Credere.Batch`, `Credere.Mcp`) are composition roots. They wire module DI registrations and translate transport input/output. They do not embed business logic.

The React UI talks only to `Credere.Api` over HTTP.

Read [`docs/architecture.md`](docs/architecture.md) for the full picture.

---

## 4. Project structure

```
src/
  Credere.Core/                  Shared kernel: value objects, math. NodaTime only.
  Credere.Shared/                Cross-cutting primitives: Result, outbox contracts, idempotency.
  Credere.Modules.Origination/   LoanApplication aggregate; underwriting.
  Credere.Modules.Servicing/     Loan aggregate; payments (allocation); aging; payoff.
  Credere.Modules.Documents/     QuestPDF generation; Mongo blob storage.
  Credere.Modules.Reporting/     Dashboard read models (pure event consumer).
  Credere.Modules.Ai/            Agents + MCP tools + guardrails + Mongo artifact stores.
  Credere.Api/                   Minimal-API host. Wires modules.
  Credere.Batch/                 Worker host. Daily accrual + aging + outbox dispatch.
  Credere.Mcp/                   MCP stdio host.
  Credere.Ai.Evals/              Executable evaluation console.
tests/
  Credere.Core.Tests/            Unit + property-based.
  Credere.Shared.Tests/
  Credere.Modules.*.Tests/       Per-module unit + lightweight integration.
  Credere.Integration.Tests/     End-to-end via WebApplicationFactory + Testcontainers.
  Credere.ArchitectureTests/     NetArchTest. Enforces module boundaries. BUILD-BREAKING.
web/                             Next.js 15 banker UI (App Router, TypeScript).
docs/                            Human-maintained documentation.
```

Rules:

- `Credere.Core` and `Credere.Shared` are the only projects that may be referenced by multiple modules.
- A module project (`Credere.Modules.X`) may reference only `Credere.Core` and `Credere.Shared`.
- Hosts may reference modules through their public contracts (`I<Module>Module` + DTOs + integration events). The module project is referenced for DI registration only.
- Tests may reference whatever they need to test.
- `Credere.Ai.Evals` is a side-car executable: it may reference `Credere.Modules.Ai` to replay prompts.

---

## 5. Technology stack — locked

| Area | Choice |
|---|---|
| Runtime | .NET 10, C# latest |
| HTTP | ASP.NET Core minimal APIs |
| Primary DB | SQL Server 2022 (Developer Edition locally) |
| Secondary DB | MongoDB 7 (AI artifacts + document blobs) |
| ORM | EF Core 10, one `DbContext` per module |
| Dates / money | NodaTime, custom `Money`, `Percentage`, `InterestRate`, `DayCountConvention` |
| Mediation | Direct `ICommandHandler<T, TR>` / `IQueryHandler<T, TR>` interfaces (no MediatR) |
| Validation | `System.ComponentModel.DataAnnotations` + explicit `Validate` methods returning `Result<T, ValidationError>` (no FluentValidation) |
| Mapping | Manual via record constructors / extension methods (no Mapster / AutoMapper) |
| PDF | QuestPDF |
| Architecture tests | NetArchTest |
| Testing | xUnit, FluentAssertions, FsCheck.Xunit, Verify.Xunit, Testcontainers, Bogus, Stryker.NET (mutation on `Credere.Core` only) |
| AI abstraction | `Microsoft.Extensions.AI` |
| Agent orchestration | Hand-rolled loop on MEAI. No Semantic Kernel. |
| MCP | `ModelContextProtocol` C# SDK |
| Observability | OpenTelemetry + Langfuse |
| Frontend | Next.js 15 App Router, TypeScript strict, Tailwind v4, shadcn/ui, pnpm |
| Auth | Local IdentityServer (dev); JWT bearer |

**Do not propose alternatives unless explicitly asked.** New dependencies require an ADR.

---

## 6. Coding standards

### General
- Public types are `sealed` by default. Inheritance is opt-in and must be justified.
- Public APIs are documented with XML comments.
- Prefer composition over inheritance. Prefer interfaces over abstract classes without shared implementation.
- One type per file. Small, tightly-related types may share a file (e.g., a record + its validator).

### Nullability
- `<Nullable>enable</Nullable>` is on globally.
- No `null` in public APIs. Use `Maybe<T>`, `Result<T, E>`, or explicit `?`.
- **Never** use `null!`. If you need it, the design is wrong.

### Async
- All async methods take `CancellationToken cancellationToken`.
- Use `ConfigureAwait(false)` in library code (`Credere.Core`, `Credere.Shared`, module libraries).
- Never call `.Result`, `.Wait()`, or `GetAwaiter().GetResult()`.

### Error handling
- Domain and application operations that can fail return `Result<T, DomainError>`. **All of them.** The semantics are uniform across every command and query handler.
- Throw exceptions **only** for programmer bugs or configuration errors (null args from DI, missing environment variables, unreachable code paths).
- An invalid state transition is an expected failure and returns `Result<T, DomainError>`. It does NOT throw.
- Custom exceptions live in a project's `Exceptions/` folder and inherit from a project-specific base.
- Never catch `Exception` without rethrowing or logging full context.

### Naming
- Aggregates: `Loan`, `LoanApplication`, `Payment`, `Document`.
- Value objects: `Money`, `InterestRate`, `Percentage`, `LoanTerm`.
- Commands: imperative verbs — `DisburseLoan`, `RecordPayment`.
- Queries: descriptive nouns — `GetLoanById`, `ListOverdueLoans`.
- Events (domain, internal): past tense — `LoanActivated`, `PaymentReceived`.
- Integration events (public, cross-module): past tense — `LoanDisbursed`, `PaymentRecorded`.
- Services: `IInterestCalculator`, `IScheduleGenerator`.
- Tests: `<ClassUnderTest>Tests` for unit, `<Feature>Tests` for integration.

### Formatting
- `dotnet format` before every commit. CI rejects unformatted code.
- 4-space indent, no tabs. File-scoped namespaces.

### Idempotency
- Every command carries a `CommandId`. Handlers persist `(CommandId, Result)` in a per-handler receipt table within the same transaction. Duplicate commands return the original result.
- Integration-event handlers are idempotent by subscriber + event-id.

---

## 7. Domain modeling rules

### Aggregates
- Single entry point (aggregate root).
- Invariants enforced in the constructor and in every command method.
- Commands return `Result<T, DomainError>` on success/business-failure; emit domain events on success.
- Aggregates do not call external services. Application services orchestrate; aggregates are pure.

### State machines
- States and transitions are explicit in code (not inferred from bool flags).
- Invalid transitions return `Result<T, DomainError>` with full context. No exceptions.
- Every transition emits a domain event.
- Diagrams in [`docs/domain/loan-lifecycle.md`](docs/domain/loan-lifecycle.md) must stay in sync with code.

### Value objects
- `sealed record` with value-based equality.
- Immutable. Operations return new instances.
- Invariants validated in the constructor.
- Override `ToString()` for readable logs.

### Domain events (internal to a module)
- Named in past tense, describe what happened.
- Carry enough data that consumers don't need to re-query the aggregate.
- Immutable records.

### Integration events (public, cross-module)
- Published via the module's outbox in the same transaction as the state write.
- Versioned via a `[Version(n)]` attribute. Breaking changes require an upcaster and a migration note in the module doc.

---

## 8. Testing requirements

### Coverage targets
- `Credere.Core`: ≥90% line, ≥80% mutation (Stryker.NET).
- `Credere.Shared`: ≥90% line.
- `Credere.Modules.*`: ≥85% line for Application + Domain; Infrastructure covered by integration tests.
- AI: evaluation suites pass thresholds in [`docs/ai/evals.md`](docs/ai/evals.md).

### What to test
- Every public method of every aggregate.
- Every state transition (valid and invalid).
- Every value object's algebraic properties where applicable.
- Every API endpoint's happy path and ≥3 failure modes.
- Every batch operation's idempotency.
- Every interest calculation against known reference values from ISDA / EU / ECB documentation.
- Every architecture rule in `Credere.ArchitectureTests`.

### Property-based tests
Use FsCheck.Xunit for:
- `Money` addition associativity for same currency.
- `InterestRate.Apply(principal, period).Sum() == TotalInterest(principal, rate, term)` within rounding tolerance.
- Schedule generation: sum of principal portions equals original principal.
- APRC calculation: round-trip stability.

### Integration tests
- Testcontainers for real SQL Server + MongoDB. Never mock the database.
- Each test owns its data. No cross-test ordering dependencies.
- Clean state via transaction rollback or per-test schema reset — not by deleting rows.

### Snapshot tests
- Verify.Xunit for generated schedules and generated PDFs (via text extraction).

---

## 9. Banking domain semantics — critical rules

### Never guess on financial math
If you do not know the correct day-count convention, rounding rule, leap-year handling, or APRC iteration method — **stop and ask the human owner**. Do not invent. Do not extrapolate.

LLMs are known to produce plausible-looking but subtly incorrect financial code. Verification against reference data is mandatory.

### Day-count conventions
- Live in `Credere.Core/Calculations/DayCounts/`.
- Each has ≥20 test vectors sourced from ISDA documentation or ECB illustrative examples.
- Reference sources cited in the implementation file's XML comment.
- Formulas documented in [`docs/domain/day-counts.md`](docs/domain/day-counts.md).

### Interest calculation
- Only path is `IInterestCalculator`.
- Never write inline interest math in handlers, services, or endpoints.
- Rounding: banker's (`MidpointRounding.ToEven`) unless a specific business rule says otherwise. Document the choice in code.

### Payment allocation
- Waterfall: fees → past-due interest → current interest → principal.
- Documented in [`docs/domain/payment-allocation.md`](docs/domain/payment-allocation.md) with a citation.
- Lives in the Servicing module's `PaymentAllocationWaterfall` class.
- **No stubs on the production path.** Payment allocation is present from the first slice that touches payments.

### APRC
- `AprcCalculator` implements iterative solver per EU Consumer Credit Directive 2008/48/EC (and 2023/2225 where applicable).
- Reference cases match official EU examples to four decimal places.
- Never modify without re-running the full reference suite.

### IFRS 9 staging
- Logic in `IIfrs9StagingService`.
- Triggers documented in [`docs/domain/ifrs9-staging.md`](docs/domain/ifrs9-staging.md).
- Do not modify staging without a documentation + owner discussion.

---

## 10. AI layer rules

### MCP tool design
- Every tool requires authorization. No anonymous tools.
- Parameter schemas are explicit and validated. No `object`, no `dynamic`.
- Return shapes are structured records with documented fields.
- Tool descriptions are written for an LLM consumer: when to use the tool, not just what it does.
- Every invocation logged with: tool name, arguments (PII redacted), result hash, latency, cost, Langfuse trace id.

### Agent prompts
- System prompts live in `src/Credere.Modules.Ai/Prompts/` as `.md` files, not inline strings.
- Prompt versions tracked. Changes increment the version and document the change in `src/Credere.Modules.Ai/Prompts/CHANGELOG.md`.
- Prompts include: role, authority limits, compliance constraints, output format, few-shot examples.

### Evaluations
- Every agent has a golden dataset (≥200 scenarios) in MongoDB `ai.eval_golden`.
- Evaluations run on every PR touching `src/Credere.Modules.Ai/**` or `docs/ai/prompts/**`.
- A 5% drop on any metric fails CI.
- LLM-as-judge prompts are versioned; the version is baked into every result row.

### Observability
- All LLM calls flow through Langfuse instrumentation.
- Cost, latency, quality metrics tagged per agent.
- Errors and refusals surfaced in dashboards, not just logs.

### Safety and compliance
- Collections agent: no threats, no third-party disclosure, no contact outside permitted hours, mandatory regulatory disclosures. Enforced by `PolicyFilter` — a deterministic rules engine, not another LLM.
- Decision-supporting agents produce reasoning traces for audit.
- Refusals are logged with full context.

---

## 11. What you should do

1. **Read the relevant files first.** Before editing a module, read its doc under `docs/modules/` and relevant ADRs.
2. **Plan before acting.** For non-trivial changes, write a short plan before edits.
3. **Make atomic changes.** One commit, one thing. Multi-step changes get multiple commits.
4. **Write tests alongside code, not after.**
5. **Run tests before claiming done.** "Wrote the code" ≠ "verified it works."
6. **Run `dotnet format` before committing.**
7. **Update documentation.** ADR, state-machine diagram, module doc, README — same PR.
8. **Write clear Conventional-Commits messages.** Body explains why.
9. **Escalate uncertainty.** When unsure about banking semantics, security, or architectural intent — ask the human.

---

## 12. What you must never do

1. **Never invent financial formulas, rates, dates, or identifiers.**
2. **Never bypass tests** to make a change "look complete."
3. **Never weaken an architecture test** to allow a cross-module dependency. Fix the design.
4. **Never modify generated files directly** (EF migrations, OpenAPI output). Regenerate.
5. **Never add secrets to source control.** `dotnet user-secrets` locally, env vars in CI.
6. **Never log PII in plain text.** Customer names, account numbers, IDs go through redaction.
7. **Never disable a security check** without a security ADR.
8. **Never commit code that does not build.** `dotnet build` must succeed.
9. **Never silence a compiler warning** with `#pragma warning disable` without justification.
10. **Never use `dynamic`, `object`, or `var`-as-substitute-for-typing** in public APIs.
11. **Never change a state transition rule** without updating the state machine documentation.
12. **Never delete an ADR.** ADRs are append-only. Superseded ADRs are marked superseded and link to the replacement.
13. **Never delete tests** to make the build green. Failing tests are signals, not obstacles.
14. **Never put business logic in a host** (`Credere.Api`, `Credere.Batch`, `Credere.Mcp`). Hosts are composition roots.

---

## 13. Escalate to the human when

1. You cannot find an authoritative reference for a financial calculation.
2. A banking concept in the code seems wrong but a test asserts it is correct.
3. A security control appears to block a feature.
4. You would need to delete or significantly change a test.
5. The task asks you to do something this document forbids.
6. Two pieces of guidance contradict (a code comment vs. an ADR).
7. You are asked to add a dependency that conflicts with the locked stack.
8. You are uncertain which module an action belongs to.

### How to escalate
- Stop the current task.
- Summarize: what you're doing, what's blocking you, options, your recommendation + why.
- Wait for input. Do not guess.

---

## 14. Working style

- **Concise.** Minimal narration around code changes.
- **Honest.** If a change may have side effects, say so. If uncertain, say so.
- **Incremental.** Small verifiable steps over large speculative changes.
- **Skeptical.** Ask yourself if a banking analyst would find your change wrong. If yes, fix it.

---

*Updates to this file require human approval via PR.*
