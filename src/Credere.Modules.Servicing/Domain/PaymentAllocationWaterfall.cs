using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Domain;

/// <summary>
/// Applies the owner-approved D2 waterfall in docs/domain/payment-allocation.md, "Formal rules"
/// and reference cases PA-01 through PA-12.
/// </summary>
internal static class PaymentAllocationWaterfall
{
    /// <summary>
    /// Pays outstanding fees, past-due interest, current interest and principal in order.
    /// Any unapplied amount is returned as excess.
    /// </summary>
    /// <remarks>The aggregate validates payment eligibility before calling this method.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">A payment or balance is negative.</exception>
    /// <exception cref="InvalidOperationException">An amount is in a different currency.</exception>
    /// <exception cref="OverflowException">A difference cannot be represented as an exact amount.</exception>
    public static PaymentAllocation Allocate(
        Money payment,
        Money feesOutstanding,
        Money pastDueInterest,
        Money currentInterest,
        Money remainingPrincipal)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentOutOfRangeException.ThrowIfNegative(payment.Amount, nameof(payment));

        var feesAllocated = AllocateUpTo(payment, feesOutstanding, nameof(feesOutstanding));
        var available = SubtractExactly(payment, feesAllocated);
        var pastDueInterestAllocated = AllocateUpTo(available, pastDueInterest, nameof(pastDueInterest));
        available = SubtractExactly(available, pastDueInterestAllocated);
        var currentInterestAllocated = AllocateUpTo(available, currentInterest, nameof(currentInterest));
        available = SubtractExactly(available, currentInterestAllocated);
        var principalAllocated = AllocateUpTo(available, remainingPrincipal, nameof(remainingPrincipal));

        return new PaymentAllocation(
            feesAllocated,
            pastDueInterestAllocated,
            currentInterestAllocated,
            principalAllocated,
            SubtractExactly(available, principalAllocated),
            SubtractExactly(feesOutstanding, feesAllocated),
            SubtractExactly(pastDueInterest, pastDueInterestAllocated),
            SubtractExactly(currentInterest, currentInterestAllocated),
            SubtractExactly(remainingPrincipal, principalAllocated));
    }

    private static Money AllocateUpTo(Money available, Money outstanding, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(outstanding, parameterName);
        ArgumentOutOfRangeException.ThrowIfNegative(outstanding.Amount, parameterName);
        return available <= outstanding ? available : outstanding;
    }

    private static Money SubtractExactly(Money balance, Money allocated)
    {
        var remainder = balance - allocated;

        // Decimal can drop cents near its precision limit. For example, subtracting one cent
        // from decimal.MaxValue yields a remainder that must end in .99. Compare the fractional
        // parts directly because adding the cent back can also lose precision.
        var expectedFraction = (balance.Amount % 1m - allocated.Amount % 1m + 1m) % 1m;
        if (remainder.Amount % 1m != expectedFraction)
        {
            throw new OverflowException("The allocation difference cannot be represented exactly.");
        }

        return remainder;
    }
}
