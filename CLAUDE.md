# Credere — Claude Code Specific Guidance

> **Audience:** Claude Code when working on Credere.
> **Status:** Living. Additive to [`AGENTS.md`](AGENTS.md).

Read `AGENTS.md` first. Everything in it applies. This file covers only Claude-specific workflows and pitfalls. If anything here conflicts with `AGENTS.md`, `AGENTS.md` wins.

---

## 1. Operating principles

### Read first, edit second
Before any non-trivial edit:
1. View the file you are about to change.
2. View neighboring files in the same module folder to pick up local conventions.
3. View the relevant ADR in `docs/adr/` if one exists.
4. View the relevant domain doc in `docs/domain/` or module doc in `docs/modules/`.

Your default "start editing immediately" instinct is wrong for this project. Take the extra few tool calls.

### Use plan mode for non-trivial work
Any change that touches more than one file or requires architectural judgment: plan first. Short numbered steps. Honest about uncertainty. Get explicit confirmation for anything touching domain logic, financial math, security, or module boundaries.

### Use TaskCreate for multi-step work
Three or more distinct steps: use `TaskCreate`. Mark `in_progress` on start, `completed` on done — never batch. Never mark complete unless tests pass.

### Be precise about uncertainty
"I think this is correct" is useful. "This should work" without verification is not. For banking semantics: "this matches the reference in <cite>" or "I have not verified this — please review."

---

## 2. Tool usage patterns

### Reading
- Unfamiliar files: read the whole file first.
- Large generated files (EF migrations, OpenAPI output): read ranges.
- Searching: prefer `Grep` for code patterns, `Glob` for filenames. Avoid bash `find`/`grep`.

### Editing
- `Edit` for surgical changes.
- `Write` for new files or full rewrites only.
- Never use `Write` to re-create an existing file wholesale.
- When `old_str` might be ambiguous, include 2–3 lines of context.

### Bash
- Use for: tests, build, format, git. Not for file ops that have dedicated tools.
- Working directory: tests live in `tests/`. From the repo root `dotnet test` tests everything; from a specific project it tests only that project.

### Test execution
- After changes to `Credere.Core`: run `dotnet test tests/Credere.Core.Tests/`.
- After changes to a module: run that module's test project + `Credere.ArchitectureTests`.
- After cross-module changes: run `dotnet test` from the repo root.
- Integration tests require Docker. Check `docker ps` before assuming SQL Server / Mongo are available.
- AI eval tests are slow and cost money. Run them only when AI code changes.

### Web search
- Sparingly. Most .NET and banking answers live in the repo docs or library docs.
- When you search, cite the source in the code change.
- For regulations: EUR-Lex, ESMA, EBA, BIS. Avoid blog posts for legal rules.

---

## 3. Project-specific workflows

### Adding a feature slice to a module
1. Read the module doc under `docs/modules/`.
2. Create a feature folder under the module's `Application/Commands/` or `Application/Queries/`.
3. Define the command/query record, the handler, the result type, and its tests in that folder.
4. If the slice produces an integration event, add the event record under `IntegrationEvents/` and update the module doc's event catalog.
5. Update the module public interface (`I<Module>Module`) and its DI extension method.
6. Run the module's tests + `Credere.ArchitectureTests`.

### Adding an integration event
1. Add the record under the publishing module's `IntegrationEvents/`.
2. Add it to the event catalog in `docs/architecture.md` and the publisher's module doc.
3. In subscribing modules, add a handler under `Application/IntegrationEventHandlers/`.
4. Add a test proving the outbox dispatches it to subscribers.

### Adding a new MCP tool
1. Read `src/Credere.Modules.Ai/Mcp/` for conventions.
2. Define the tool with explicit parameter + return records.
3. Add authorization attribute. No anonymous tools.
4. Add structured logging + Langfuse span + Mongo `ai.llm_calls` row.
5. Update `docs/ai/mcp-tools.md`.
6. Manually verify the tool from Claude Desktop.

### Refactoring a value object
Value objects are foundational.
1. Read the existing tests to understand current contracts.
2. Make the change.
3. Run property-based tests — failures indicate an invariant break.
4. Run every module's tests that depend on this value object.
5. Update any ADR that referenced the previous design.

### Adding an interest calculation
1. Find the authoritative reference (ISDA, EU directive, IFRS document).
2. Cite it in code and tests.
3. Implement.
4. Write ≥20 reference test cases from the source document.
5. Property-based tests for invariants.
6. Mutation test with Stryker.NET; aim for ≥80%.
7. Human review before merge.

### Updating the loan state machine
1. Update the diagram in `docs/domain/loan-lifecycle.md` **first**.
2. Update the transition code.
3. Add tests for new transitions (valid + invalid).
4. Verify no orphan transitions.
5. Cross-reference ADRs.

---

## 4. Working with the roadmap

The roadmap is in [`docs/roadmap.md`](docs/roadmap.md). Before starting work:
- Identify which milestone the work belongs to.
- Check prerequisites in earlier milestones are complete.
- If you are jumping ahead, flag it to the user. It usually indicates the plan needs an update or the work should wait.

---

## 5. Communication style

### Brevity
Confirmations and simple answers: 1–2 sentences. Technical explanations: as long as needed, no longer. Code changes: minimal narration. The diff is the explanation.

### Tone
Direct. Not curt. Not effusive. Honest about uncertainty. No flattery ("great question") or padding ("certainly", "of course"). Respectful disagreement when you have grounds for it.

