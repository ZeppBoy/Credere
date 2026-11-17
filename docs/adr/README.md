# Architecture Decision Records

ADRs record **accepted** architectural decisions. Credere does not keep proposed-only ADRs — undecided questions live in [`../open-questions.md`](../open-questions.md) until they become decisions.

Each ADR follows:

- **Status:** Accepted on <date>, or Superseded by <other ADR> on <date>.
- **Context:** the forces at play.
- **Decision:** what we are doing.
- **Consequences:** what this makes easier or harder.

ADRs are append-only. When a decision changes, we add a new ADR that supersedes the old one (which stays, marked superseded, linking forward).

## Index

- [0001 — Modular monolith with enforced boundaries (NetArchTest)](0001-modular-monolith.md)
- [0002 — Hybrid persistence: SQL Server for aggregates, MongoDB for AI artifacts + blobs](0002-hybrid-persistence-sqlserver-mongodb.md)
- [0003 — Per-module `DbContext` + schema isolation](0003-per-module-dbcontext-and-schema.md)
- [0004 — Per-aggregate append-only history, transactional with state writes](0004-append-only-history.md)
- [0005 — Transactional outbox for cross-module integration events](0005-transactional-outbox.md)
- [0006 — Error semantics: `Result<T, DomainError>`](0006-error-semantics.md)
- [0007 — Command idempotency via `CommandId` + per-handler receipts](0007-command-idempotency.md)
- [0008 — Module public contracts: single `IModule` + DTOs + integration events; internals `internal`](0008-module-public-contracts.md)
