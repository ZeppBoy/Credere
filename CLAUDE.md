@AGENTS.md

<!--
Maintainers:
- AGENTS.md (imported above) is the tool-agnostic manual. It and this file load together, so never repeat
  a rule here; this file holds Claude Code specifics only.
- Area procedures live in .claude/rules/*.md (path-scoped). Hard guarantees (protected files, formatting,
  model and effort) live in .claude/settings.json.
- Tuned for Claude Sonnet 5.5 via the `sonnet` alias (2026-11), with project effort set to `high` because
  financial correctness outweighs speed here. When the alias moves to a new model, run
  `/doctor prompt-audit` and `/context all`.
- Changes need owner approval via PR.
-->

# Claude Code on Credere

## Approval

Use plan mode and wait for the owner's approval before changing any of these:
- financial calculations in `Credere.Core`
- security or compliance logic
- a public API signature
- module boundaries
- a test or a dependency
- `AGENTS.md`, this file, or `.claude/`

These decide whether the project is credible to its reviewers, and the owner wants to see them before they happen. Other changes go ahead without asking.

For a milestone-sized request such as "build the Servicing module", start by proposing a breakdown for approval: value objects, state transitions, commands and events, invariants, and an order. Then implement one slice at a time, with tests in each slice.

## Verify

| Changed | Run |
|---|---|
| `Credere.Core` / `Credere.Shared` | `dotnet test tests/Credere.Core.Tests` (or `tests/Credere.Shared.Tests`) |
| A module | `dotnet test tests/Credere.Modules.<X>.Tests` and `dotnet test tests/Credere.ArchitectureTests` |
| Cross-module, or before the final report | `dotnet test` from the root and `dotnet format Credere.slnx --verify-no-changes` |

Integration tests need Docker, so check `docker ps` first. For banking semantics, cite the reference source ("matches ECB example 3, §2.1"); without one, say the result is not verified and ask the owner to review it.

## Working with the owner

- When a request conflicts with `AGENTS.md`, name the conflict and offer the compliant alternative. The owner prefers that to silent compliance. Example: asked to "just use `DateTime`", explain the NodaTime rule and offer to convert at the correct boundary.
- Branch names follow `CONTRIBUTING.md`: `feat/`, `fix/` or `docs/`.
- For work that spans sessions, keep state in `docs/working-notes/<topic>.md` (gitignored), ADRs and commit messages. Personal, uncommitted preferences go in `CLAUDE.local.md` (gitignored).

## MCP — Planned (M5)

`Credere.Mcp` is a skeleton and exposes no tools yet. Once M5 lands, register it locally with `claude mcp add credere -- dotnet run --project src/Credere.Mcp`. Exercise each tool from a real MCP client, and check authorization by switching identities. The owner does the final check in Claude Desktop.
