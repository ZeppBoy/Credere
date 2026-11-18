---
paths:
  - "src/Credere.Modules.*/**"
  - "tests/Credere.Modules.*/**"
  - "docs/modules/**"
  - "docs/domain/loan-lifecycle.md"
---

# Module work

AGENTS.md §3, §5 and §6 hold the rules, and `docs/architecture.md` §4 shows the internal layout. This file describes what a finished piece of module work contains.

## A feature slice is complete when

- `Application/Commands/<Name>/` (or `Queries/<Name>/`) holds the public command or query record, the internal handler, the public result or view type, and the validator.
- For a command, the receipt row, state change, history row and outbox row are written in one transaction (ADR-0004, ADR-0005, ADR-0007).
- The slice is exposed on `I<Module>Module` and registered in `Add<Module>Module`.
- Tests in `tests/Credere.Modules.<Module>.Tests` cover it, using Testcontainers for persistence. Those tests and `tests/Credere.ArchitectureTests` pass.
- `docs/modules/<module>.md` describes the slice.

## An integration event is complete when

- A public record exists under the publisher's `IntegrationEvents/`, marked `[Version(1)]`.
- It is listed in the catalog in `docs/architecture.md` §6 and in the publisher's module doc.
- Each subscriber has a handler under `Application/IntegrationEventHandlers/` that is idempotent per subscriber and event id.
- A test proves the outbox delivers the event to its subscribers.

## Changing the loan state machine

Update the diagram in `docs/domain/loan-lifecycle.md` before the code, so the design is reviewed first. Then:
- Each transition is explicit, returns `Result<T, DomainError>`, and emits a domain event.
- Every new transition has tests, valid and invalid.
- No state is left unreachable or without an exit.
