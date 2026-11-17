using Credere.Modules.Ai;
using Xunit;

namespace Credere.Modules.Ai.Tests;

public class SmokeTests
{
    [Fact]
    public void Ai_module_interface_exists()
    {
        var module = typeof(IAiModule);
        Assert.True(module.IsInterface);
    }
}
