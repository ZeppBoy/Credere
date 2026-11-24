using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using NodaTime;
using Xunit;

namespace Credere.Core.Tests.Schedules;

/// <summary>Tests <see cref="ScheduleRow"/> value equality (docs/domain/schedules.md, "Public contract").</summary>
public sealed class ScheduleRowTests
{
    private static Money Eur(decimal amount) => Money.Create(amount, "EUR").Value;

    private static ScheduleRow Row(decimal interest = 10m) => new(
        1, new LocalDate(2026, 2, 28), Eur(1000m), Eur(0m), Eur(interest), Eur(400m), Eur(400m + interest), Eur(600m));

    [Fact]
    public void Equality_isByValue()
    {
        Assert.Equal(Row(), Row());
        Assert.Equal(Row().GetHashCode(), Row().GetHashCode());
        Assert.NotEqual(Row(), Row(11m));
        Assert.NotEqual(Row(), Row() with { CapitalisedInterest = Eur(1m) });
    }
}
