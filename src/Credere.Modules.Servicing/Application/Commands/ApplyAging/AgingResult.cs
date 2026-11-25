using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Application.Commands.ApplyAging;

/// <summary>Describes the current loan state and days past due after applying aging.</summary>
/// <param name="State">The current or resulting state.</param>
/// <param name="DaysPastDue">The days past due.</param>
/// <param name="Changed">Whether the operation changed the loan.</param>
public sealed record AgingResult(LoanState State, int? DaysPastDue, bool Changed);
