using Credere.Core.Schedules;
using Xunit;

namespace Credere.Core.Tests.Schedules;

/// <summary>Tests <see cref="PaymentFrequency"/> (docs/domain/schedules.md, "Public contract").</summary>
public sealed class PaymentFrequencyTests
{
    [Fact]
    public void Values_areThePeriodsPerYear()
    {
        Assert.Equal(12, (int)PaymentFrequency.Monthly);
        Assert.Equal(4, (int)PaymentFrequency.Quarterly);
    }

    [Fact]
    public void Defined_listsExactlyMonthlyAndQuarterly()
    {
        Assert.Equal(
            [PaymentFrequency.Quarterly, PaymentFrequency.Monthly],
            Enum.GetValues<PaymentFrequency>().OrderBy(f => (int)f).ToArray());
    }
}
