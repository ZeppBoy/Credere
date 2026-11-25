using Credere.Core.ValueObjects;

namespace Credere.Modules.Servicing.Contracts;

/// <summary>Describes the allocation of a payment and the balances that remain.</summary>
/// <param name="FeesAllocated">The amount applied to fees.</param>
/// <param name="PastDueInterestAllocated">The amount applied to past-due interest.</param>
/// <param name="CurrentInterestAllocated">The amount applied to current interest.</param>
/// <param name="PrincipalAllocated">The amount applied to principal.</param>
/// <param name="Excess">The part of the payment recorded for return.</param>
/// <param name="NewFeesOutstanding">The fees remaining after the payment.</param>
/// <param name="NewPastDueInterest">The past-due interest remaining after the payment.</param>
/// <param name="NewCurrentInterest">The current interest remaining after the payment.</param>
/// <param name="NewRemainingPrincipal">The principal remaining after the payment.</param>
public sealed record PaymentAllocation(
    Money FeesAllocated,
    Money PastDueInterestAllocated,
    Money CurrentInterestAllocated,
    Money PrincipalAllocated,
    Money Excess,
    Money NewFeesOutstanding,
    Money NewPastDueInterest,
    Money NewCurrentInterest,
    Money NewRemainingPrincipal);
