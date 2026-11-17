using Credere.Modules.Documents;
using Xunit;

namespace Credere.Modules.Documents.Tests;

public class SmokeTests
{
    [Fact]
    public void Documents_module_interface_exists()
    {
        var module = typeof(IDocumentsModule);
        Assert.True(module.IsInterface);
    }
}
