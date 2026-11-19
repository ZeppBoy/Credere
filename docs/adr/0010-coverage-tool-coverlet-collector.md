# ADR-0010: Line coverage is measured with `coverlet.collector`

**Status:** Accepted 2026-11-19.

## Context

AGENTS.md §8 sets line-coverage targets: ≥90% for `Credere.Core` and `Credere.Shared`, ≥85% for module Application and Domain code. Mutation coverage for Core is measured by Stryker.NET. Nothing in the locked stack measures line coverage, so the targets cannot be checked. AGENTS.md §4 requires an ADR and owner approval for a new dependency.

There is no CI until M7, so the measurement has to run locally with `dotnet test`.

## Decision

Use `coverlet.collector` in the test projects, run through the standard test command:

```sh
dotnet test --collect:"XPlat Code Coverage"
```

- The package is added to the test projects in M1-00 (tooling), with its version in `Directory.Packages.props`.
- It is a test-only dependency. No production project references it.
- The coverage targets in AGENTS.md §8 are read from its report. A coverage report is evidence for the targets, not a replacement for the tests AGENTS.md §8 requires: a high number on untested edge cases still fails review.

## Consequences

- **Pros**
  - One extra argument on a command the project already uses; no change to how projects build.
  - The same command works locally and, from M7, in CI.
- **Costs**
  - One more package to keep current in `Directory.Packages.props`.
  - Coverage output lands per test project, so a combined figure needs an extra step when it's wanted.
- **Rejected alternatives**
  - **`coverlet.msbuild`.** It hooks into the build through MSBuild properties, which changes how every test project builds. The collector leaves the build alone.
  - **No measurement; review by eye.** The AGENTS.md §8 targets would be unfalsifiable.
