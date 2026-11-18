# Evaluations

Credere evaluates its agents with golden datasets scored by a versioned LLM judge. A regression ≥5% on any tracked metric fails CI on PRs that touch `src/Credere.Modules.Ai/**`, `src/Credere.Ai.Evals/**`, or `docs/ai/golden/**`.

## Pieces

| Piece | Location | Purpose |
|---|---|---|
| Golden datasets | Mongo `ai.eval_golden` + mirror `docs/ai/golden/*.jsonl` | Fixed scenarios with reference inputs and expected traits. |
| Judge prompt | `src/Credere.Modules.Ai/Prompts/judge.md` | LLM-as-judge rubric. Versioned. |
| Evals runner | `src/Credere.Ai.Evals/` | Console app executes datasets through agents, persists results, prints a report. |
| Results | Mongo `ai.eval_results` | Per-scenario result with judge score, pass/fail, rationale, judge version. |
| CI gate | GitHub Actions workflow (introduced M7) | Compares current run against the previous main's run; fails on regression >5%. |

## Golden dataset schema

Each golden file is JSON lines. One scenario per line:

```jsonl
{"id":"cd-001","agent":"CreditDecisioning","split":"approvable","inputs":{...},"expected":{"outcome":"Approve","must_contain":["rationale.income"],"must_not_contain":[]}}
{"id":"cd-002","agent":"CreditDecisioning","split":"declinable","inputs":{...},"expected":{"outcome":"Decline","must_contain":["dti"],"must_not_contain":["approve"]}}
```

Fields:

- `id` — stable id.
- `agent` — `CreditDecisioning | EarlyWarning | Collections`.
- `split` — group label (`approvable`, `declinable`, `edge`, `compliance`, ...).
- `inputs` — the exact agent input payload.
- `expected` — the acceptance criteria. May include:
  - `outcome` — expected categorical outcome (for CreditDecisioning).
  - `must_contain` — strings / tokens the output must contain.
  - `must_not_contain` — strings the output must not contain.
  - `required_disclosures` — regulatory phrases that must be present (Collections).
  - `max_latency_ms` — latency ceiling.
  - `max_cost_usd` — cost ceiling.

## Volume targets

| Agent | Min scenarios | Splits |
|---|---|---|
| CreditDecisioning | 200 | approvable, declinable, hr, edge, compliance |
| EarlyWarning | 200 | elevated, calm, boundary, edge |
| Collections | 200 | sms, email, letter, after-hours, compliance |

Smaller volumes risk stochastic noise overwhelming a 5% threshold.

## Judge

Judge model: configured in `AiEvalsOptions`, with its id recorded in every result row. The candidates are Claude Haiku 5.5 (`claude-haiku-5-5`, cheapest) and Claude Sonnet 5.5; the owner chooses. Judge prompt lives at `Prompts/judge.md`. It receives:

- The agent's output.
- The expected acceptance criteria.
- A judging rubric covering: correctness, rationale quality, compliance, format, PII absence.

Judge outputs a structured verdict:

```json
{
  "pass": true,
  "score": 0.92,
  "sub_scores": {
    "correctness": 1.0,
    "rationale_quality": 0.85,
    "compliance": 1.0,
    "format": 1.0,
    "no_pii": 1.0
  },
  "notes": "..."
}
```

Every result row stores the judge-prompt version it was evaluated under:

```
{
  "scenario_id": "cd-001",
  "run_id": "...",
  "model": "claude-sonnet-5-5",
  "effort": "medium",
  "agent_prompt_version": "cd-v12",
  "judge_version": "judge-v3",
  "judge_model": "claude-haiku-5-5",
  "verdict": { ... },
  "latency_ms": 1740,
  "cost_usd": 0.021,
  "timestamp": "..."
}
```

**Changing the judge prompt invalidates comparisons across versions.** The CI gate only compares runs with the same `judge_version`. A judge-version bump requires a full re-baseline (stored under a tag).

**Runs are stochastic.** Current Claude models don't accept non-default sampling parameters, so a scenario can't be pinned to one output. Each scenario runs N times (N set in `AiEvalsOptions`, starting at 3). The gate compares means between runs that share `judge_version`, `model` and `effort`.

## Metrics

Per agent per split:

- **Pass rate** = mean(verdict.pass).
- **Mean score**.
- **Per-sub-score means** (correctness, rationale_quality, compliance, format, no_pii).
- **Mean latency**, **p95 latency**, **mean cost**, **p95 cost**.

Compliance sub-score has its own threshold: any **compliance** sub-score < 1.0 is treated as a hard fail (≥1% hard-fail rate fails the build, independent of the 5% regression rule).

## CI gate (M7)

Triggered on PRs that touch `src/Credere.Modules.Ai/**` (prompts live in its `Prompts/` folder), `src/Credere.Ai.Evals/**`, or `docs/ai/golden/**`.

1. Run the evals console.
2. Compare the current `run_id`'s metrics to the latest `run_id` on `main` with the same `judge_version`.
3. For each `(agent, split, metric)` tuple: if the current value is ≥5% worse than the baseline **and** the sample size supports significance, record a regression.
4. Any regression fails the build.
5. Compliance hard-fail rate ≥1% fails the build independently.
6. Full results uploaded as an artifact; a short report posted as a PR comment.

## Running locally

```sh
# All datasets through all agents against the local DB + Anthropic sandbox key.
dotnet run --project src/Credere.Ai.Evals -- --agent all --split all

# A specific split.
dotnet run --project src/Credere.Ai.Evals -- --agent CreditDecisioning --split compliance
```

The runner respects `AiEvalsOptions.MaxConcurrency` and the per-agent cost cap.

## Cost control

- Evals run only on PRs touching AI code.
- Judge costs are capped at `AiEvalsOptions.JudgeDailyBudgetUsd`.
- Nightly full re-baselines are disabled until M7 opt-in; on-demand baselines are manual.

## When to update golden data

- Prompt change intended to fix a specific known failure mode → add scenarios that describe that failure mode to the dataset in the same PR.
- New rule in `PolicyFilter` → add compliance scenarios that exercise it.
- Model vendor swap → full re-baseline, tagged.

Golden data changes are reviewed with the same rigor as code — a PR that adds scenarios must include the rationale and (for compliance scenarios) a citation.
