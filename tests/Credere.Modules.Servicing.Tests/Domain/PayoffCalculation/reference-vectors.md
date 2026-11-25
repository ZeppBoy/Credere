# Payoff reference vectors

The source is Credere's owner-approved [D5 payoff rules](../../../../docs/modules/servicing.md#payoff-quotes) and [ADR-0011](../../../../docs/adr/0011-m2-module-contracts.md#decision). Each total is principal + unpaid past-due interest + unpaid current interest through the quote date + fees. The [interest accrual vectors](../InterestAccrual/reference-vectors.md) supply the dated interest amounts. All amounts are EUR and fees are 0.00 under D5.

Rows PO-01 through PO-06 use the M1 interest-only schedule and no payments. For PO-07 and PO-08, the respective payment in the linked accrual case has already been recorded. The total is the exact sum of the four displayed components.

| ID | Payoff date | Source or starting state | Principal | Past-due interest | Current interest | Fees | Expected total |
|---|---|---|---:|---:|---:|---:|---:|
| PO-01 | 2026-01-15 | IA-01, accrual from disbursement has zero length | 10000.00 | 0.00 | 0.00 | 0.00 | 10000.00 |
| PO-02 | 2026-02-14 | IA-02 | 10000.00 | 0.00 | 40.28 | 0.00 | 10040.28 |
| PO-03 | 2026-02-15 | IA-03, quote on the first scheduled due date | 10000.00 | 0.00 | 41.67 | 0.00 | 10041.67 |
| PO-04 | 2026-02-16 | IA-05, first period now past due | 10000.00 | 41.67 | 1.39 | 0.00 | 10043.06 |
| PO-05 | 2026-03-15 | IA-06, exactly two periods accrued | 10000.00 | 41.67 | 41.67 | 0.00 | 10083.34 |
| PO-06 | 2026-01-20 | IA-07 cumulative amount before any payment | 10000.00 | 0.00 | 6.94 | 0.00 | 10006.94 |
| PO-07 | 2026-01-20 | IA-07, after paying interest on 2026-01-17 | 10000.00 | 0.00 | 4.16 | 0.00 | 10004.16 |
| PO-08 | 2026-01-20 | IA-08, after paying interest and half the principal on 2026-01-17 | 5000.00 | 0.00 | 2.08 | 0.00 | 5002.08 |

## Date validation and quote revalidation

| ID | Input | Expected outcome |
|---|---|---|
| PO-09 | `AccruedThroughDate` is 2026-02-16 as in IA-05. Quote for 2026-02-15. | `ValidationError` for `payoffDate`. |
| PO-10 | Execute PO-06 for 2026-01-20 after the principal payment in IA-08. | `PayoffQuoteStale` with `QuotedTotal` 10006.94 and `CurrentTotal` 5002.08. Loan state is unchanged. |
| PO-11 | Execute PO-08 for 2026-01-20 with the IA-08 loan state. | Recomputed total equals 5002.08. Payment of that amount closes all four balances with 0.00 excess. |
