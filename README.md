# Credere

A loan servicing platform for .NET with an AI augmentation layer, built as a learning and portfolio project.

> **Status:** Early scaffolding. Nothing in this repository executes a real loan yet. Docs describe the intended design; see [`docs/status.md`](docs/status.md) for what actually works today.

## What this project is

Credere is the back office of a bank for a single product type — term loans. It models the full life of a loan: application, underwriting decision, disbursement, interest accrual, payments (with real allocation to interest and principal), aging through delinquency states, and payoff. On top of that it exposes an AI layer — an MCP server for direct Claude Desktop access plus three agents (credit decisioning, early warning, collections) with deterministic policy guardrails and versioned quality evaluations.

The design is a **modular monolith**: strict, build-enforced bounded contexts inside a single deployable artifact. Modules communicate only through public interfaces and integration events delivered via a transactional outbox. Extracting any module to its own process later is a mechanical change, not a redesign.

## What this project is not

- Not a production banking system. Credere is a portfolio project and a learning artifact.
- No double-entry general ledger. Loan-side balances only.
- No real payment rails (SEPA / ACH settlement). Payments are atomic.
- No multi-tenancy, no multi-currency, no real KYC/AML, no real credit bureau integration.
- No regulatory or legal certification. Treat every calculation as educational unless independently verified against a cited reference source.

## Stack

| Area | Choice |
|---|---|
| Runtime | .NET 10, C# latest |
| HTTP | ASP.NET Core minimal APIs |
| Primary DB | SQL Server 2022 (Developer Edition locally) |
| Secondary DB | MongoDB 7 (AI artifacts + document blobs) |
| ORM | EF Core 10 (one `DbContext` per module) |
| Dates/money | NodaTime + custom `Money` / `Percentage` / `InterestRate` |
| AI | `Microsoft.Extensions.AI`, Anthropic Claude as primary provider |
| MCP | `ModelContextProtocol` C# SDK |
| Observability | OpenTelemetry + Langfuse |
| Frontend | Next.js 15 (App Router) + TypeScript + Tailwind CSS + shadcn/ui |
| Testing | xUnit, FluentAssertions, FsCheck, Verify.Xunit, Testcontainers, Stryker.NET |
| Architecture tests | NetArchTest (module boundaries) |
| PDFs | QuestPDF |

See [`docs/architecture.md`](docs/architecture.md) for the module map and dependency rules.

## Get started

Requirements: .NET 10 SDK, Node 20+ with Corepack (for pnpm), Docker Desktop.

```sh
# Clone and enter
git clone <repo> credere && cd credere

# Start dependencies
docker compose up -d

# Restore + build + test the .NET side
dotnet restore
dotnet build
dotnet test

# Install + run the frontend
corepack enable pnpm
pnpm --dir web install
pnpm --dir web dev
```

Visit `http://localhost:5000/health` for the API health endpoint and `http://localhost:3000` for the banker UI.

## Documentation

Start with [`docs/README.md`](docs/README.md). Key entries:

- [`docs/architecture.md`](docs/architecture.md) — modular-monolith principles, module map, dependency rules.
- [`docs/roadmap.md`](docs/roadmap.md) — milestones and acceptance criteria.
- [`docs/status.md`](docs/status.md) — what works right now (one living document, not a sprawl).
- [`docs/adr/`](docs/adr/) — architecture decisions (accepted only; proposals live in `docs/open-questions.md`).
- [`docs/domain/`](docs/domain/) — banking concepts and formulas with citations.
- [`docs/modules/`](docs/modules/) — one doc per bounded context.
- [`docs/ai/`](docs/ai/) — MCP tools, agents, guardrails, evaluations.

Working with an AI coding agent: read [`AGENTS.md`](AGENTS.md) first. Claude Code specifics live in [`CLAUDE.md`](CLAUDE.md).

## License

[MIT](LICENSE). No warranty. Operator-responsible if adapted for real use.
