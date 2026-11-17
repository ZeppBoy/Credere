# Contributing to Credere

Credere is a learning and portfolio project, but it is maintained as if it were production code. Contributions are welcome under the rules below.

## Before you start

- Read [`AGENTS.md`](AGENTS.md). It applies to human contributors too.
- Read [`docs/architecture.md`](docs/architecture.md) to understand the module boundaries.
- Check [`docs/open-questions.md`](docs/open-questions.md) if your change touches an undecided area — please comment before implementing.

## Development setup

Requirements: .NET 10 SDK, Node 20+ with Corepack, Docker Desktop.

```sh
docker compose up -d
dotnet restore && dotnet build && dotnet test
corepack enable pnpm
pnpm --dir web install && pnpm --dir web build
```

## Branches

- `main` is always deployable.
- Feature branches: `feat/<short-description>`.
- Fix branches: `fix/<short-description>`.
- Documentation branches: `docs/<short-description>`.

## Commit messages

Follow the [Conventional Commits](https://www.conventionalcommits.org/) specification. The body explains **why** the change exists; the diff shows what. One commit = one logical change.

Allowed types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `chore`, `build`, `ci`.

Example:

```
feat(servicing): implement payment allocation waterfall

Allocates an incoming payment in the order fees → past-due interest →
current interest → principal, per ECB consumer-credit illustrative example
2019-04-12, §3.2. Introduces PaymentAllocationWaterfall as an internal
pure function; Loan.RecordPayment delegates to it and emits one
PaymentReceived event with the computed breakdown.

Refs: docs/domain/payment-allocation.md
```

## Pull requests

- One PR per logical change. No "while I was at it" mixed bags.
- PR under ~400 lines of production diff. Larger PRs need a reviewer-aligned reason.
- PR description covers: what changed, why, how to test, which ADRs are relevant.
- Self-review the diff before requesting human review.
- Update documentation in the same PR as the code (ADR, module doc, state machine, README).

## Required checks

Before opening a PR:

- [ ] `dotnet format` is clean.
- [ ] `dotnet build` passes with zero warnings.
- [ ] `dotnet test` passes from the repo root.
- [ ] `pnpm --dir web lint` and `pnpm --dir web build` pass if the frontend changed.
- [ ] Architecture tests (`tests/Credere.ArchitectureTests/`) are green.
- [ ] Documentation updated where relevant.
- [ ] No secrets or PII in the diff.

For changes to financial calculations, additionally:

- [ ] A reference source is cited in code and tests.
- [ ] At least three independent reference test cases pass.
- [ ] A human reviewer with banking context is requested.

## Dependency changes

New NuGet or npm packages require an ADR explaining why the locked stack is not enough. Keep `Directory.Packages.props` and `web/package.json` central.

## Security

Do not report security issues in public issues. Email the maintainer address listed in the repository metadata.
