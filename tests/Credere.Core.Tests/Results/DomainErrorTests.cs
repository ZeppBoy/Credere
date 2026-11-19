using Credere.Core.Results;
using Xunit;

namespace Credere.Core.Tests.Results;

/// <summary>Tests the codes, messages and equality of the common <see cref="DomainError"/> records (ADR-0006).</summary>
public sealed class DomainErrorTests
{
    private static readonly Guid SampleId = new("3f2504e0-4f89-11d3-9a0c-0305e82c3301");

    /// <summary>Gets each common error with the code and message that ADR-0006 defines.</summary>
    // ADR-0006, "Result<T, E> contract": the record definitions fix every code and message template.
    public static TheoryData<DomainError, string, string> CommonErrors => new()
    {
        { new ValidationError("amount", "must be positive"), "validation", "amount: must be positive" },
        {
            new InvalidStateTransition("Active", "Disburse", "already disbursed"),
            "invalid-state-transition",
            "Disburse not allowed from Active: already disbursed"
        },
        {
            new ConcurrencyConflict("Loan", SampleId),
            "concurrency-conflict",
            "Concurrent update on Loan 3f2504e0-4f89-11d3-9a0c-0305e82c3301"
        },
        {
            new NotFound("Loan", SampleId),
            "not-found",
            "Loan 3f2504e0-4f89-11d3-9a0c-0305e82c3301 not found"
        },
        { new ConflictingTerms("term exceeds product maximum"), "conflicting-terms", "term exceeds product maximum" },
    };

    private sealed record ModuleSpecificError(string Detail) : DomainError("module-specific", Detail);

    [Theory]
    [MemberData(nameof(CommonErrors))]
    public void CommonError_hasTheCodeAndMessageDefinedByAdr0006(DomainError error, string code, string message)
    {
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void ValidationError_exposesFieldAndReason()
    {
        var error = new ValidationError("currency", "must be a three-letter code");

        Assert.Equal("currency", error.Field);
        Assert.Equal("must be a three-letter code", error.Reason);
    }

    [Fact]
    public void InvalidStateTransition_takesTheStateAsAString()
    {
        var error = new InvalidStateTransition("Closed", "Disburse", "loan is closed");

        Assert.Equal("Closed", error.FromState);
        Assert.Equal("Disburse", error.Command);
        Assert.Equal("loan is closed", error.Reason);
    }

    [Fact]
    public void ConcurrencyConflictAndNotFound_exposeAggregateAndId()
    {
        var conflict = new ConcurrencyConflict("Loan", SampleId);
        var missing = new NotFound("LoanApplication", SampleId);

        Assert.Equal(("Loan", SampleId), (conflict.Aggregate, conflict.Id));
        Assert.Equal(("LoanApplication", SampleId), (missing.Aggregate, missing.Id));
    }

    [Fact]
    public void Errors_compareByValue()
    {
        Assert.Equal(new NotFound("Loan", SampleId), new NotFound("Loan", SampleId));
        Assert.NotEqual(new NotFound("Loan", SampleId), new NotFound("Loan", Guid.Empty));
        Assert.NotEqual<DomainError>(new NotFound("Loan", SampleId), new ConcurrencyConflict("Loan", SampleId));
    }

    [Fact]
    public void ModuleSpecificError_canExtendDomainError_andTravelInAResult()
    {
        var result = Result<int, DomainError>.Failure(new ModuleSpecificError("policy declined"));

        Assert.Equal("module-specific", result.Error.Code);
        Assert.Equal("policy declined", result.Error.Message);
    }
}
