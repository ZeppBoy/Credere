# Parallel development with several agents

**Status:** Approved (2026-11-19).

This document describes how two or more AI coding agents (Claude Code and Codex, usually run side by side from Rider's Air agent workspace, or any tool that follows [`AGENTS.md`](../AGENTS.md)) and human contributors work on Credere at the same time without blocking each other or breaking `main`. The per-milestone task breakdowns live in [`plans/`](plans/README.md). The rules in AGENTS.md still apply in full, CLAUDE.md adds Claude Code specifics, and this document adds the rules that only matter when several agents run at once. AGENTS.md §16 summarises them for every tool.

## Principles

1. **One task, one agent, one worktree, one branch.** No two agents share a working directory or a branch.
2. **Contracts before fan-out.** Each milestone starts with a short serial set of tasks that land the types and interfaces the rest of the work compiles against. Those tasks land real, tested code, not stubs. Parallel tasks start only once those contracts are on `main`.
3. **A file has one writer at a time.** Each task owns a list of paths. Shared hotspots (the [serialized files](#serialized-files)) change only in tasks created for that purpose.
4. **Shared state lives on GitHub, not on a disk.** Claims, progress and handoffs go in the task's issue and pull request. `docs/working-notes/` is gitignored and exists only in one worktree, so other agents never see it.
5. **The owner remains the gate.** Agents produce reviewable work faster than one person can review it. The plan therefore limits concurrency and batches approvals; it does not relax them.
6. **A different vendor checks the work.** Reference vectors and PRs written by a Claude agent are recomputed or reviewed by a Codex agent, and the other way round. Two models from different vendors are less likely to make the same mistake than one model checking itself.
7. **The fences are enforced, not only described.** The pre-commit hook rejects files outside the task's owned paths, and `.claude/settings.json`, `.codex/` and the hooks protect the serialized files. A rule that only lives in prose holds for one tool at best.

## Roles

| Role | Who | Does | Does not |
|---|---|---|---|
| **Owner** | Human | Decides open questions. Approves plans, reference vectors and PRs. Merges. | — |
| **Coordinator** | The owner, or one agent session in the main checkout | Creates and labels task issues. Moves gated issues to `ready` once the owner decides; the `Unblock tasks` workflow moves the rest. Tells open PRs to rebase. Updates `docs/status.md`. Runs each milestone's close-out task. | Implement a task while also coordinating. |
| **Implementer** | 2–3 agent sessions, each in its own worktree | Claims one `ready` task, implements it inside the task's owned paths, opens a PR. | Edit paths it doesn't own, merge, or push to `main`. |
| **Reviewer** | An agent session from the other vendor than the PR's author, often the other implementer between tasks | Reviews PRs that are ready (`/code-review` in Claude Code, `/review` in Codex). Recomputes reference vectors independently before the owner sees them. | Fix the code it reviews in the same PR, or review work from its own vendor. |

**Concurrency:** use at most three implementers. More than that mostly produces PRs waiting for review and rebases after every merge. The default is two implementers from different vendors, one Claude Code and one Codex, each reviewing the other's work between its own tasks. Give the critical-path lane to one of them and the most formula-driven lane (day counts, then rates) to the other.

## Setting up a worktree

Enable the shared git hooks once per clone (`git config core.hooksPath .githooks`); every worktree of the clone then uses them. Then, for each task:

1. **Create the worktree and branch.**
   - **Rider Air:** start the session from the Agent Sessions tool window with the worktree execution mode, never local, so two agents never share a working directory. Check where Air puts its worktrees the first time; if it is inside the repository, add that directory to `.gitignore` first, or one agent's worktree shows up as untracked files in every other checkout.
   - **Claude Code CLI:** `claude --worktree m1-05`.
   - **By hand** (any tool), next to the repository:
     ```sh
     git fetch origin
     git worktree add ../Credere-wt/m1-05 -b feat/m1-05-act-act-isda origin/main
     ```
   - A tool's own worktree pool (Kilo Code's, for example) works the same way.
2. **Name the branch** after the task, following CONTRIBUTING.md: `feat/m1-05-act-act-isda`, `docs/m1-01-result-placement`. Rename a generated name before the first push: `git branch -m feat/m1-05-act-act-isda`.
3. **Write `.task-paths`** in the worktree root: the task's owned paths from the brief, one glob per line. `*` also matches `/`, and `{a,b}` lists alternatives, so the paths can be copied from the plan as written:
   ```text
   # M1-05
   src/Credere.Core/DayCounts/ActActIsda.cs
   tests/Credere.Core.Tests/DayCounts/ActActIsda*
   docs/domain/day-counts.md
   ```
   The file is gitignored. In a linked worktree the pre-commit hook refuses to commit without it, and refuses any staged path, added, changed or deleted, that matches none of its globs. A doc section can't be fenced by path, so for a shared doc such as `day-counts.md` the task still edits only its own section.
4. **Build:** `dotnet build Credere.slnx`.
5. **Clean up** after the PR merges: `git worktree remove <path>`, or let the tool remove its own worktree.

Each worktree has its own `bin/` and `obj/`, so builds and tests in different worktrees don't interfere. The NuGet package cache is shared, which is safe.

## Tool setup

### Rider Air

The Air plugin is bundled with Rider 2026.3. Rider detects Claude Code and Codex when they are installed and signed in, and runs each with its own configuration and subscription. Start one session per task. Review an agent's changes in Rider's diff and Local History before the agent opens its PR, and use Code Review notes to send back fixes.

### Claude Code

Reads `CLAUDE.md`, which imports `AGENTS.md`, loads `.claude/rules/` by path, and is guarded by `.claude/settings.json`. Nothing else to set up.

### Codex

Codex reads `AGENTS.md` only. AGENTS.md §13–§16 therefore hold everything it must follow, and §15 points it to the `.claude/rules/` files. `.codex/config.toml` (workspace-write sandbox with network, on-request approvals, high reasoning effort) and `.codex/rules/credere.rules` (the command allow, prompt and forbid lists) load only when the project is trusted:

- Each worktree is a separate path. In a new worktree session, run `/status` and check that the sandbox is `workspace-write`. If it is not, the worktree isn't trusted: trust it when Codex asks, or add it under `[projects."<path>"]` with `trust_level = "trusted"` in `~/.codex/config.toml`.
- A linked worktree keeps its git metadata in the main clone's `.git/`, outside the worktree. If `git commit` fails with a permission error in the sandbox, add that directory to the writable roots in `~/.codex/config.toml` (user level, because the path is machine-specific):
  ```toml
  [sandbox_workspace_write]
  writable_roots = ["/Users/<you>/repo/Credere/.git"]
  ```

## Task lifecycle

Each task in a milestone plan becomes one GitHub issue, titled with its id (`M1-05: Actual/Actual (ISDA)`) and labelled with the milestone (`m1`) and one status label.

| Status label | Meaning | Set by |
|---|---|---|
| `blocked` | A dependency isn't merged, or an owner decision is open. | Coordinator |
| `ready` | Every dependency is merged and every gating decision is made. | Coordinator |
| `claimed` | An implementer is working on it. | Implementer |
| `in-review` | Its PR is ready for review. | Implementer |
| *(closed)* | Its PR merged. | GitHub, through `Closes #<n>` |

An implementer works through these steps:

1. **Find work.** List the ready tasks with `gh issue list --label m1 --label ready`. Take the one nearest the front of the plan's critical path.
2. **Claim it.** Check that the issue is still `ready` (`gh issue view <n>`). Then run `gh issue edit <n> --remove-label ready --add-label claimed` and `gh issue comment <n> --body "Claimed by <tool>, worktree <name>"`. The `claimed` label is the lock: never start an issue that has it. Then set up the worktree, including `.task-paths` ([Setting up a worktree](#setting-up-a-worktree)).
3. **Write reference vectors first** (financial tasks only). Commit the reference test data and its citations before any implementation. Push, and open a draft PR (`gh pr create --draft`, with `Closes #<n>` in the body). Comment `Vectors ready for review`. A reviewer from the other vendor recomputes the vectors independently, and the owner approves them on the PR. Only then is the implementation written. This is the order `.claude/rules/financial-math.md` requires, so reference values can't be fitted to the code.
4. **Implement** inside the task's owned paths, with Conventional Commits. For non-financial tasks, open the draft PR at the first push.
5. **Rebase and check.** Rebase on `origin/main`, then run the [checks](#checks).
6. **Ask for review.** Complete the [PR description](#pr-description-template), run `gh pr ready <n>`, and move the issue from `claimed` to `in-review`.
7. **After the merge,** the `Unblock tasks` workflow (`.github/workflows/unblock-tasks.yml`) moves every `blocked` issue whose dependencies are all closed as completed, and whose gate is `—`, to `ready`. It reads the `Depends on:` and `Gate:` fields from the first line of the issue body, so keep that line in the brief format. A gated issue moves when the owner edits its gate to `—` or moves it by hand; an issue without both fields, or one that depends on a task closed as not planned, is left for the coordinator. The coordinator also comments on open PRs that depend on the merged task, so their authors rebase.

### Working continuously

An implementer doesn't have to stop at each gate. Given a lane (a list of tasks in the milestone plan), it loops:

1. On its own open PRs: implement when the owner has approved the vectors; address review comments.
2. On the other vendor's PRs that wait for review: recompute the vectors independently or review the code, and comment. It never changes their files.
3. Otherwise, with fewer than two open PRs of its own, it claims the next `ready` task in its lane (or any `ready` task when the lane has none), starts a fresh branch from `origin/main` in its worktree, rewrites `.task-paths` for the new task, writes the vectors first and opens a draft PR.
4. When nothing is actionable, it posts a short status comment on its open PRs and stops.

The limit of two open PRs keeps unreviewed work and rebases small. Every gate stays in place: vectors before code, cross-vendor checks, the hooks, and the owner's merge. A long session loses detail as its context fills, so the implementer writes a handoff comment on its current PR after every three tasks, and the next task starts in a fresh session.

The session prompts live in [`prompts/`](prompts/): [`implementer.md`](prompts/implementer.md) for both implementers, and [`coordinator.md`](prompts/coordinator.md) for the coordinator.

**Handoff.** An implementer that stops before finishing (the session ends, the context fills up, or it is blocked) writes a handoff comment on the PR. The comment covers what is done, what comes next, and any open problems. The next session continues from the PR, not from local notes.

## Ownership rules

### Owned paths

Each task lists the paths it may create or change. For example, M1-05 owns `src/Credere.Core/DayCounts/ActActIsda.cs`, `tests/Credere.Core.Tests/DayCounts/ActActIsdaTests.cs`, and the "Actual/Actual (ISDA)" section of `docs/domain/day-counts.md`. An implementer changes nothing else. AGENTS.md §11 requires docs to change together with the code, so owned paths can include named sections of a domain or module doc. The task edits only those sections.

### Serialized files

The files below change only in tasks created for that purpose, never in a fan-out task. Concurrent edits to them either conflict or merge silently into something wrong.

| Files | Why they are serialized | Changed by |
|---|---|---|
| `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.config/dotnet-tools.json` | Every project builds against them. | Tooling tasks. A new dependency also needs an ADR and owner approval (AGENTS.md §4). |
| `Credere.slnx`, every `*.csproj` | The project graph and its references. | Tooling and contract tasks. |
| `AGENTS.md`, `CLAUDE.md`, `.claude/**`, `.codex/**`, `.githooks/**`, `.github/**`, `CONTRIBUTING.md` | Every agent reads them, or they enforce the rules for every agent. | Owner-approved docs tasks (AGENTS.md §13). |
| `tests/Credere.ArchitectureTests/**` | The boundary rules (AGENTS.md §3). | Owner-approved tasks only. |
| `docs/status.md` | One table of adjacent rows, which conflicts on almost every parallel edit. | The coordinator, after merges. Implementers put status changes in the PR description instead. |
| `docs/open-questions.md`, `docs/adr/**` | Numbered decision records. | Owner-approved docs tasks. Reserve the next number on the issue before writing. |
| `docs/architecture.md`, and the contract parts of `docs/domain/**` and `docs/modules/**` | They define the public contracts. | Contract tasks. An implementer that finds one wrong raises it ([stop conditions](#stop-conditions)). |

### Public contracts

Public signatures are defined in `docs/domain/` and `docs/modules/` and land in contract tasks. A fan-out task implements them as written. Changing a public signature is a separate change, approved by the owner (AGENTS.md §13) and merged before the dependent tasks continue.

## Checks

Before asking for review, each worktree runs the checks in AGENTS.md §11:

```sh
dotnet build Credere.slnx
dotnet test
dotnet format Credere.slnx --verify-no-changes
```

During development, the per-project commands in the AGENTS.md §14 table are enough. The full suite runs on the rebased branch before review. The hooks run their own checks on every commit and push (CONTRIBUTING.md), but they don't replace these.

**Shared machine resources:**

- `docker compose` binds fixed ports (1433, 27017, 4317, 4318, 3100), so only one stack runs per machine. Agents don't start or stop it; the owner or coordinator does.
- Testcontainers picks random ports, so integration tests can run in several worktrees at once. The SQL Server 2022 image runs emulated on Apple Silicon, though, so run at most one integration-test suite at a time (from M2).
- M1 needs no Docker, because `Credere.Core` and `Credere.Shared` are pure.

## Merging

- The owner merges, in the dependency order given in the milestone plan. PRs that don't depend on each other merge in any order.
- Before a merge, the branch is rebased on current `main` and its checks pass on the rebased code.
- After a merge, dependent branches rebase (`git fetch origin && git rebase origin/main`) and rerun their checks. A conflict in a path the task doesn't own means an ownership rule was broken somewhere: stop and raise it.
- `main` stays green. After each merge the coordinator runs the full suite on `main` and reports any failure on the merged PR.

## Stop conditions

On top of AGENTS.md §12, an implementer stops and comments on its issue when:

- the task needs a change outside its owned paths, or in a serialized file;
- a public contract it implements looks wrong or incomplete;
- it needs a type from a task that hasn't merged yet;
- a reference vector can't be sourced the way the plan says;
- a rebase conflicts in a path the task doesn't own.

It then either continues only within its own paths or stops. It never works around the problem by editing someone else's files.

## Task brief template

The coordinator gives each implementer a brief like this, filled in from the plan. The brief is also the issue body.

```markdown
## M1-05: Actual/Actual (ISDA)

Milestone plan: docs/plans/m1.md · Depends on: M1-04 (merged) · Gates: none

### Goal
`ActActIsda : DayCountConvention` per docs/domain/day-counts.md §1.

### Owned paths
- src/Credere.Core/DayCounts/ActActIsda.cs
- tests/Credere.Core.Tests/DayCounts/ActActIsdaTests.cs
- docs/domain/day-counts.md, section "1. Actual/Actual (ISDA)"

### Consumes (do not change)
`DayCountConvention` (M1-04).

### References
ISDA 2006 Definitions §4.16(b); ISDA memo ISDA-BS:9951.1 (1998), four worked examples.

### Acceptance
- ≥20 vectors: the 4 published examples plus formula-derived cases, each cited. Owner approves them before implementation.
- FsCheck properties from day-counts.md (zero, non-decreasing, additivity).
- AGENTS.md §11 checks pass on a branch rebased on main.

### Out of bounds
Serialized files (docs/parallel-development.md). `docs/status.md`: report status in the PR instead.
```

## PR description template

```markdown
Closes #<issue>

## Task
<id and one-line goal>

## Changes
<what changed, and why where it isn't obvious>

## Reference sources
<document, section, example number, for each reference case or vector set>

## Checks
<commands run on the rebased branch, with results>

## Status change
<rows of docs/status.md the coordinator should update>

## Decisions needed / handoff
<open questions for the owner, or where a paused task stands>
```

## Adopting this process

The branch that added this section made the repository side operational: AGENTS.md §13–§16, the Codex configuration, the shared git hooks, the PR template, and the task-id branch names in CONTRIBUTING.md. What remains needs the owner, because it touches GitHub or a local clone:

1. Approve this document and the milestone plan, and decide the plan's gating questions.
2. Enable the hooks in each clone: `git config core.hooksPath .githooks`.
3. Create the GitHub labels: `m1` … `m7`, `blocked`, `ready`, `claimed`, `in-review`.
4. Create one issue per task from the milestone plan, using the brief template.
5. In Rider, check that Air detects both Claude Code and Codex, and run one small task with each to confirm the Codex trust and writable-roots settings above.
