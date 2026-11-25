# ADR-0011: M2 module contracts — handoff, payment excess, aging, business dates, accrual and payoff

**Status:** Accepted 2026-11-25. Records owner decisions D3, D4 and D5 of the [M2 plan](../plans/m2.md), approved as recommended on issue #65. The detailed contracts are in [Origination](../modules/origination.md), [Servicing](../modules/servicing.md) and [loan-lifecycle.md](../domain/loan-lifecycle.md); this ADR records why.

## Context

The M2 module docs disagreed with each other and with the architecture:

- The catalog named `ApplicationDecided` as the handoff to Servicing. The Servicing doc consumed `ApplicationAccepted`, and its `Loan` held a `LoanApplicationId`, an Origination type. A module project may reference only `Credere.Core` and `Credere.Shared` ([ADR-0001](0001-modular-monolith.md), architecture §3), so Servicing cannot name Origination's event or id.
- The lifecycle diagram, the transition table and the waterfall text disagreed on overpayments and on which states accept a payment. The roadmap says every transition emits an event, while aging an active loan with zero days past due is a no-op.
- Business calendars, accrual posting and rounding, and payoff quote validity were left open, and AGENTS.md §7 forbids choosing them without a reference or the owner.

## Decision

**D4 — The handoff is `ApplicationAccepted`, translated by the host.**
- Origination publishes `ApplicationAccepted` after the borrower accepts the final terms. `ApplicationDecided` is a decision, not a handoff.
- A host-owned handler maps the event to a Servicing-owned command, `RegisterDisbursementIntent`, with an opaque `SourceReference` and the `AcceptedTermsVersion`. `EventId` becomes `CommandId`, so a duplicate delivery is a receipt hit ([ADR-0007](0007-command-idempotency.md)).
- Servicing persists a `DisbursementIntent`, unique on `(SourceReference, AcceptedTermsVersion)`, before treasury confirms the disbursement. `Disburse` takes the intent id.
- The same rule applies to any other cross-module subscription, because the dependency rule leaves no other way: the subscriber owns its input, and the host translates. Each later module's contract task defines its inputs.
- In M2 the handler lives in the end-to-end test and runs against the real outbox payload and subscriber receipt. The production dispatcher, polling and retries stay with M4.

**D3 — Payments, excess and aging.**
- Payments are accepted in `Active`, `InGrace` and `PastDue`. Payments on `Defaulted` loans are a separate, open decision, and return `InvalidStateTransition` until it is made.
- A positive overpayment on an eligible loan is allowed. It is allocated through the waterfall; the unplaced remainder is the `Excess`, which is recorded for return in `PaymentRecorded` and in `PaymentExcessRecorded`. An excess means the balance reached zero, so the loan becomes `PaidOff`. The loan keeps no credit balance.
- `ApplyAging` is a state transition, a days update, or a no-op. A no-op changes neither state nor days past due, emits no event and writes no history, but stores its receipt. A days update emits `LoanPastDueDaysUpdated` and `LoanAged`. The transition table and the diagram are one specification.

**D5 — Business dates, accrual, payoff.**
- The host supplies every business date as an explicit field; calendars are configuration; the module ships no jurisdiction data.
- Supported in M2: `FixedRate`, and the annuity, differentiated and interest-only bullet schedules from M1. Floating and tiered rates and zero-coupon bullets are not.
- Interest accrues cumulatively over constant-principal segments, rounded once per segment inside `IInterestCalculator.Calculate`. The ledger row for a date is the difference between cumulative amounts, so the total does not depend on how often accrual runs, and an on-schedule loan reproduces the schedule's `InterestDue` to the cent. This is a Credere design decision with no regulatory source; M2-03 writes vectors first and the owner approves them.
- A payoff quote is stateless. `ExecutePayoff` recomputes the total for the quoted date and returns `PayoffQuoteStale` on any difference.

## Consequences

- **Pros**
  - No module references another. The one place that knows both sides of a handoff is the host, where [ADR-0008](0008-module-public-contracts.md) already allows it.
  - Retried and duplicate deliveries are harmless at every step: the receipt, the intent key and the accrual date each make the repeat a no-op.
  - Accrual is reproducible for any date, which keeps batch replay ([AGENTS.md](../../AGENTS.md) §5, Time) honest.
  - The lifecycle contract has no row that contradicts the roadmap.
- **Costs**
  - A mapping to write and test per cross-module subscription, with a duplicated input record (`AcceptedLoanTerms` alongside `DecidedTerms`). That is the DTO duplication ADR-0008 accepts.
  - Cumulative segment accrual is more state than daily rounding: the aggregate tracks segments and the past-due roll.
  - M2 cannot disburse a product whose terms need a floating or tiered rate.
- **Not decided here**
  - Payments on, and cure from, `Defaulted` loans.
  - The day count, frequency and repayment type of the M2 demo product.
- **Rejected alternatives**
  - **Servicing subscribes to `ApplicationDecided`:** it starts a disbursement before the borrower accepts, and it needs Origination's types.
  - **`InternalsVisibleTo` or a shared contracts project:** the bypass ADR-0008 rejects.
  - **Daily rounded accrual:** each day's `Money` rounds on its own, so the sum drifts from the period interest by up to half a cent a day, and from the schedule.
  - **Keeping overpayments as a credit balance on the loan:** a balance that is neither principal nor interest, which the waterfall and the invariants do not model.
  - **A stored payoff quote with an expiry:** adds a table and a clock read for a value that is a pure function of the loan and the date.
