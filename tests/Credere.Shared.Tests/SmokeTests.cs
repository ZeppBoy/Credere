using Credere.Shared;
using Xunit;

namespace Credere.Shared.Tests;

public class SmokeTests
{
    [Fact]
    public void Shared_assembly_loads()
    {
        var marker = typeof(SharedMarker);
        Assert.NotNull(marker);
    }
}
