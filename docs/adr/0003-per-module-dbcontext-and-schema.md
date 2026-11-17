# ADR-0003: Per-module `DbContext` + schema isolation

**Status:** Accepted 2026-11-17.

## Context

A modular monolith can share a single `DbContext` across modules (one set of migrations, implicit FKs are easy) or give each module its own `DbContext` (independent evolution, no cross-module FKs, slower migrations).

Shared `DbContext` is tempting because:
- One migration pipeline.
- FKs enforce referential integrity across modules.
- Queries can join across modules at the SQL level.

But each of those "pros" is a direct violation of the modular-monolith contract: cross-module FKs are a backdoor dependency; cross-module joins are a bypass of the public interface; a shared migration pipeline means every module's schema is coupled to every other module's schema versioning.

## Decision

Each module in Credere owns one `DbContext` and one SQL Server schema:

| Module | DbContext | Schema |
|---|---|---|
| Origination | `OriginationDbContext` | `origination` |
| Servicing | `ServicingDbContext` | `servicing` |
| Documents | `DocumentsDbContext` | `documents` |
| Reporting | `ReportingDbContext` | `reporting` |
| Ai | `AiDbContext` | `ai` |

Rules:

1. All `DbContext` classes point at the same SQL Server instance (one database connection per deployment).
2. Each `DbContext` only reads / writes its own schema.
3. **No cross-schema FKs.** References between schemas (e.g., `reporting.Loans.LoanId` ↔ `servicing.Loans.Id`) are stored as plain values, not as FK constraints. Integrity is maintained by the integration-event projection and the receipt tables.
4. Each module has its own migrations pipeline. Migrations run in a documented order on startup: Core-ish Shared tables first (if any), then module migrations in a stable order. A host helper `MigrateAllAsync()` handles this.
5. The `DbContext` type is `internal` to the module. The public interface does not expose it.

Migrations tooling:

- Each module's `.csproj` has `Microsoft.EntityFrameworkCore.Design` referenced for the `dotnet ef` CLI.
- Migrations live in `src/Credere.Modules.<Name>/Infrastructure/Migrations/`.
- `dotnet ef` commands are run per-module. Example:
  ```sh
  dotnet ef migrations add InitialServicing \
      --project src/Credere.Modules.Servicing \
      --startup-project src/Credere.Api \
      --context ServicingDbContext \
      --output-dir Infrastructure/Migrations
  ```

## Consequences

- **Pros**
  - Modules can evolve their schemas independently.
  - No sneaky cross-module dependencies via FKs.
  - Easier to extract a module later: the schema is already isolated; the FK-less value references become network-call references.
  - Smaller `DbContext` classes — faster EF Core model building per module.

- **Costs**
  - Multiple migrations pipelines; developers must know which `--context` to pass to `dotnet ef`.
  - No database-level referential integrity across modules. We rely on the projection + receipt mechanism (integration events + outbox + idempotent handlers) to keep cross-module references consistent.
  - A junk row in one schema cannot be caught by FK — must be caught by a projection-level check (and a test).

- **Rejected alternatives**
  - **Single shared `DbContext`:** fails the boundary contract.
  - **One database per module:** operational overhead unwarranted for a portfolio; multi-database joins in reporting become painful.
