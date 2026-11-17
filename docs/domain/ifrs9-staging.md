# IFRS 9 staging

Loans are classified into one of three stages for impairment / expected-credit-loss purposes. Credere implements stage assignment per **IFRS 9 Financial Instruments**, simplified for a single-product consumer-loan portfolio.

## Authoritative sources

- **IFRS 9 Financial Instruments**, IFRS Foundation, 2014 (as amended).
- **European Banking Authority** guidelines on credit-risk management practices and accounting for expected credit losses (EBA/GL/2017/06).

Cite these in XML comments on `IIfrs9StagingService.cs` and in the test class.

## Stages

| Stage | Condition | ECL horizon |
|---|---|---|
| **Stage 1** | Performing; no significant increase in credit risk since initial recognition. | 12-month expected credit loss. |
| **Stage 2** | Significant increase in credit risk (SICR) since initial recognition, but not credit-impaired. | Lifetime expected credit loss. |
| **Stage 3** | Credit-impaired (default event has occurred). | Lifetime expected credit loss on gross carrying amount. |

## Triggers used by Credere (simplified)

A loan is in **Stage 3** when:

- `State ∈ {Defaulted, WrittenOff}`, OR
- `State == Restructured` due to financial difficulty (forbearance flag true on the restructuring plan).

A loan is in **Stage 2** when:

- `State == PastDue` with `DaysPastDue ≥ 30`, OR
- `State == InGrace` with the grace period exceeding 30 days, OR
- The borrower's internal risk score (set at origination by the credit-decisioning agent, re-scored by the early-warning agent) is at or below the configured `Stage2RiskScoreThreshold` (default: `BCRS_B`, i.e., the second-worst band), OR
- The loan is on the Early-Warning watchlist produced by the AI module.

Otherwise the loan is in **Stage 1**.

## Public contract

```csharp
public interface IIfrs9StagingService
{
    Ifrs9Stage Classify(StagingInputs inputs);
}

public sealed record StagingInputs(
    LoanState State,
    int? DaysPastDue,
    bool ForbearanceApplied,
    RiskScore CurrentRiskScore,
    RiskScore OriginationRiskScore,
    bool OnEarlyWarningWatchlist);

public enum Ifrs9Stage { Stage1 = 1, Stage2 = 2, Stage3 = 3 }
```

## Stage assignment is authoritative, not aggregate-owned

The Servicing module exposes `IIfrs9StagingService` as a read-only classifier. The Loan aggregate does not own a `Stage` field. Reasons:

1. Stage depends on cross-module inputs (risk scores from Ai).
2. Stage is derived, not transacted — it can change daily without a business event.
3. IFRS 9 staging is a reporting concern; Reporting module projects it.

The daily batch (`Credere.Batch`) computes staging for every active loan and writes the result to `reporting.LoanStaging` (SQL) along with a lineage record pointing at the inputs used.

## Reversibility

- Stage 1 ↔ Stage 2 is reversible: if `DaysPastDue` drops below 30 and risk score improves above the threshold and the loan is not on the watchlist, the loan returns to Stage 1.
- Stage 3 is sticky in most jurisdictions: a loan can exit Stage 3 only via a specific "exit from default" rule (typically 90 consecutive days performing post-restructuring). Credere models this via `Ifrs9ExitFromStage3Rule` (M4+). Until then Stage 3 loans stay in Stage 3 even if `DaysPastDue` is 0.

## What Credere does NOT do

- **No ECL calculation.** Expected credit loss requires PD (probability of default), LGD (loss given default), EAD (exposure at default) models with macroeconomic overlays. Out of scope. Documented in [`../README.md`](../README.md) as a non-goal.
- **No forward-looking adjustments.** Stage 1 loans in a recession should theoretically have a higher ECL; our model does not reflect this.
- **No portfolio-level overlays.** All rules are per-loan.

A separate document (or future ADR) would be needed if ECL becomes in scope.

## Testing

- **Deterministic unit tests** for every trigger: exact input, expected stage.
- **Boundary tests**: `DaysPastDue = 29 → Stage 1`, `DaysPastDue = 30 → Stage 2`, `DaysPastDue = 89 → Stage 2`, `DaysPastDue = 90 → Stage 3`.
- **Property-based**: for any valid input set, the output is exactly one of `Stage1`, `Stage2`, `Stage3`.
- **Scenario tests** in `Credere.Integration.Tests`: simulate a loan over 180 days, verify staging transitions daily batch-by-batch.
