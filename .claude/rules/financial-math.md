---
paths:
  - "src/Credere.Core/**"
  - "tests/Credere.Core.Tests/**"
  - "**/*Interest*"
  - "**/*DayCount*"
  - "**/*Aprc*"
  - "**/*Schedule*"
  - "**/*Allocation*"
  - "**/*Ifrs9*"
  - "docs/domain/**"
---

# Financial math and value objects

AGENTS.md §7 holds the rules. This file adds the order of work and the traps specific to Credere's types.

## Adding or changing a calculation

The order matters here: reference values written after the code tend to be fitted to the code.

1. Find the primary source: ISDA definitions, EU directive text, the IFRS 9 standard, or ECB illustrative examples. Blogs and secondary write-ups don't count as references. If no primary source exists, ask the owner.
2. Write the reference test cases from that source before implementing.
3. Implement, citing the source in the XML doc. Add FsCheck properties for the invariants.
4. Update the matching `docs/domain/*.md` page. Ask for owner review, and record it as **Reviewed** in `docs/status.md`.

Once Stryker.NET is configured (M1), the change should also keep `Credere.Core` at ≥80% mutation score.

## Refactoring a value object

The existing tests are the contract. A failing property-based test means an invariant broke, not that the test is flaky. Run the test projects of every module that uses the value object. If an ADR described the old design, add a superseding ADR.

## Traps in Credere's own types

- `Percentage` stores percent units: `new Percentage(5.25m)` is 5.25%. Passing a fraction such as `0.0525m` to the constructor is a 100× error; use `Percentage.FromFraction`.
- `Money` rounds when it is created, so creating a `Money` inside a loop (per day, per period) rounds on every iteration. Do that only where a domain doc says the rounding belongs there.
- `Math.Round(x, 2)` without a `MidpointRounding` argument hides the rounding choice; always pass the mode.
- `rates.md` sketches positional records such as `FixedRate(Percentage NominalRate, …)`. Implement them with get-only properties and `Create(...)`, so a `with` expression can't bypass the `NominalRate ≤ 100%` invariant.
