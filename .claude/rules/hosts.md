---
paths:
  - "src/Credere.Api/**"
  - "src/Credere.Batch/**"
  - "src/Credere.Mcp/Program.cs"
  - "tests/Credere.Integration.Tests/**"
---

# Hosts

Hosts handle composition, transport translation and OpenTelemetry setup. If a host seems to need a module's internal type, the module's public API is missing something. Add it to the public surface instead of making the type public (ADR-0008).

- **Error mapping (ADR-0006):**
  - `ValidationError` → 400 problem-details
  - `NotFound` → 404
  - `ConcurrencyConflict` → 409
  - `InvalidStateTransition` and `ConflictingTerms` → 422
  - any unmapped `DomainError` → 500 with a logged correlation id
  - MCP returns `{ ok: false, error: { code, message } }`
- **Endpoints:** one `Map<Module>Endpoints` extension per module, using `MapGroup("/api/<module>")`. Handlers return `TypedResults` (for example `Results<Ok<LoanView>, ProblemHttpResult>`), so the OpenAPI output that generates the frontend's types is accurate.
- **Edge validation:** `AddProblemDetails()`, plus .NET 10's built-in minimal-API validation (`AddValidation()`) for DataAnnotations on request DTOs. Domain rules are still checked in `Create(...)` and the handlers.
- **`CommandId`:** the client generates it, and the API rejects a command that arrives without one.
- **Batch:** jobs are idempotent per business date. Running a date twice produces no duplicate events.
- **Endpoint tests** use `WebApplicationFactory` with Testcontainers, and cover the happy path plus ≥3 failure modes.
