using Credere.Core.Results;
using Credere.Modules.Origination.Contracts;
using Xunit;

namespace Credere.Modules.Origination.Tests.Contracts;

public sealed class ProductCodeTests
{
    [Theory]
    [InlineData("A")]
    [InlineData("TERM_01-FIXED")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ012345")]
    public void Create_ValidCode_PreservesItsValue(string value)
    {
        var result = ProductCode.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456")]
    [InlineData("term")]
    [InlineData(" TERM")]
    [InlineData("TERM/01")]
    [InlineData("TÉRM")]
    public void Create_InvalidCode_ReturnsValidationError(string value)
    {
        var result = ProductCode.Create(value);

        Assert.False(result.IsSuccess);
        Assert.Equal("value", Assert.IsType<ValidationError>(result.Error).Field);
    }
}
