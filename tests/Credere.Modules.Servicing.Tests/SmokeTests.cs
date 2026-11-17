using Credere.Modules.Servicing;
using Xunit;

namespace Credere.Modules.Servicing.Tests;

public class SmokeTests
{
    [Fact]
    public void Servicing_module_interface_exists()
    {
        var module = typeof(IServicingModule);
        Assert.True(module.IsInterface);
    }
}
