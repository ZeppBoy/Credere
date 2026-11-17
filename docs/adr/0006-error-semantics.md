# ADR-0006: Error semantics — `Result<T, DomainError>` for expected failures; exceptions for bugs only

**Status:** Accepted 2026-11-17.

## Context

Promissio's aggregates mixed error semantics: some commands returned `Result<T>`, others threw `InvalidStateTransitionException`. Callers had to know which was which. The AGENTS.md said "domain operations that can fail return Result<T>" but the actual code violated this routinely — most notably `Loan.Activate`, `Loan.ApplyAging`, `Loan.WriteOff`, etc. all threw on invalid state.

An invalid state transition in a long-running business workflow is an **expected** failure, not a programmer error. A duplicate payment submission is an expected failure. A currency mismatch is an expected failure. These should be visible in handler return types, not hidden in the exception path.

## Decision

Credere uses a uniform rule:

1. **Expected business failures** — invalid state transition, validation error, concurrency conflict, currency mismatch, missing aggregate, etc. — return `Result<T, DomainError>.Failure(...)`. **Every** command and query handler's signature reflects this. **Aggregates themselves return `Result<T, DomainError>`** from every command method; they never throw a business exception.
2. **Programmer / configuration errors** — null arguments from DI, missing environment variables, unreachable code paths — throw exceptions. These are bugs; the process should fail loudly in dev and be captured by OTEL in prod.

### `Result<T, E>` contract

```csharp
public readonly record struct Result<T, E>
{
    public bool IsSuccess { get; }
    public T Value { get; }           // undefined when !IsSuccess
    public E Error { get; }           // undefined when IsSuccess

    public static Result<T, E> Success(T value);
    public static Result<T, E> Failure(E error);

    public Result<T2, E> Map<T2>(Func<T, T2> f);
    public Result<T2, E> Bind<T2>(Func<T, Result<T2, E>> f);
}
```

`DomainError` is a discriminated-union-style record hierarchy:

```csharp
public abstract record DomainError(string Code, string Message);

public sealed record ValidationError(string Field, string Message)
    : DomainError("validation", $"{Field}: {Message}");

public sealed record InvalidStateTransition(object FromState, string Command, string Reason)
    : DomainError("invalid-state-transition", $"{Command} not allowed from {FromState}: {Reason}");

public sealed record ConcurrencyConflict(string Aggregate, Guid Id)
    : DomainError("concurrency-conflict", $"Concurrent update on {Aggregate} {Id}");

public sealed record NotFound(string Aggregate, Guid Id)
    : DomainError("not-found", $"{Aggregate} {Id} not found");

public sealed record ConflictingTerms(string Reason)
    : DomainError("conflicting-terms", Reason);
```

New error types extend `DomainError` within the module's namespace.

### Transport mapping

- API host maps `Result<T, DomainError>.Failure` to HTTP responses via a central mapper:
  - `ValidationError` → 400 with problem-details JSON (one field per `ValidationError` error).
  - `NotFound` → 404.
  - `ConcurrencyConflict` → 409 with retry guidance.
  - `InvalidStateTransition`, `ConflictingTerms` → 422 with the `Code` and `Message`.
  - Any other `DomainError` → 500 with a logged correlation id; this is a signal we forgot to map.

- MCP host returns a structured `{ "ok": false, "error": { "code": ..., "message": ... } }` for failures.

- Agent outputs receive the `DomainError` as context for the next model turn; agents don't throw on tool errors either.

## Consequences

- **Pros**
  - Error handling is uniform across the entire codebase: one `Result<T, DomainError>` return type at every public handler.
  - Callers see failure modes in the type system; nothing hidden in exception paths.
  - Mapping to HTTP / MCP responses is centralised.
  - Aggregates become simpler — no try/catch around state-transition code.

- **Costs**
  - More verbose than exception throwing in some simple paths (`if (!result.IsSuccess) return result.Error.ToProblem();`).
  - Requires discipline: a tempting `throw new ArgumentException(...)` on a user-visible failure is a style break and will be caught in review.

- **Rejected alternatives**
  - **Exceptions for everything:** the Promissio status quo; already judged unsatisfactory.
  - **MediatR pipeline behavior that catches domain exceptions and converts them:** hides semantics, breaks referential transparency of handlers.
  - **`OneOf<T, Err1, Err2, ...>`:** more powerful but noisy; `Result<T, DomainError>` with a hierarchy of errors is sufficient and reads naturally.
