using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Modules.Servicing.Application.Commands.AccrueInterest;

/// <summary>Describes the amount and progress of an accrual operation.</summary>
/// <param name="AccruedThroughDate">The exclusive end date of posted interest.</param>
/// <param name="Accrued">The additional interest posted by this operation.</param>
/// <param name="Changed">Whether the operation changed the loan.</param>
public sealed record AccrualResult(LocalDate AccruedThroughDate, Money Accrued, bool Changed);
