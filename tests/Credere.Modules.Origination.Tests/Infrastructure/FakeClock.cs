using NodaTime;

namespace Credere.Modules.Origination.Tests.Infrastructure;

/// <summary>A hand-written <see cref="IClock"/> whose time the test sets, so no test reads the wall clock.</summary>
internal sealed class FakeClock : IClock
{
    private Instant _now;

    internal FakeClock(Instant now)
    {
        _now = now;
    }

    public Instant GetCurrentInstant() => _now;

    internal void Set(Instant now) => _now = now;
}
