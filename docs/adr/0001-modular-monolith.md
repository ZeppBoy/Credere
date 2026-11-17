# ADR-0001: Modular monolith with enforced boundaries

**Status:** Accepted 2026-11-17.

## Context

Credere's predecessor (Promissio) split Origination and Servicing into separate ASP.NET Core host projects with identical dependency graphs. No runtime boundary was enforced; the modules shared the same `Promissio.Application` and `Promissio.Infrastructure` libraries and could reach into each other freely. The split signalled "microservices" without providing any of the benefits, and the shared libraries slowly accumulated coupling in both directions.

Credere is a single-author portfolio project. Microservices add deployment, messaging, and operational overhead that is not warranted for a demo audience. Vertical slice architecture (feature folders in one monolith) is the opposite extreme — minimal structure, minimal signal to a hiring reviewer, and slow erosion of boundaries as the codebase grows.

We want the architectural discipline of bounded contexts (clean modules, independent evolution, "we can extract later" being a true statement) with the operational simplicity of a monolith.

## Decision

Credere is organised as a **modular monolith**. A module is a `Credere.Modules.<Name>` project with these properties:

1. One `DbContext` and one SQL schema; owns its data.
2. One public interface (`I<Name>Module`) plus DTOs and integration events; everything else is `internal`.
3. May reference only `Credere.Core`, `Credere.Shared`, and third-party packages. **Cannot reference another module's project.**
4. Communicates cross-module via integration events written to its outbox.

Boundaries are enforced at build time by `tests/Credere.ArchitectureTests/` using `NetArchTest`. Each rule in [`../architecture.md`](../architecture.md) §3 is a test. A failing architecture test is a failing build. Weakening or disabling an architecture test to make a change pass is not permitted; the fix is to redesign the dependency (usually via an integration event).

Hosts (`Credere.Api`, `Credere.Batch`, `Credere.Mcp`) are composition roots: they reference module projects for DI registration and consume them through `I<Name>Module`. They never contain business logic.

## Consequences

- **Pros**
  - Clean bounded contexts with real, testable boundaries.
  - Operationally trivial: one deployable artifact, one process, one database host.
  - "Extract to microservices later" is honest — the public contracts and integration events are the interface a message broker would consume.
  - Strong portfolio signal: shows understanding of bounded contexts, DDD modularity, and when not to pay the microservices tax.

- **Costs**
  - Discipline overhead: every tempting cross-module `using` is blocked.
  - Developers unfamiliar with the pattern will try to break the rules before learning the escape hatch (integration events).
  - Slower to make cross-cutting changes than a vertical-slice monolith.

- **Rejected alternatives**
  - **Microservices:** operational overhead unwarranted; cost to ship a demo roughly doubles.
  - **Vertical slices in one project:** no architectural discipline; no mechanism to prevent erosion.
  - **Soft boundaries via naming conventions only:** fails the first time someone needs a quick fix.
