using Credere.Modules.Reporting;
using Xunit;

namespace Credere.Modules.Reporting.Tests;

public class SmokeTests
{
    [Fact]
    public void Reporting_module_interface_exists()
    {
        var module = typeof(IReportingModule);
        Assert.True(module.IsInterface);
    }
}
