# Parallel development with several agents

**Status:** Proposed (2026-11-19), awaiting owner approval. Until it is approved, agents work one at a time, as before.

This document describes how two or more AI coding agents (Claude Code, Kilo Code, or any tool that follows [`AGENTS.md`](../AGENTS.md)) and human contributors work on Credere at the same time without blocking each other or breaking `main`. The per-milestone task breakdowns live in [`plans/`](plans/README.md). The rules in AGENTS.md and CLAUDE.md still apply in full; this document adds the rules that only matter when several agents run at once.

## Principles

1. **One task, one agent, one worktree, one branch.** No two agents share a working directory or a branch.
2. **Contracts before fan-out.** Each milestone starts with a short serial set of tasks that land the types and interfaces the rest of the work compiles against. Those tasks land real, tested code, not stubs. Parallel tasks start only once those contracts are on `main`.
3. **A file has one writer at a time.** Each task owns a list of paths. Shared hotspots (the [serialized files](#serialized-files)) change only in tasks created for that purpose.
4. **Shared state lives on GitHub, not on a disk.** Claims, progress and handoffs go in the task's issue and pull request. `docs/working-notes/` is gitignored and exists only in one worktree, so other agents never see it.
5. **The owner remains the gate.** Agents produce reviewable work faster than one person can review it. The plan therefore limits concurrency and batches approvals; it does not relax them.

## Roles

| Role | Who | Does | Does not |
|---|---|---|---|
| **Owner** | Human | Decides open questions. Approves plans, reference vectors and PRs. Merges. | — |
| **Coordinator** | The owner, or one agent session in the main checkout | Creates and labels task issues. Moves issues to `ready` when their dependencies merge. Tells open PRs to rebase. Updates `docs/status.md`. Runs each milestone's close-out task. | Implement a task while also coordinating. |
| **Implementer** | 2–3 agent sessions, each in its own worktree | Claims one `ready` task, implements it inside the task's owned paths, opens a PR. | Edit paths it doesn't own, merge, or push to `main`. |
| **Reviewer** | Optional agent session | Reviews PRs that are ready (for example `/code-review`). Recomputes reference vectors independently before the owner sees them. | Fix the code it reviews in the same PR. |

**Concurrency:** use at most three implementers. More than that mostly produces PRs waiting for review and rebases after every merge. Two implementers plus a reviewer is a good default.

## Setting up a worktree

Keep worktrees outside the repository, next to it:

```sh
git fetch origin
git worktree add ../Credere-wt/m1-05 -b feat/m1-05-act-act-isda origin/main
cd ../Credere-wt/m1-05
dotnet build Credere.slnx
```

- **Claude Code:** `claude --worktree m1-05` creates a worktree for the session and starts in it. Rename the branch to the task's branch name before the first push: `git branch -m feat/m1-05-act-act-isda`.
- **Other tools** (for example Kilo Code's worktree pool) work the same way, as long as the branch follows the naming below.
- If a tool creates worktrees *inside* the repository, add that directory to `.gitignore` first. Otherwise one agent's worktree shows up as untracked files in every other checkout.
- **Branch names** follow CONTRIBUTING.md, with the task id: `feat/m1-05-act-act-isda`, `docs/m1-01-result-placement`.
- **Clean up** after the PR merges: `git worktree remove ../Credere-wt/m1-05`.

Each worktree has its own `bin/` and `obj/`, so builds and tests in different worktrees don't interfere. The NuGet package cache is shared, which is safe.

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
2. **Claim it.** Check that the issue is still `ready` (`gh issue view <n>`). Then run `gh issue edit <n> --remove-label ready --add-label claimed` and `gh issue comment <n> --body "Claimed by <tool>, worktree <name>"`. The `claimed` label is the lock: never start an issue that has it.
3. **Write reference vectors first** (financial tasks only). Commit the reference test data and its citations before any implementation. Push, and open a draft PR (`gh pr create --draft`, with `Closes #<n>` in the body). Comment `Vectors ready for review`. The reviewer recomputes the vectors independently, and the owner approves them on the PR. Only then is the implementation written. This is the order `.claude/rules/financial-math.md` requires, so reference values can't be fitted to the code.
4. **Implement** inside the task's owned paths, with Conventional Commits. For non-financial tasks, open the draft PR at the first push.
5. **Rebase and check.** Rebase on `origin/main`, then run the [checks](#checks).
6. **Ask for review.** Complete the [PR description](#pr-description-template), run `gh pr ready <n>`, and move the issue from `claimed` to `in-review`.
7. **After the merge,** the coordinator moves newly unblocked issues to `ready` and comments on open PRs that depend on the merged task, so their authors rebase.

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
| `AGENTS.md`, `CLAUDE.md`, `.claude/**`, `CONTRIBUTING.md` | Every agent reads them. | Owner-approved docs tasks (CLAUDE.md "Approval"). |
| `tests/Credere.ArchitectureTests/**` | The boundary rules (AGENTS.md §3). | Owner-approved tasks only. |
| `docs/status.md` | One table of adjacent rows, which conflicts on almost every parallel edit. | The coordinator, after merges. Implementers put status changes in the PR description instead. |
| `docs/open-questions.md`, `docs/adr/**` | Numbered decision records. | Owner-approved docs tasks. Reserve the next number on the issue before writing. |
| `docs/architecture.md`, and the contract parts of `docs/domain/**` and `docs/modules/**` | They define the public contracts. | Contract tasks. An implementer that finds one wrong raises it ([stop conditions](#stop-conditions)). |

### Public contracts

Public signatures are defined in `docs/domain/` and `docs/modules/` and land in contract tasks. A fan-out task implements them as written. Changing a public signature is a separate change, approved by the owner (CLAUDE.md "Approval") and merged before the dependent tasks continue.

## Checks

Before asking for review, each worktree runs the checks in AGENTS.md §11:

```sh
dotnet build Credere.slnx
dotnet test
dotnet format Credere.slnx --verify-no-changes
```

During development, the per-project commands in the CLAUDE.md "Verify" table are enough. The full suite runs on the rebased branch before review.

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

These steps make the process operational. Each needs the owner's approval, because it touches serialized files or GitHub:

1. Add a short "Parallel work" section to AGENTS.md pointing here, so every tool reads the rules.
2. Add to CLAUDE.md: start each task with `claude --worktree <task-id>`, and keep handoff state in the PR, because `docs/working-notes/` is not shared between worktrees.
3. Add a PR template under `.github/` built from the [PR description template](#pr-description-template), and mention task-id branch names in CONTRIBUTING.md.
4. Create the GitHub labels: `m1` … `m7`, `blocked`, `ready`, `claimed`, `in-review`.
5. Create one issue per task from the milestone plan, using the brief template.
