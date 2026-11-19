@AGENTS.md

<!--
Maintainers:
- AGENTS.md (imported above) is the tool-agnostic manual. It and this file load together, so never repeat
  a rule here; this file holds Claude Code specifics only.
- Area procedures live in .claude/rules/*.md (path-scoped); AGENTS.md §15 points other tools to them.
  Hard guarantees for Claude (protected files, formatting, model and effort) live in .claude/settings.json;
  the tool-agnostic ones live in .githooks/ and, for Codex, .codex/.
- Tuned for Claude Sonnet 5.5 via the `sonnet` alias (2026-11), with project effort set to `high` because
  financial correctness outweighs speed here. When the alias moves to a new model, run
  `/doctor prompt-audit` and `/context all`.
- Changes need owner approval via PR.
-->

# Claude Code on Credere

## Approval and verification

AGENTS.md §13 lists the changes that need the owner's approval first; in Claude Code, use plan mode for them. AGENTS.md §14 says which checks to run for each change.

## Working with the owner

- When a request conflicts with `AGENTS.md`, name the conflict and offer the compliant alternative. The owner prefers that to silent compliance. Example: asked to "just use `DateTime`", explain the NodaTime rule and offer to convert at the correct boundary.
- Branch names follow `CONTRIBUTING.md`: `feat/`, `fix/` or `docs/`.
- For work that spans sessions, keep state in `docs/working-notes/<topic>.md` (gitignored), ADRs and commit messages. In a parallel task the handoff goes in the PR instead (AGENTS.md §16). Personal, uncommitted preferences go in `CLAUDE.local.md` (gitignored).
- For a parallel task outside Rider Air, start the session with `claude --worktree <task-id>` and rename the branch to the task's branch name before the first push.

## MCP — Planned (M5)

`Credere.Mcp` is a skeleton and exposes no tools yet. Once M5 lands, register it locally with `claude mcp add credere -- dotnet run --project src/Credere.Mcp`. Exercise each tool from a real MCP client, and check authorization by switching identities. The owner does the final check in Claude Desktop.
