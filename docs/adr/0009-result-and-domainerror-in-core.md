# ADR-0009: `Result<T, E>` and the `DomainError` hierarchy live in `Credere.Core`

**Status:** Accepted 2026-11-19. Amends [ADR-0006](0006-error-semantics.md), which stays as written (ADRs are append-only): the placement of the types and the type of `InvalidStateTransition.FromState` below replace what ADR-0006 shows. Everything else in ADR-0006 still applies.

## Context

AGENTS.md §6 and [`domain/value-objects.md`](../domain/value-objects.md) require Core value objects (`Money`, `Percentage`, `InterestRate`, …) to expose `Create(...)` methods that return `Result<T, DomainError>`. But `architecture.md` §2, the roadmap and the AGENTS.md §3 code map put `Result` in `Credere.Shared`, and the architecture test `Core_depends_only_on_BCL_and_NodaTime` forbids `Credere.Core` from referencing `Credere.Shared`. Taken together, M1 cannot compile: Core needs a type that lives in a project it may not reference.

Separately, ADR-0006 declares `InvalidStateTransition(object FromState, …)`. AGENTS.md §5 forbids `object` in public API types.

## Decision

1. `Result<T, E>`, `Unit`, the `DomainError` base record and the common error records ADR-0006 lists (`ValidationError`, `InvalidStateTransition`, `ConcurrencyConflict`, `NotFound`, `ConflictingTerms`) live in `Credere.Core`, under `Results/`. Module-specific errors are not part of this move: as ADR-0006 says, a new error type extends `DomainError` in the owning module, and as [ADR-0008](0008-module-public-contracts.md) says, it is public when the module's facade exposes it (for example `UnderwritingPolicyError`). The moved types are BCL-only code, so Core's dependency rule (NodaTime only) is unchanged. No architecture test changes.
2. Modules already reference `Credere.Core`, so every module and host sees these types without a new reference.
3. `InvalidStateTransition` takes the state as `string`:

   ```csharp
   public sealed record InvalidStateTransition(string FromState, string Command, string Reason)
       : DomainError("invalid-state-transition", $"{Command} not allowed from {FromState}: {Reason}");
   ```

   Callers pass the state's name, for example `nameof(LoanState.Active)`, or its `ToString()`.
4. The contract of `Result<T, E>`, the error hierarchy's codes and messages, and the transport mapping stay as in ADR-0006.
5. `Credere.Shared` keeps the time-free cross-module abstractions only (integration-event marker, command marker, handler interface). See [open question #8](../open-questions.md#8-where-do-shared-persistence-converters-live) for where the outbox and receipt rows live.

## Consequences

- **Pros**
  - One rule for where an error type goes: common errors in Core, module-specific errors in their module.
  - M1 compiles with the dependency rules and the architecture tests exactly as they are.
  - The error vocabulary sits next to the value objects that produce it.
  - The `object` in a public API type is gone.
- **Costs**
  - Contract examples outside this ADR's scope still show the old shapes: `new InvalidStateTransition(state, …)` in `domain/loan-lifecycle.md` passes a state value, and `architecture.md` §8 shows a separate public `Error` DTO where `modules/origination.md` returns `DomainError`. They are aligned in a follow-up contract task, M1-C1, not here.
  - `Credere.Core` is no longer only math and value objects; it also holds the result type. Core stays small and BCL/NodaTime-only, so the "shared kernel" description still holds.
  - Code that read ADR-0006 and the earlier docs as "Result is in Shared" is wrong. The D1 lines in `architecture.md`, `roadmap.md` and AGENTS.md §3 are updated together with this ADR.
- **Rejected alternative**
  - **Let `Credere.Core` reference `Credere.Shared`.** It would keep `Result` in Shared, but it needs an architecture-test change, an AGENTS.md §3 change and a weaker dependency graph, and it would make Core depend on a project whose scope is cross-module plumbing. Moving the type is the smaller change.
