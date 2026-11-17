using Credere.Modules.Origination;
using Xunit;

namespace Credere.Modules.Origination.Tests;

public class SmokeTests
{
    [Fact]
    public void Origination_module_interface_exists()
    {
        var module = typeof(IOriginationModule);
        Assert.True(module.IsInterface);
    }
}