### Pushback
When the user asks for something that conflicts with `AGENTS.md` or project standards: say so, explain the conflict, propose an alternative. Do not silently comply with requests that damage code quality or domain correctness.

Example: asked to "just use `System.DateTime` here, it's simpler" — refuse, explain that NodaTime is required in domain code, offer to add the conversion at the right boundary.

### Avoid sycophancy
No "Great question!" openers. No "Let me know if you need anything else!" closers.

---

## 6. Slash commands (if configured)

Project slash commands live in `.claude/commands/` when present. Useful ones to consider:
- `/test-domain` — runs `Credere.Core.Tests` with verbose output.
- `/format-and-test` — `dotnet format` + `dotnet test`.
- `/arch-check` — runs `Credere.ArchitectureTests` only.
- `/adr-new` — scaffolds a new ADR from template.
- `/eval-ai` — runs the AI evaluation suite.

If these exist, prefer them over composing equivalent bash inline.

---

## 7. MCP integration

Credere ships an MCP server (`Credere.Mcp`). When it is configured in your environment you have direct tool access to loan data — use it for exploration.

When working on the MCP server itself:
- Test from Claude Desktop, not just unit tests.
- Watch structured logs in real time.
- Verify authorization by switching identities.

---

## 8. Common pitfalls specific to this project

### Pitfall 1: Reaching for `System.DateTime`
Tempting because .NET in the wild uses it. **Do not.** NodaTime throughout domain and application code. If you're converting at every boundary, the boundary is in the wrong place.

### Pitfall 2: Treating `decimal` as money
`decimal` is a primitive. `Money` is a domain concept. If a domain method signature has `decimal amount`, stop and use `Money`.

### Pitfall 3: Plausible-but-wrong financial math
LLMs produce reasonable-looking interest code with subtle errors: wrong rounding direction, off-by-one day count, wrong grace-period handling, wrong compounding. **Always verify against reference values.** Three to five reference cases minimum before claiming a calculation is correct.

### Pitfall 4: Mocking what you should integration-test
Default LLM instinct is to mock. For Credere:
- Domain logic: unit test, no mocks (pure logic).
- Repository / module integration: real SQL Server via Testcontainers.
- API endpoints: WebApplicationFactory + Testcontainers.
- AI agents: evaluation tests against the actual model (sandboxed test key).

Resist mocking EF Core, Mongo, or the LLM. Those mocks teach nothing and hide real bugs.

### Pitfall 5: Silent assumption about timezones
Banking is business days in specific zones. "End of day" depends on jurisdiction. Never assume UTC. Use NodaTime's `DateTimeZone` explicitly when business semantics matter.

### Pitfall 6: Documentation that overstates completeness
Document what exists, not what is planned. Do not write "Credere supports X" if X is in a future milestone. Use clear markers: "Implemented", "Planned (M5)", "Under consideration".

### Pitfall 7: Accepting too-large tasks
"Build the Servicing module" is not a task, it's a milestone. Break down:
- List value objects needed.
- List state machine transitions.
- List commands / events.
- List invariants.
- Propose an order.

Then implement piece by piece, with tests at each step.

### Pitfall 8: Crossing a module boundary to "save time"
Tempting to `using Credere.Modules.Servicing.Domain;` from Reporting. Architecture tests will catch it. **The right fix is an integration event, not a shortcut.** The pain of the correct design is the point.

### Pitfall 9: Ignoring failed tests
If a test fails after your change, investigate. Even if it "looks wrong," ask the user before changing it. Test code is often more carefully thought out than it looks.

---

## 9. When to ask vs decide

### Decide independently
- Internal variable / local helper names.
- Formatting (covered by `dotnet format`).
- Test scaffolding for clear specifications.
- Obvious missing null checks / input validation.
- Refactoring inside a single method for clarity.

### Ask first
- Anything in `Credere.Core` that involves financial calculation.
- Adding a dependency.
- Changing a public API signature.
- Modifying or deleting any test.
- Architectural changes spanning multiple modules.
- Changes to security or compliance logic.
- Anything conflicting with `AGENTS.md`.
- Changes to the `AGENTS.md` or `CLAUDE.md` themselves.

---

## 10. Working across sessions

Claude Code sessions are not persistent. Therefore:
- Start each session by re-reading `AGENTS.md` and this file.
- Multi-session work: leave pointers in commit messages, ADRs, and `/docs/` rather than chat history.
- If a task spans sessions, capture state in a working-notes file (gitignored under `/docs/working-notes/`).

---

## 11. Honesty checklist before declaring work done

- [ ] `dotnet test` passes (repo root).
- [ ] `dotnet format` is clean.
- [ ] `Credere.ArchitectureTests` green.
- [ ] No `null!`, no `dynamic`, no raw `decimal` in domain APIs.
- [ ] No `System.DateTime` / `DateTimeOffset` / `TimeSpan` in domain code.
- [ ] New domain logic has unit tests.
- [ ] New public APIs have XML doc comments.
- [ ] Relevant documentation updated (ADR, module doc, state machine, README).
- [ ] Commit messages Conventional Commits.
- [ ] No secrets, no PII in source or logs.
- [ ] Banking semantics verified against reference source (if applicable).
- [ ] No `// TODO` / `// FIXME` without an issue link.

If any item is unchecked: complete it or flag it explicitly in your handoff.

---

*Updates to this file require human approval via PR.*
