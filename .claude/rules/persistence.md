---
paths:
  - "src/**/Infrastructure/**"
  - "src/**/Migrations/**"
  - "**/*DbContext*"
  - "**/*Repository*"
  - "**/*Outbox*"
---

# Persistence (EF Core + MongoDB)

ADR-0002 to ADR-0005 and `docs/architecture.md` §5 describe the design. These are the specifics of this solution.

## EF Core / SQL Server

- **Schema isolation:** each module's `DbContext` sets `HasDefaultSchema("<module>")` and `MigrationsHistoryTable("__EFMigrationsHistory", "<module>")`, so modules never share migration history.
- **Decimals:** every `decimal` column has an explicit `HasPrecision(p, s)`, because EF's implicit `decimal(18,2)` silently truncates rates. Record the chosen precision in the module doc.
- **NodaTime:** map with explicit value converters, `LocalDate` → `date` and `Instant` → UTC `datetime2`. Core and Shared can't reference EF Core, so converters live in the module's `Infrastructure/`. Where shared converters should live is an open question; ask before creating a new project.
- **Concurrency:** every aggregate state table has a `RowVersion` (`IsRowVersion()`). The repository turns `DbUpdateConcurrencyException` into a `ConcurrencyConflict` result.
- **One transaction:** the state row, history row (ADR-0004), outbox row (ADR-0005) and command receipt (ADR-0007) are written in one `SaveChangesAsync`.
- **Queries:** `AsNoTracking()`, with results projected to view DTOs. `IQueryable` and entity types stay inside `Infrastructure/`. No lazy loading.
- **Raw SQL** goes through `FromSql` or `ExecuteSql` with parameterised interpolation, never concatenated strings.
- **Migrations:** `dotnet ef migrations add <Name> --project src/Credere.Modules.<X> --startup-project src/Credere.Api --context <X>DbContext --output-dir Infrastructure/Migrations`. This needs the `dotnet-ef` tool; add a tool manifest when the first migration lands.

## MongoDB

- Register serializers explicitly at startup: `GuidSerializer(GuidRepresentation.Standard)`, `decimal` as `Decimal128`, and dedicated serializers for NodaTime types. Driver defaults have changed between versions.
- Collection names follow `docs/architecture.md` §5.
- The Documents module commits the SQL metadata row only after the Mongo blob write succeeds, and compensates on failure (`docs/modules/documents.md`).
