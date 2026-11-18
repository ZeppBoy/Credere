---
paths:
  - "tests/**"
---

# Tests

AGENTS.md §8 holds the rules. These are the specifics of this solution.

- **Containers:** start one SQL Server or Mongo container per test collection, through an xUnit collection fixture (`IAsyncLifetime`). The SQL Server 2022 image is x64-only and runs emulated on Apple Silicon, so starting one per test makes the suite very slow.
- **Reference-case tests** are `[Theory]` tests with `TheoryData`. Cite the source (document, section, example number) per row or per data set, so a reviewer can check each value.
- **Naming:** test methods are named `Method_condition_expected`. Underscores are allowed in tests (CA1707 is suppressed there).
- **FluentAssertions** stays on 6.x, because v8 changed licence. Use only 6.x APIs.
- **No paid model calls under `tests/`.** Evals live in `Credere.Ai.Evals`, so `dotnet test` stays free and repeatable.
