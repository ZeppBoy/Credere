using Credere.Core.Results;
using Xunit;

namespace Credere.Core.Tests.Results;

/// <summary>Tests the construction, accessors, equality and guard behaviour of <see cref="Result{T, E}"/>.</summary>
public sealed class ResultTests
{
    private static readonly ValidationError SampleError = new("amount", "must be positive");

    [Fact]
    public void Success_holdsValue_andIsSuccess()
    {
        var result = Result<int, DomainError>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_holdsError_andIsNotSuccess()
    {
        var result = Result<int, DomainError>.Failure(SampleError);

        Assert.False(result.IsSuccess);
        Assert.Same(SampleError, result.Error);
    }

    [Fact]
    public void Value_onFailure_throwsInvalidOperation()
    {
        var result = Result<int, DomainError>.Failure(SampleError);

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Error_onSuccess_throwsInvalidOperation()
    {
        var result = Result<int, DomainError>.Success(1);

        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Default_isNotSuccess_andNeitherAccessorWorks()
    {
        var result = default(Result<int, DomainError>);

        Assert.False(result.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Default_mapAndBind_throwInsteadOfInventingAnError()
    {
        var result = default(Result<int, DomainError>);

        Assert.Throws<InvalidOperationException>(() => result.Map(x => x));
        Assert.Throws<InvalidOperationException>(() => result.Bind(x => Result<int, DomainError>.Success(x)));
    }

    [Fact]
    public void Failure_withNullError_throwsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Result<int, DomainError>.Failure(null!));
    }

    [Fact]
    public void Success_withNullValue_isAllowedWhenTheTypeAllowsIt()
    {
        var result = Result<string?, DomainError>.Success(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Map_nullFunction_throwsArgumentNull()
    {
        var result = Result<int, DomainError>.Success(1);

        Assert.Throws<ArgumentNullException>(() => result.Map<int>(null!));
    }

    [Fact]
    public void Bind_nullFunction_throwsArgumentNull()
    {
        var result = Result<int, DomainError>.Success(1);

        Assert.Throws<ArgumentNullException>(() => result.Bind<int>(null!));
    }

    [Fact]
    public void Map_onSuccess_appliesFunction_andKeepsNewValueType()
    {
        var result = Result<int, DomainError>.Success(21).Map(x => (x * 2).ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.True(result.IsSuccess);
        Assert.Equal("42", result.Value);
    }

    [Fact]
    public void Map_onFailure_keepsErrorAndSkipsFunction()
    {
        var calls = 0;
        var result = Result<int, DomainError>.Failure(SampleError).Map(x =>
        {
            calls++;
            return x;
        });

        Assert.Equal(0, calls);
        Assert.Same(SampleError, result.Error);
    }

    [Fact]
    public void Bind_onSuccess_returnsTheFunctionResult_includingItsFailure()
    {
        var other = new NotFound("Loan", Guid.Empty);
        var result = Result<int, DomainError>.Success(1).Bind(_ => Result<int, DomainError>.Failure(other));

        Assert.Same(other, result.Error);
    }

    [Fact]
    public void Bind_onFailure_keepsErrorAndSkipsFunction()
    {
        var calls = 0;
        var result = Result<int, DomainError>.Failure(SampleError).Bind(x =>
        {
            calls++;
            return Result<int, DomainError>.Success(x);
        });

        Assert.Equal(0, calls);
        Assert.Same(SampleError, result.Error);
    }

    [Fact]
    public void Equals_successesCompareByValue_failuresByError()
    {
        Assert.Equal(Result<int, string>.Success(1), Result<int, string>.Success(1));
        Assert.NotEqual(Result<int, string>.Success(1), Result<int, string>.Success(2));
        Assert.Equal(Result<int, string>.Failure("a"), Result<int, string>.Failure("a"));
        Assert.NotEqual(Result<int, string>.Failure("a"), Result<int, string>.Failure("b"));
    }

    [Fact]
    public void Equals_successAndFailureAreNeverEqual_evenWhenPayloadsMatch()
    {
        var success = Result<int, int>.Success(1);
        var failure = Result<int, int>.Failure(1);

        Assert.NotEqual(success, failure);
        Assert.True(success != failure);
        Assert.False(success == failure);
    }

    [Fact]
    public void Equals_defaultDiffersFromEverythingElse()
    {
        Assert.NotEqual(default, Result<int, int>.Success(0));
        Assert.NotEqual(default, Result<int, int>.Failure(0));
        Assert.Equal(default, default(Result<int, int>));
    }

    [Fact]
    public void Equals_boxedObject_matchesOnlyResultsOfTheSameType()
    {
        var result = Result<int, int>.Success(1);

        Assert.True(result.Equals((object)Result<int, int>.Success(1)));
        Assert.False(result.Equals((object)Result<long, int>.Success(1)));
        Assert.False(result.Equals(null));
    }

    [Fact]
    public void GetHashCode_equalResultsHaveEqualHashCodes()
    {
        Assert.Equal(
            Result<int, string>.Success(7).GetHashCode(),
            Result<int, string>.Success(7).GetHashCode());
        Assert.Equal(
            Result<int, string>.Failure("x").GetHashCode(),
            Result<int, string>.Failure("x").GetHashCode());
    }

    [Fact]
    public void ToString_showsWhichSideItIs()
    {
        Assert.Equal("Success(5)", Result<int, string>.Success(5).ToString());
        Assert.Equal("Failure(boom)", Result<int, string>.Failure("boom").ToString());
        Assert.Equal("Result(uninitialized)", default(Result<int, string>).ToString());
    }

    [Fact]
    public void Unit_resultCarriesNoData_andAllUnitsAreEqual()
    {
        var result = Result<Unit, DomainError>.Success(Unit.Value);

        Assert.True(result.IsSuccess);
        Assert.Equal(Unit.Value, default(Unit));
        Assert.Equal("()", Unit.Value.ToString());
    }
}
