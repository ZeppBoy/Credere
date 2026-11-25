using Credere.Modules.Origination.Contracts;
using NodaTime;

namespace Credere.Modules.Origination.Domain.Events;

/// <summary>
/// The base of every domain event a <see cref="LoanApplication"/> emits. Domain events are internal to the module
/// (AGENTS.md §6): they are the append-only history of ADR-0004, and the handler maps them to the public integration
/// events of ADR-0005.
/// </summary>
/// <remarks>
/// An event carries enough data that a consumer never re-queries the aggregate. The history row's <c>CommandId</c>
/// and <c>CorrelationId</c> columns come from this base.
/// </remarks>
/// <param name="ApplicationId">The application the event belongs to, which is also its stream id.</param>
/// <param name="OccurredAt">When the aggregate handled the command, as passed in by the handler.</param>
/// <param name="CommandId">The idempotency id of the command that produced the event.</param>
/// <param name="CorrelationId">The id that correlates the event with related work.</param>
internal abstract record LoanApplicationEvent(
    LoanApplicationId ApplicationId,
    Instant OccurredAt,
    Guid CommandId,
    Guid CorrelationId);
