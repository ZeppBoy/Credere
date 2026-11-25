# Interest accrual reference vectors

The source is Credere's owner-approved [D5 accrual rules](../../../../docs/modules/servicing.md#interest-accrual-and-posting) and [ADR-0011](../../../../docs/adr/0011-m2-module-contracts.md#decision). Amounts are EUR. Interest is cumulative across constant-principal segments. Round each segment to cents, midpoint to even, then subtract cumulative interest at the previously posted date to get the ledger amount.

## Monthly interest-only loan

Use the first two periods of the existing M1 [Level30E360_24m vector](../../../Credere.Core.Tests/Schedules/BulletScheduleGeneratorInterestOnlyVectorTests.cs). The loan disburses on 2026-01-15 with principal 10,000.00 at 5% fixed, 30E/360. The first two due dates are 2026-02-15 and 2026-03-15. Each 30-day period earns 10,000 × 0.05 × 30/360 = 41.666… which rounds to 41.67.

Unless a row says otherwise, there are no payments and the previous accrual date is the disbursement date. The current and past-due balances are the amounts outstanding at the run date. Fees are zero under D5.

| ID | Scenario | Previous accrual date | Run date | Cumulative interest at previous date | Cumulative interest at run date | Ledger post | Current interest | Past-due interest |
|---|---|---|---|---:|---:|---:|---:|---:|
| IA-01 | Same-day accrual | 2026-01-15 | 2026-01-15 | 0.00 | 0.00 | None | 0.00 | 0.00 |
| IA-02 | Day before the first due date | 2026-01-15 | 2026-02-14 | 0.00 | 40.28 | 40.28 | 40.28 | 0.00 |
| IA-03 | First due date, one run | 2026-01-15 | 2026-02-15 | 0.00 | 41.67 | 41.67 | 41.67 | 0.00 |
| IA-04 | First due date after IA-02 | 2026-02-14 | 2026-02-15 | 40.28 | 41.67 | 1.39 | 41.67 | 0.00 |
| IA-05 | Day after an unpaid due date, after IA-04 | 2026-02-15 | 2026-02-16 | 41.67 | 43.06 | 1.39 | 1.39 | 41.67 |
| IA-06 | Second due date, both periods unpaid | 2026-01-15 | 2026-03-15 | 0.00 | 83.34 | 83.34 | 41.67 | 41.67 |
| IA-07 | Interest-only payment on 2026-01-17 | 2026-01-17 | 2026-01-20 | 2.78 | 6.94 | 4.16 | 4.16 | 0.00 |
| IA-08 | Payment halves principal on 2026-01-17 | 2026-01-17 | 2026-01-20 | 2.78 | 4.86 | 2.08 | 2.08 | 0.00 |
| IA-09 | Replay a prior date after IA-05 | 2026-02-16 | 2026-02-15 | 43.06 | 41.67 (reference only) | None | 1.39 | 41.67 |
| IA-10 | Partial interest paid on the first due date | 2026-02-15 | 2026-02-16 | 41.67 | 43.06 | 1.39 | 1.39 | 40.28 |
| IA-13 | First accrual after activation on 2026-01-16 | 2026-01-15 | 2026-01-20 | 0.00 | 6.94 | 6.94 | 6.94 | 0.00 |
| IA-14 | Payment on 2026-02-16 clears both interest balances and reduces principal by 1,000.00 | 2026-02-16 | 2026-02-20 | 43.06 | 48.06 | 5.00 | 5.00 | 0.00 |
| IA-15 | First accrual at month-end on the 31st | 2026-01-15 | 2026-01-31 | 0.00 | 20.83 | 20.83 | 20.83 | 0.00 |

- **IA-02 to IA-04.** Prior-day cumulative interest is 10,000 × 0.05 × 29/360 = 40.277… → 40.28. The last day's post is 41.67 − 40.28 = 1.39. The two runs together post 41.67, matching the single run in IA-03. Running on each calendar date from 2026-01-16 through 2026-02-15 also posts 41.67 in total. The daily increments are differences of rounded cumulative values.
- **IA-05 and IA-06.** The second segment starts on the first due date. Its first day earns 10,000 × 0.05 × 1/360 = 1.388… → 1.39. By its due date both segments earn 41.67 each, for a total of 83.34. The first period's unpaid interest is past due only once the run date is after 2026-02-15. The second period's interest is still current on 2026-03-15.
- **IA-07.** The payment is the accrued interest on 2026-01-17, 10,000 × 0.05 × 2/360 = 2.777… → 2.78. The principal remains 10,000.00. The same segment earns 10,000 × 0.05 × 5/360 = 6.944… → 6.94 by 2026-01-20. The increase and the remaining current interest are 6.94 − 2.78 = 4.16. Splitting the segment would round the last three days to 4.17.
- **IA-08.** The payment is 5,002.78, consisting of the same 2.78 interest and half of the 10,000.00 principal. That closes the first segment. The new segment earns 5,000 × 0.05 × 3/360 = 2.083… → 2.08. Cumulative interest is 2.78 + 2.08 = 4.86.
- **IA-09.** The run date precedes the posted date. The table gives `Accrued(2026-02-15)` for reference. The run returns a no-op with `Changed = false` before performing any calculation or writing a ledger row or event. It preserves the prior posted date and balances.
- **IA-10.** The payment on 2026-02-15 is 1.39, the one-day interest from IA-05. It reduces current interest to 41.67 − 1.39 = 40.28. On the following day that remaining amount moves to past-due interest, and 1.39 starts accruing as current interest.
- **IA-13.** D5 starts interest at disbursement, so the first run includes the day before activation. The five-day cumulative amount from IA-07 is 6.94.
- **IA-14.** Immediately after IA-05, the payment is 41.67 past-due interest + 1.39 current interest + 1,000.00 principal = 1,043.06. The new segment earns 9,000 × 0.05 × 4/360 = 5.00. Cumulative interest is 43.06 + 5.00 = 48.06, and the ledger posts the 5.00 increase.
- **IA-15.** Under [30E/360](../../../../docs/domain/day-counts.md), the 31st counts as day 30. The segment runs for 15 convention days and earns 10,000 × 0.05 × 15/360 = 20.833… → 20.83.

## Exact midpoint rounding

Use these two published repository fixtures from M1 [FixedRateTests](../../../Credere.Core.Tests/Rates/FixedRateTests.cs) and [ExactInterestTests](../../../Credere.Core.Tests/Rates/ExactInterestTests.cs). Each fixture is one unpaid open segment. The previous accrual date is the segment start. No scheduled date is crossed.

| ID | Segment start | Run date | Principal | Nominal rate | Convention | Exact interest | Expected cumulative, ledger post and current interest |
|---|---|---|---:|---:|---|---:|---:|
| IA-11 | 2024-01-01 | 2024-01-21 | 1950.00 | 0.06% | ACT/360 | 0.065 | 0.06 |
| IA-12 | 2024-01-01 | 2024-01-06 | 73.00 | 1.5% | ACT/365 Fixed | 0.015 | 0.02 |

The calculations are 1950.00 × 0.0006 × 20/360 and 73.00 × 0.015 × 5/365. Half-to-even keeps 6 cents in IA-11 and raises 1 cent to 2 cents in IA-12. Past-due interest is 0.00 in both.
