using Xunit;

namespace Credere.Integration.Tests;

public class SmokeTests
{
    [Fact]
    public void Integration_test_project_runs()
    {
        // End-to-end tests with Testcontainers land in M2.
        Assert.True(true);
    }
}
