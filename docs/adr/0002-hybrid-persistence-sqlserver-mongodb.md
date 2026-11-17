# ADR-0002: Hybrid persistence — SQL Server for aggregates, MongoDB for AI artifacts + blobs

**Status:** Accepted 2026-11-17.

## Context

Credere's data has two fundamentally different shapes:

1. **Aggregate state** — loans, applications, payments, documents metadata. Deeply relational. Transactional. Needs optimistic concurrency and append-only history for audit.
2. **AI artifacts and PDF blobs** — agent transcripts, LLM call logs, eval results, golden datasets, generated PDFs. Schema-evolving, document-shaped, append-mostly, no transactional cross-coupling with aggregates.

Promissio locked in both PostgreSQL + Marten (document+event store on top of Postgres) and EF Core; only Marten was ever used. The dual stack cost was paid up front and never recovered.

For Credere we considered:

- PostgreSQL only.
- SQL Server only (store AI artifacts as JSON columns).
- MongoDB only (model aggregates as documents).
- Hybrid SQL Server + MongoDB.
- Hybrid PostgreSQL + MongoDB.

## Decision

Use a **hybrid**: **SQL Server 2022** for aggregate state, **MongoDB 7** for AI artifacts and document blobs.

Choice of SQL Server over PostgreSQL:

- EF Core's support for SQL Server is the most mature in the .NET ecosystem, especially `rowversion` optimistic concurrency.
- Target audience (fintech employers, banking-domain practitioners) is dominated by SQL Server shops.
- Developer Edition is free and container-ready (`mcr.microsoft.com/mssql/server:2022-latest`).

Choice of MongoDB for AI side:

- Agent sessions, LLM call records, and golden datasets are naturally document-shaped and schema-evolving. Modeling them as JSON columns in SQL Server would guarantee a slow drift into unstructured JSON queries.
- PDF blob storage over an object store would require a third container (MinIO). Mongo blobs are good enough for a portfolio demo and documented as such.
- Demonstrates NoSQL on the résumé without forcing document modeling onto loan data.

## Consequences

- **Pros**
  - Each store is used for the shape of data it is best at.
  - No JSON-column drift in SQL Server.
  - Clear operational story: "if the data participates in a loan transaction, it's in SQL; if it's an AI artifact or a blob, it's in Mongo."
  - Fewer containers than a Marten+EF+objectstore stack.

- **Costs**
  - Two client libraries, two connection strings, two sets of Testcontainers.
  - Any workflow that touches both stores (notably document generation) needs a two-phase pattern with compensation on failure. See `docs/modules/documents.md`.
  - A minor chance of blob orphans on failure; mitigated by the janitor process in M3.

- **Rejected alternatives**
  - **Postgres only** (status-quo-ish): no significant advantage for this project; weaker fintech signal in a .NET-focused portfolio.
  - **Mongo only:** loans are deeply relational; using Mongo as the primary loses transactions and introduces modeling pain.
  - **Marten (event sourcing in Postgres):** adds versioning/upcasting complexity for a single aggregate; the Promissio experience showed the tax outweighs the demo value.
  - **SQL Server + MinIO:** adds a third container for marginal benefit; deferred to a future ADR if Mongo blobs prove insufficient.
