# Credere — Agent Operating Manual

<!--
Maintainers: tuned for current Claude models (written against Claude Sonnet 5.5, 2026-11).
Plain register on purpose: current models follow instructions literally and over-apply capitalised
or bolded emphasis, so rules carry their reason instead. Re-audit with `/doctor prompt-audit`
whenever the default model changes. Changes need owner approval via PR.
Codex reads only this file (CLAUDE.md is Claude-only) and caps it at 32 KiB (project_doc_max_bytes), so
rules every tool must follow belong here, and the file should stay under about 28 KiB.
-->

This file applies to every AI coding agent and every human contributor. Delivery status is in [`docs/status.md`](docs/status.md), milestones in [`docs/roadmap.md`](docs/roadmap.md), and the design in [`docs/architecture.md`](docs/architecture.md) and [`docs/adr/`](docs/adr/). If a rule here seems wrong for your task, ask the owner instead of working around it.

## 1. Context

Credere is a term-loan servicing platform with an AI layer, built as a portfolio project and held to production standards. Its readers are fintech engineering managers, banking practitioners who will check day counts, state transitions and IFRS 9 staging, AI engineers, and the .NET community. A change is done when it would survive their review.

Work proceeds milestone by milestone, M1 to M7. `docs/status.md` records what actually exists, so check it before assuming a type, table or endpoint is there. Before starting work that belongs to a later milestone than the current one, say so.

## 2. Commands

| Task | Command |
|---|---|
| Start dependencies | `docker compose up -d` (SQL Server 2022, MongoDB 7, OTEL collector, Langfuse) |
| Build (warnings are errors) | `dotnet build Credere.slnx` |
| All tests | `dotnet test` from the repo root; integration tests need Docker running |
| One project / one test | `dotnet test tests/<Project>` · add `--filter "FullyQualifiedName~<Name>"` |
| Architecture tests | `dotnet test tests/Credere.ArchitectureTests` |
| Format / check format | `dotnet format Credere.slnx` · `dotnet format Credere.slnx --verify-no-changes` |
| Frontend | `pnpm --dir web install` · `pnpm --dir web lint` · `pnpm --dir web build` |
| Mutation tests (`Credere.Core`) | Stryker.NET. Not configured yet; it lands in M1. |
| AI evaluations | `src/Credere.Ai.Evals`. It calls paid models, so ask before running it. It is not part of `dotnet test`. Planned (M7). |

There is no CI until M7, so the local checks in §11 are the only gate.

## 3. Architecture

Credere is a modular monolith: one deployable with five bounded-context modules, **Origination, Servicing, Documents, Reporting, Ai**. The boundaries are what make the project credible, and `tests/Credere.ArchitectureTests` enforces them as build failures.

- Each module owns one `DbContext`, one SQL Server schema, and one outbox table.
- A module exposes one facade, `I<Module>Module`, plus public command/query DTOs, strongly typed ids, views and integration events. Everything else is `internal` ([ADR-0008](docs/adr/0008-module-public-contracts.md)), and modules never grant `InternalsVisibleTo` to each other.
- A module project references only `Credere.Core` and `Credere.Shared`. Modules talk to each other only through integration events, written to the outbox in the same transaction as the state change ([ADR-0005](docs/adr/0005-transactional-outbox.md)).
- `Credere.Core` depends on NodaTime only. `Credere.Shared` depends on the BCL only.
- Hosts (`Credere.Api`, `Credere.Batch`, `Credere.Mcp`) are composition roots: DI wiring and transport translation, no business logic. `web/` talks only to `Credere.Api` over HTTP.
- When an architecture test blocks a change, the dependency needs redesigning, usually as an integration event. Weakening the test would hide the problem the test exists to catch.

```
src/   Credere.Core (value objects, math, Result, DomainError) · Credere.Shared (event and command markers)
       Credere.Modules.{Origination,Servicing,Documents,Reporting,Ai}
       Credere.Api · Credere.Batch · Credere.Mcp (hosts) · Credere.Ai.Evals (eval console)
tests/ Credere.{Core,Shared}.Tests · Credere.Modules.*.Tests · Credere.Integration.Tests · Credere.ArchitectureTests
web/   Next.js banker UI          docs/  architecture, ADRs, domain, modules, AI
```

The internal layout of a module is described in [`docs/architecture.md` §4](docs/architecture.md).

## 4. Locked stack

