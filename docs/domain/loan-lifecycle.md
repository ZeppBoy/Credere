# Loan lifecycle

The Servicing module's `Loan` aggregate moves through a fixed state machine. Every transition emits a domain event and (via the outbox) an integration event. Invalid transitions return `Result<T, DomainError>`; they do not throw.

## States

```
             ┌──────────────┐
             │   Disbursed  │   initial (after confirmed disbursement)
             └──────┬───────┘
                    │ Activate (first business day after disbursement)
                    ▼
             ┌──────────────┐
    ┌────────┤    Active    ├────────┐
    │        └──────┬───────┘        │
    │               │                │
    │  ApplyAging   │  ApplyAging    │  ApplyAging
    │  (days=0)     │  (0<d<pd)      │  (d≥default)
    │               ▼                │
    │        ┌──────────────┐        │
    │   ┌────┤   InGrace    ├────┐   │
    │   │    └──────┬───────┘    │   │
    │   │           │            │   │
    │   │ ApplyAging│ ApplyAging │   │
    │   │ (d≥pd)    │ (d≥def)    │   │
    │   │           ▼            │   │
    │   │    ┌──────────────┐    │   │
    │   └────┤   PastDue    ├────┘   │
    │        └──────┬───────┘        │
    │               │                │
    │ ApplyAging    │ ApplyAging     │
    │ (d=0, Cure)   │ (d≥default)    │
    │               ▼                │
    │        ┌──────────────┐        │
    │        │   Defaulted  │◀───────┘
    │        └──────┬───────┘
    │               │
    │     ┌─────────┼─────────┐
    │     │         │         │
    │     ▼         ▼         ▼
    │  ┌──────┐┌─────────┐┌──────────┐
    │  │Write ││Restruc- ││Recovered │
    │  │ Off  ││ tured   ││          │   (terminal states)
    │  └──────┘└─────────┘└──────────┘
    │
    │  RecordPayment (that pays off the balance) from any non-terminal state:
    └──────────────────────────────────────────────────────▶ PaidOff (terminal)
```

- `Disbursed` — initial state immediately after confirmed disbursement.
- `Active` — performing; no days past due.
- `InGrace` — 1 day ≤ days past due < `PastDueThreshold` (default 1 — i.e., grace period is zero days by default; configurable).
- `PastDue` — `PastDueThreshold` ≤ days past due < `DefaultThreshold`.
- `Defaulted` — days past due ≥ `DefaultThreshold` (default 90).
- `WrittenOff`, `Restructured`, `Recovered`, `PaidOff` — terminal.

## Transition table

Every row below is implemented and tested.

| From | Command | Guard | To | Event emitted (domain) | Integration event |
|---|---|---|---|---|---|
| Disbursed | Activate | effectiveDate is the first business day after disbursement; not a holiday | Active | `LoanActivated` | — (internal) |
| Active | ApplyAging | days=0 (explicit no-op; no event) | Active | — | — |
| Active | ApplyAging | 0 < days < pastDueThreshold | InGrace | `LoanEnteredGracePeriod` | `LoanAged` |
| Active | ApplyAging | pastDueThreshold ≤ days < defaultThreshold | PastDue | `LoanBecamePastDue` | `LoanAged` |
| Active | ApplyAging | days ≥ defaultThreshold | Defaulted | `LoanDefaulted` | `LoanAged` |
| InGrace | ApplyAging | days=0 (Cure) | Active | `LoanBecameActive` | `LoanAged` |
| InGrace | ApplyAging | pastDueThreshold ≤ days < defaultThreshold | PastDue | `LoanBecamePastDue` | `LoanAged` |
| InGrace | ApplyAging | days ≥ defaultThreshold | Defaulted | `LoanDefaulted` | `LoanAged` |
| PastDue | ApplyAging | days=0 (Cure) | Active | `LoanBecameActive` | `LoanAged` |
| PastDue | ApplyAging | pastDueThreshold ≤ days < defaultThreshold (re-aging) | PastDue | `LoanPastDueDaysUpdated` | `LoanAged` |
| PastDue | ApplyAging | days ≥ defaultThreshold | Defaulted | `LoanDefaulted` | `LoanAged` |
| Active / InGrace / PastDue | RecordPayment | amount > 0, amount ≤ remaining balance, currency matches | same (or PaidOff if closes balance) | `PaymentReceived` and optionally `LoanPaidOff` | `PaymentRecorded`, optionally `LoanPaidOff` |
| Active / InGrace / PastDue | Payoff | — | PaidOff | `LoanPaidOff` | `LoanPaidOff` |
| Defaulted | WriteOff | reason non-empty | WrittenOff | `LoanWrittenOff` | `LoanWrittenOff` |
| Defaulted | Restructure | plan non-empty | Restructured | `LoanRestructured` | `LoanRestructured` |
| Defaulted | Recover | method non-empty | Recovered | `LoanRecovered` | `LoanRecovered` |

Any other `(state, command)` pair returns `Result<Unit, DomainError>.Failure(new InvalidStateTransition(state, command, reason))`. **No exceptions.**

## Thresholds

- `pastDueThreshold` — default 1 day. Configurable per product.
- `defaultThreshold` — default 90 days. Configurable per product; subject to IFRS 9 and local regulatory floors (see [`ifrs9-staging.md`](ifrs9-staging.md)).

Both thresholds live on the loan's approved terms (immutable per loan after disbursement).

## Days-past-due semantics

- `daysPastDue` is computed by the aging batch, not by the aggregate. The aggregate receives `daysPastDue: int` as a command parameter.
- The aggregate tracks `DaysPastDue` as nullable (null when `State == Active`, non-null otherwise).
- "Business days" interpretation is governed by the product's `HolidayCalendar`; the aging batch is responsible for that interpretation and passes the numeric result in.

## Rehydration

The aggregate supports rehydration from its persisted event stream. A rehydrated aggregate emits no events during replay. Any event in the stream that this version of the code does not understand is a hard error; schema evolution requires explicit upcasters (see [`../adr/0004-append-only-history.md`](../adr/0004-append-only-history.md)).

## Diagram sync

The ASCII diagram in this document and the transition table are the authoritative specification. The aggregate's `ApplyAging` / `Activate` / `RecordPayment` / `Payoff` / `WriteOff` / `Restructure` / `Recover` implementations must match this table exactly. A PR that changes either must update both.
