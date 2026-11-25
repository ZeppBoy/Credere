using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Domain;
using FluentAssertions;
using Xunit;

namespace Credere.Modules.Servicing.Tests.Domain.PaymentAllocation;

public sealed class PaymentAllocationWaterfallTests
{
    // Owner decision D2 and docs/domain/payment-allocation.md, "Reference examples".
    public static TheoryData<string, decimal, decimal[], decimal[], decimal, decimal[]> ReferenceCases => new()
    {
        { "PA-01", 5m, StandardBalances, [5m, 0m, 0m, 0m], 0m, [5m, 25m, 15m, 1000m] },
        { "PA-02", 10m, StandardBalances, [10m, 0m, 0m, 0m], 0m, [0m, 25m, 15m, 1000m] },
        { "PA-03", 20m, StandardBalances, [10m, 10m, 0m, 0m], 0m, [0m, 15m, 15m, 1000m] },
        { "PA-04", 35m, StandardBalances, [10m, 25m, 0m, 0m], 0m, [0m, 0m, 15m, 1000m] },
        { "PA-05", 40m, StandardBalances, [10m, 25m, 5m, 0m], 0m, [0m, 0m, 10m, 1000m] },
        { "PA-06", 50m, StandardBalances, [10m, 25m, 15m, 0m], 0m, [0m, 0m, 0m, 1000m] },
        { "PA-07", 100m, StandardBalances, [10m, 25m, 15m, 50m], 0m, [0m, 0m, 0m, 950m] },
        { "PA-08", 1050m, StandardBalances, [10m, 25m, 15m, 1000m], 0m, [0m, 0m, 0m, 0m] },
        { "PA-09", 1050.01m, StandardBalances, [10m, 25m, 15m, 1000m], 0.01m, [0m, 0m, 0m, 0m] },
        { "PA-10", 0.01m, [0m, 0m, 0m, 1000m], [0m, 0m, 0m, 0.01m], 0m, [0m, 0m, 0m, 999.99m] },
        { "PA-11", 20m, [10m, 0m, 15m, 1000m], [10m, 0m, 10m, 0m], 0m, [0m, 0m, 5m, 1000m] },
        { "PA-12", 0.50m, [0m, 0m, 0.37m, 100m], [0m, 0m, 0.37m, 0.13m], 0m, [0m, 0m, 0m, 99.87m] },
    };

    private static decimal[] StandardBalances => [10m, 25m, 15m, 1000m];

    [Theory]
    [MemberData(nameof(ReferenceCases))]
    public void Allocate_ReferencePayment_MatchesD2(
        string caseId,
        decimal payment,
        decimal[] balances,
        decimal[] expectedAllocation,
        decimal expectedExcess,
        decimal[] expectedRemaining)
    {
        var result = PaymentAllocationWaterfall.Allocate(
            Euro(payment), Euro(balances[0]), Euro(balances[1]), Euro(balances[2]), Euro(balances[3]));

        Money[] allocated =
            [result.FeesAllocated, result.PastDueInterestAllocated, result.CurrentInterestAllocated, result.PrincipalAllocated];
        Money[] remaining =
            [result.NewFeesOutstanding, result.NewPastDueInterest, result.NewCurrentInterest, result.NewRemainingPrincipal];

        allocated.Should().Equal(expectedAllocation.Select(Euro), "{0} specifies each allocated balance", caseId);
        result.Excess.Should().Be(Euro(expectedExcess), "{0} specifies the unapplied amount", caseId);
        remaining.Should().Equal(expectedRemaining.Select(Euro), "{0} specifies each new balance", caseId);
    }

    [Fact]
    public void Allocate_DifferentCurrencyAfterPaymentExhausted_Throws()
    {
        var wrongCurrency = Money.Zero("USD");
        var zero = Euro(0m);

        var act = () => PaymentAllocationWaterfall.Allocate(Euro(0.01m), Euro(0.01m), zero, zero, wrongCurrency);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Allocate_NegativeOutstandingBalance_Throws()
    {
        var zero = Euro(0m);

        var act = () => PaymentAllocationWaterfall.Allocate(zero, zero, zero, Euro(-0.01m), zero);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("currentInterest");
    }

    [Fact]
    public void Allocate_UnrepresentablePrincipalRemainder_ThrowsWithoutLosingCents()
    {
        var zero = Euro(0m);

        var act = () => PaymentAllocationWaterfall.Allocate(Euro(0.01m), zero, zero, zero, Euro(decimal.MaxValue));

        act.Should().Throw<OverflowException>();
    }

    private static Money Euro(decimal amount) => Money.Create(amount, "EUR").Value;
}