| Area | Choice |
|---|---|
| Runtime / HTTP | .NET 10, C# latest, ASP.NET Core minimal APIs |
| Data | SQL Server 2022 + EF Core 10 (one `DbContext` per module); MongoDB 7 (AI artifacts, document blobs) |
| Dates / money | NodaTime; custom `Money`, `Percentage`, `InterestRate`, `DayCountConvention` |
| Mediation / mapping | Direct `ICommandHandler<T, TR>` / `IQueryHandler<T, TR>`; manual mapping. No MediatR, AutoMapper, or Mapster |
| Validation | `Create(...)` and `Validate` methods returning `Result<T, DomainError>` with `ValidationError` ([ADR-0006](docs/adr/0006-error-semantics.md)); DataAnnotations at the transport edge. No FluentValidation |
| PDF | QuestPDF |
| Testing | xUnit, FluentAssertions (pinned below v8, which changed licence), FsCheck.Xunit, Verify.Xunit, Testcontainers, Bogus, NetArchTest, Stryker.NET (`Credere.Core` only) |
| AI | `Microsoft.Extensions.AI` with a hand-rolled agent loop (no Semantic Kernel); `ModelContextProtocol` C# SDK; agents target Claude Sonnet 5.5 (§9) |
| Observability | OpenTelemetry + Langfuse |
| Frontend | Next.js 15 App Router, TypeScript strict, Tailwind v4, shadcn/ui (copied in), pnpm via Corepack |
| Auth | JWT bearer. The dev OIDC provider is undecided ([open question #3](docs/open-questions.md)) |

The stack is fixed so the codebase stays coherent for its readers. Propose alternatives only when asked. A new NuGet or npm dependency needs an ADR and owner approval. Versions live in `Directory.Packages.props` and `web/package.json`.

## 5. Coding standards

- **Types:** public types are `sealed` unless inheritance is justified. One type per file, except a tightly related pair such as a record and its validator. File-scoped namespaces.
- **Docs:** public APIs have XML doc comments. CS1591 is suppressed in `Directory.Build.props` until M1, so the build won't remind you.
- **Nullability:** nullable reference types are on and warnings are errors. Optional values are `T?`, and operations that can fail return `Result<T, DomainError>`. `null!` hides a design problem, so fix the design instead.
- **Public API types:** no `dynamic` and no `object`. A `#pragma warning disable` needs a comment that justifies it.
- **Async:** every async method takes a `CancellationToken cancellationToken`. Library projects use `ConfigureAwait(false)`. Never block on async code with `.Result`, `.Wait()` or `GetAwaiter().GetResult()`.
- **Errors ([ADR-0006](docs/adr/0006-error-semantics.md)):** expected business failures (validation, invalid transition, not found, concurrency conflict) are returned as `Result<T, DomainError>` from aggregate methods and handlers. Throw only for bugs and configuration errors. Catch `Exception` only to rethrow, or to log with full context.
- **Idempotency ([ADR-0007](docs/adr/0007-command-idempotency.md)):** every command carries a `CommandId`. The handler stores a receipt in the same transaction as the state change and the outbox write. Integration-event handlers are idempotent per subscriber and event id.
- **Time:** domain code never reads the clock; the business date or `Instant` arrives as a parameter, so batch runs can be replayed for any date. Application code takes NodaTime `IClock` from DI, and `SystemClock.Instance` appears only in host composition.
- **Ids:** aggregate ids are strongly typed `readonly record struct`s such as `LoanId`, so the compiler stops a `LoanId` from being passed where a `LoanApplicationId` is expected. `CommandId` and `CorrelationId` stay `Guid`.
- **Culture:** analyzer CA1305 is on. Machine-readable formatting and parsing use `CultureInfo.InvariantCulture`. Culture-specific formatting happens only in PDFs and the UI, with an explicit culture.
- **Analyzers:** CA1848/CA2254 (`[LoggerMessage]` logging with constant templates), CA1852 (sealed internal types) and CA2016 (forward `CancellationToken`) already fail the build. Fix the code rather than suppress them.
- **Visibility for tests:** a module grants `InternalsVisibleTo` only to its own test project.
- **Naming:** commands are imperative (`DisburseLoan`), queries are nouns (`GetLoanById`), and domain and integration events are past tense (`LoanActivated`, `LoanDisbursed`). Unit test classes are named `<ClassUnderTest>Tests`, integration test classes `<Feature>Tests`.

## 6. Domain modelling

- **Aggregates:** one root per aggregate. Invariants are enforced at construction and in every command method. Aggregates do no I/O; application services orchestrate.
- **State machines:** states and transitions are explicit types, never inferred from bool flags. An invalid transition returns `InvalidStateTransition`. Every transition emits a domain event. Update [`docs/domain/loan-lifecycle.md`](docs/domain/loan-lifecycle.md) first, then the code, in the same commit.
- **Value objects:** `sealed record` with get-only properties and a readable `ToString()`. Untrusted input goes through a static `Create(...)` that returns `Result<T, DomainError>`. Constructors and operators throw only on programmer error, such as mixing currencies inside the domain. Types with invariants avoid positional parameters, because a `with` expression would bypass validation. A value object that holds a collection implements equality itself, because record equality compares collections by reference.
- **Events:** domain events are internal to a module and carry enough data that consumers never re-query. Integration events are public records under `IntegrationEvents/`, versioned with `[Version(n)]`. A breaking change needs an upcaster and a note in the module doc.

## 7. Financial correctness

Interest that has been charged wrongly cannot be quietly corrected later. Plausible-looking financial code is often subtly wrong: rounding in the wrong direction, an off-by-one day count, a mishandled grace period, or wrong compounding. So treat every calculation as unverified until it matches cited reference values. If `docs/domain/` doesn't define the day-count convention, rounding rule, leap-year or grace-period handling, or the APRC method a task needs, ask the owner rather than choosing one. Rates, dates, amounts and identifiers come from the task, the code, or a cited source, never from invention.

Regulatory and market-convention texts (EUR-Lex, ECB, ISDA, EBA, BIS) change and are easy to misremember. When web access is available, check the current source even if you feel sure. Cite the document, section and example number in the XML doc and in the test.

- **Time:** use NodaTime throughout domain and application code: `LocalDate` for business dates, an explicit `DateTimeZone` for jurisdiction-specific cut-offs such as end of day, and no `DateTime`, `DateTimeOffset` or `TimeSpan`. Banking days are local, so UTC is never assumed.
- **Money:** domain signatures take `Money`, not raw `decimal`. `Money` rounds to two decimal places with `MidpointRounding.ToEven` when it is created ([value-objects.md](docs/domain/value-objects.md)). That makes every `Money` creation a rounding point, so intermediate math stays in `decimal` until a domain doc says to produce money.
- **Rounding:** every rounding call passes its `MidpointRounding` explicitly, even where the default would be ToEven, so the call site shows the choice. Other rules apply only where a domain doc defines them.
- **Number types:** no `double` or `float` in financial math. The one documented exception is the fractional exponent inside the APRC solver ([aprc.md](docs/domain/aprc.md)), which converts back to `decimal` at a single boundary.
- **Day counts:** day differences in calculations come only from a `DayCountConvention`, never from `Period.Between`, `Period.DaysBetween` or date subtraction.
- **Interest** goes through `IInterestCalculator`, so every calculation is in one auditable place.
- **Reference cases:** a day-count convention needs ≥20 vectors from ISDA or ECB sources. APRC must reproduce every worked example in Directive 98/7/EC, Annex II, at the precision the example prints ([aprc.md](docs/domain/aprc.md#reference-examples)). Every other calculation needs ≥5 reference cases from a cited source.
- **Payment allocation** goes fees → past-due interest → current interest → principal, in Servicing's `PaymentAllocationWaterfall` ([doc](docs/domain/payment-allocation.md)). It is present, without stubs, from the first slice that touches payments.
- **APRC:** `AprcCalculator` follows Directive (EU) 2023/2225, Article 30 and Annex III, which replaced Directive 2008/48/EC from 20 November 2026. Any change re-runs the full reference suite.
- **IFRS 9 staging** lives in `IIfrs9StagingService`. Change it only together with its doc, after discussing it with the owner.

## 8. Testing

- **Targets:** `Credere.Core` ≥90% line and ≥80% mutation. `Credere.Shared` ≥90% line. Module Application and Domain code ≥85% line. Infrastructure is covered by integration tests.
- **What gets tested:**
  - every public aggregate method
  - every state transition, valid and invalid
  - value-object algebra
  - each API endpoint's happy path plus ≥3 failure modes
  - batch idempotency
  - every architecture rule
- **Property-based tests (FsCheck):**
  - `Money` associativity within one currency
  - per-period interest summing to the total within tolerance
  - schedule principal summing to the original principal
  - APRC round-trip stability
- **Real dependencies:** pure domain logic is tested directly. Persistence is tested against real SQL Server and MongoDB via Testcontainers. EF Core, Mongo and the LLM are never mocked, because those mocks hide the bugs that matter here. There is no mocking library in the stack; hand-written fakes are only for ports at the module edge, such as `IClock`.
- **Isolation and determinism:**
  - each test owns its data
  - state resets by transaction rollback or a per-test schema, never by deleting rows
  - Bogus is seeded
  - a failing FsCheck seed becomes a pinned regression case
  - time comes from a fake `IClock`, never from sleeps or wall-clock waits
- **Snapshots:** Verify.Xunit covers generated schedules and PDFs (via text extraction). The `*.verified.*` files are the oracle, so a changed snapshot is accepted only after owner review.

## 9. AI layer (M5+)

Before changing `Credere.Modules.Ai`, `Credere.Mcp` or `Credere.Ai.Evals`, read [`docs/ai/overview.md`](docs/ai/overview.md) and [`docs/ai/guardrails.md`](docs/ai/guardrails.md). The model requirements are in [`docs/ai/agents.md` § Model requirements](docs/ai/agents.md).

- **Model config:** agents target Claude Sonnet 5.5 (`claude-sonnet-5-5`). Model ids and effort levels live in configuration (`AllowedModels`), never in code.
- **MCP tools:** every tool requires authorization (JWT plus scope). Parameters and returns are explicit, validated records. A description reads as a contract: what the tool does, when to use it and when not, each parameter, and what it doesn't return. Every call is logged with the tool name, PII-redacted arguments, a result hash, latency, cost, and the Langfuse trace id.
- **Prompts:** stored as versioned `.md` files in `src/Credere.Modules.Ai/Prompts/`, with changes recorded in `Prompts/CHANGELOG.md`.
- **Guardrails:** `PolicyFilter` is deterministic code, not another LLM. Collections outreach requires human approval.
- **Audit trail:** a short rationale field plus stored thinking summaries. Prompts never ask the model to write out its reasoning, because Sonnet 5.5 declines that as `reasoning_extraction`.
- **Evals:** ≥200 golden scenarios per agent. Every result row records the judge prompt version, model id and effort level. Once CI exists, a drop of more than 5% fails it for changes under `src/Credere.Modules.Ai/**`, `src/Credere.Ai.Evals/**` or `docs/ai/golden/**`.

## 10. Hard constraints

Each of these protects something that can't be recovered or faked later:

- **No invented financial data.** Rates, dates, formulas or identifiers that aren't sourced would be presented to banking readers as fact.
- **Failing tests are not skipped, deleted or weakened, and neither are architecture tests.** A failing test is evidence; investigate it and ask the owner before changing it.
- **Generated files are regenerated, not hand-edited.** This covers EF migration designers, the model snapshot, and OpenAPI output; a hand edit drifts from the source and is lost on the next generation.
- **Secrets and PII stay out of source and logs.** Use `dotnet user-secrets` locally and environment variables in CI. Redact PII before logging.
- **A security check is disabled only with a security ADR.**
- **ADRs are append-only.** A superseded ADR stays and links forward, so the decision history stays readable.
- **Documentation describes the code as it is.** Mark status as Implemented, Planned (M<n>) or Under consideration, so readers can trust what the docs claim.

## 11. Definition of done

When you change code that can be built, run or tested, run a real check that exercises the change before you report it as done: the relevant tests, the build, `pnpm --dir web build`, or the changed command itself. A syntax-only check, or a check command that failed to start, does not count. If Docker or another prerequisite is missing, report which check didn't run and why, and don't call the change done.

- `dotnet build Credere.slnx` passes with zero warnings, `dotnet test` passes from the root, and `dotnet format Credere.slnx --verify-no-changes` is clean. For frontend changes, `pnpm --dir web lint` and `pnpm --dir web build` pass.
- New domain logic arrives with its tests. Financial logic cites its reference source in code and tests.
- Docs are updated in the same change: ADR, module doc, state-machine diagram, and `docs/status.md` when a status changes.
- Commits follow Conventional Commits, one logical change each, with a body that explains why ([CONTRIBUTING](CONTRIBUTING.md)). A `// TODO` or `// FIXME` carries an issue link.
- The final report states what changed, which checks ran and their results, what wasn't verified, and any open questions.

## 12. When to ask the owner

Ask the owner when:
- a calculation has no authoritative reference
- a banking concept looks wrong but a test asserts it
- a security control blocks a feature
- a test would need deleting or significant change
- the task conflicts with this file
- two sources of guidance disagree
- a dependency outside the locked stack seems necessary
- it's unclear which module owns a behaviour

Describe what you were doing, what blocks you, the options, and your recommendation with its reasons. Then wait for the answer.

## 13. Changes that need the owner's approval first

Propose a plan and wait for the owner's approval before changing any of these:
- financial calculations in `Credere.Core`
- security or compliance logic
- a public API signature
- module boundaries
- a test or a dependency
- the agent instructions and configuration: `AGENTS.md`, `CLAUDE.md`, `.claude/`, `.codex/`, `.githooks/`

These decide whether the project is credible to its reviewers, and the owner wants to see them before they happen. Other changes go ahead without asking. Claude Code uses plan mode for this. Other tools post the plan in the session (or on the task's PR) and wait for the answer. An approved milestone plan in `docs/plans/` counts as approval for the new files each of its tasks lists; the PR is still reviewed.

For a milestone-sized request such as "build the Servicing module", start by proposing a breakdown for approval: value objects, state transitions, commands and events, invariants, and an order. Then implement one slice at a time, with tests in each slice.

## 14. What to run for each change

| Changed | Run |
|---|---|
| `Credere.Core` / `Credere.Shared` | `dotnet test tests/Credere.Core.Tests` (or `tests/Credere.Shared.Tests`) |
| A module | `dotnet test tests/Credere.Modules.<X>.Tests` and `dotnet test tests/Credere.ArchitectureTests` |
| Cross-module, or before the final report | `dotnet test` from the root and `dotnet format Credere.slnx --verify-no-changes` |

Integration tests need Docker, so check `docker ps` first. For banking semantics, cite the reference source ("matches ECB example 3, §2.1"); without one, say the result is not verified and ask the owner to review it.

## 15. Area rules

Each area has a short procedure file under `.claude/rules/`. Claude Code loads them automatically by path. Every other tool reads the matching file before its first edit in that area. The `paths:` list at the top of each file is authoritative; this table summarises it.

| Before editing | Read |
|---|---|
| `src/Credere.Core/**`, `tests/Credere.Core.Tests/**`, `docs/domain/**`, or any file whose name contains `Interest`, `DayCount`, `Aprc`, `Schedule`, `Allocation` or `Ifrs9` | `.claude/rules/financial-math.md` |
| `tests/**` | `.claude/rules/testing.md` |
| `src/Credere.Modules.*/**`, `tests/Credere.Modules.*/**`, `docs/modules/**`, `docs/domain/loan-lifecycle.md` | `.claude/rules/modules.md` |
| `**/Infrastructure/**`, `**/Migrations/**`, or any file whose name contains `DbContext`, `Repository` or `Outbox` | `.claude/rules/persistence.md` |
| `src/Credere.Api/**`, `src/Credere.Batch/**`, `src/Credere.Mcp/Program.cs`, `tests/Credere.Integration.Tests/**` | `.claude/rules/hosts.md` |
| `src/Credere.Modules.Ai/**`, `src/Credere.Mcp/**`, `src/Credere.Ai.Evals/**`, `tests/Credere.Modules.Ai.Tests/**`, `docs/ai/**` | `.claude/rules/ai-layer.md` |
| `web/**`, `docs/frontend.md` | `.claude/rules/frontend.md` |

## 16. Parallel work

Several agents, from different vendors, can work on Credere at once. When you work on a task from a milestone plan, [`docs/parallel-development.md`](docs/parallel-development.md) applies in full. The rules that matter most:

- One task, one worktree, one branch. Never work in another agent's worktree or branch.
- Before the first edit, write the task's owned paths to `.task-paths` in the worktree root, one glob per line. It is gitignored, and the pre-commit hook rejects any staged file outside it. Change nothing outside your owned paths; if the task needs that, stop and comment on the issue.
- Shared state lives in the task's GitHub issue and pull request. `docs/working-notes/` is local to one worktree, so a handoff written there is lost to the next session.
- A reference-vector set or a PR is checked by an agent from a different vendor than the one that wrote it, so errors in the two are less likely to coincide.
- The git hooks in `.githooks/` are the shared gate for every tool. Never bypass them with `--no-verify`; if a hook blocks you, report why.
