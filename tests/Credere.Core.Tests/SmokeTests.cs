using Xunit;

namespace Credere.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Core_assembly_loads()
    {
        var marker = typeof(CoreMarker);
        Assert.NotNull(marker);
    }
}
