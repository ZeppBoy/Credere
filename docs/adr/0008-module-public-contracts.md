# ADR-0008: Module public contracts — single `IModule` + DTOs + integration events; internals `internal`

**Status:** Accepted 2026-11-17.

## Context

A modular monolith works only if modules actually hide their internals. The temptation to `using` a neighbor's domain type "just this once" is strong, and once broken, boundaries erode quickly. We need a surface-area convention that makes the correct path the easy path.

Promissio placed types in projects by layer (`Promissio.Domain`, `Promissio.Application`, `Promissio.Infrastructure`) and left everything `public`. A caller could instantiate `Promissio.Domain.Loan` directly from anywhere. The architecture tests didn't catch this because the layering model said nothing about who inside each layer could call whom.

## Decision

Each module exposes exactly one interface and a small set of public DTOs:

### Public surface of a module

| Kind | Public | Internal |
|---|---|---|
| Module facade | `I<Name>Module` | — |
| Command/query DTOs used by the facade | `SubmitApplication`, `DisburseResult`, etc. | — |
| Strongly-typed ids referenced in the facade | `LoanId`, `LoanApplicationId`, etc. | — |
| Integration events | `LoanDisbursed`, `PaymentRecorded`, etc. | — |
| View / list DTOs returned by queries | `LoanView`, `ApplicationRow` | — |
| Error records if module-specific | e.g., `UnderwritingPolicyError` | — |
| Aggregates | — | `Loan`, `LoanApplication` |
| Repositories | — | `LoanRepository` |
| `DbContext` | — | `ServicingDbContext` |
| Domain events | — | `LoanActivated`, `PaymentReceived` |
| Feature slice handlers | — | `DisburseLoanHandler`, etc. |

### Convention

- The module's project uses `<GenerateDocumentationFile>true</GenerateDocumentationFile>`.
- Every non-public type is declared `internal`. The architecture tests enforce this: a type referenced from outside the module's assembly and declared non-public would fail compile; a type declared `public` that isn't on the allow-list fails an architecture test.
- The module's `ModuleExtensions.cs` provides `AddXModule(this IServiceCollection, IConfiguration)` as the one-and-only DI entry point.
- Integration events are placed under `src/Credere.Modules.<Name>/IntegrationEvents/`. Subscribers reference only these records.

### Allow-list for public types

A module's public types live in a small number of folders:

```
src/Credere.Modules.Servicing/
├── Application/
│   └── IServicingModule.cs      ← public
│   └── Commands/.../..Command.cs ← public (the record only; handler stays internal)
│   └── Commands/.../..Result.cs  ← public
├── Application/
│   └── Queries/.../..Query.cs   ← public
│   └── Queries/.../..View.cs    ← public
├── IntegrationEvents/
│   └── *.cs                     ← public
└── <everything else>            ← internal
```

Architecture tests encode this allow-list.

### Hosts

Hosts (`Credere.Api`, `Credere.Batch`, `Credere.Mcp`) wire modules via `AddXModule` and consume them through `I<Name>Module`. They may hold references to the module project but should not reference internal types. A host that needs internal access would indicate a module API gap — resolve by adding to the public surface, not by making types public.

## Consequences

- **Pros**
  - Module boundaries are physically enforced by C# visibility.
  - Public surface is small and readable in one glance per module.
  - Cross-module DTOs (integration events, views) are explicit contracts, not accidents.
  - "Extract this module to a service" is honest: the public surface is already the network surface.

- **Costs**
  - Discipline to mark every new type as `internal` by default. A missed `internal` is a public-surface leak.
  - DTO duplication: a `LoanView` returned by Servicing is not the same type as the `LoanRow` returned by Reporting. By design — they serve different queries — but it is still extra typing.

- **Rejected alternatives**
  - **`InternalsVisibleTo` between modules:** a direct bypass of the boundary.
  - **Public everything with naming conventions as the discipline:** the Promissio approach; failed.
  - **Shared "contracts" project per module pair:** fragments the code; buys little.
