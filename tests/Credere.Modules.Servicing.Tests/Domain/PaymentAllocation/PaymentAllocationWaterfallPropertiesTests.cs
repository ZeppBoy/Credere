using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Domain;
using FsCheck;
using FsCheck.Xunit;
using Allocation = Credere.Modules.Servicing.Contracts.PaymentAllocation;

namespace Credere.Modules.Servicing.Tests.Domain.PaymentAllocation;

public sealed class PaymentAllocationWaterfallPropertiesTests
{
    // D2 and docs/domain/payment-allocation.md, "Property-based invariants".
    // Inputs are whole cents, so the generated arithmetic is exact.
    [Property(MaxTest = 500)]
    public bool Allocate_AnyPayment_ConservesPaymentAndEveryBalance(
        NonNegativeInt payment, NonNegativeInt fees, NonNegativeInt pastDue, NonNegativeInt current, NonNegativeInt principal)
    {
        var result = Allocate(payment.Get, fees.Get, pastDue.Get, current.Get, principal.Get);

        return result.FeesAllocated + result.PastDueInterestAllocated + result.CurrentInterestAllocated
                + result.PrincipalAllocated + result.Excess == Euro(payment.Get)
            && ConservesBalance(result.FeesAllocated, result.NewFeesOutstanding, fees.Get)
            && ConservesBalance(result.PastDueInterestAllocated, result.NewPastDueInterest, pastDue.Get)
            && ConservesBalance(result.CurrentInterestAllocated, result.NewCurrentInterest, current.Get)
            && ConservesBalance(result.PrincipalAllocated, result.NewRemainingPrincipal, principal.Get)
            && result.Excess.Amount >= 0m;
    }

    [Property(MaxTest = 500)]
    public bool Allocate_AnyPayment_FillsHigherPriorityBalancesFirst(
        NonNegativeInt payment, NonNegativeInt fees, NonNegativeInt pastDue, NonNegativeInt current, NonNegativeInt principal)
    {
        var result = Allocate(payment.Get, fees.Get, pastDue.Get, current.Get, principal.Get);

        return (result.NewFeesOutstanding.Amount == 0m
                || (result.PastDueInterestAllocated + result.CurrentInterestAllocated + result.PrincipalAllocated).Amount == 0m)
            && (result.NewPastDueInterest.Amount == 0m
                || (result.CurrentInterestAllocated + result.PrincipalAllocated).Amount == 0m)
            && (result.NewCurrentInterest.Amount == 0m || result.PrincipalAllocated.Amount == 0m);
    }

    [Property(MaxTest = 500)]
    public bool Allocate_Overpayment_ClearsEveryBalanceBeforeReturningExcess(
        PositiveInt overpayment, NonNegativeInt fees, NonNegativeInt pastDue, NonNegativeInt current, NonNegativeInt principal)
    {
        var total = (long)fees.Get + pastDue.Get + current.Get + principal.Get;
        var result = Allocate(total + overpayment.Get, fees.Get, pastDue.Get, current.Get, principal.Get);

        return result.Excess == Euro(overpayment.Get)
            && result.NewFeesOutstanding.Amount == 0m
            && result.NewPastDueInterest.Amount == 0m
            && result.NewCurrentInterest.Amount == 0m
            && result.NewRemainingPrincipal.Amount == 0m;
    }

    [Property(MaxTest = 500)]
    public bool Allocate_Underpayment_ReturnsNoExcess(
        NonNegativeInt paymentSeed, NonNegativeInt fees, NonNegativeInt pastDue, NonNegativeInt current, PositiveInt principal)
    {
        var total = (long)fees.Get + pastDue.Get + current.Get + principal.Get;
        var payment = paymentSeed.Get % total;
        var result = Allocate(payment, fees.Get, pastDue.Get, current.Get, principal.Get);

        return result.Excess.Amount == 0m;
    }

    private static Allocation Allocate(long payment, int fees, int pastDue, int current, int principal) =>
        PaymentAllocationWaterfall.Allocate(Euro(payment), Euro(fees), Euro(pastDue), Euro(current), Euro(principal));

    private static bool ConservesBalance(Money allocated, Money remaining, int starting) =>
        allocated.Amount >= 0m && remaining.Amount >= 0m && allocated + remaining == Euro(starting);

    private static Money Euro(long cents) => Money.Create(cents / 100m, "EUR").Value;
}
