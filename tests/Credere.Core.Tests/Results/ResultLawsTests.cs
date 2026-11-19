using Credere.Core.Results;
using FsCheck.Xunit;

namespace Credere.Core.Tests.Results;

/// <summary>
/// Checks that <see cref="Result{T, E}.Map{T2}"/> and <see cref="Result{T, E}.Bind{T2}"/> obey the functor and monad
/// laws, with arbitrary successes, failures and functions.
/// </summary>
/// <remarks>
/// The laws are the standard ones for a functor and a monad (identity, composition, left identity, right identity,
/// associativity). Functions are drawn from small parameterised families, so FsCheck's input fully determines them
/// and a failing seed is reproducible.
/// </remarks>
public sealed class ResultLawsTests
{
    private static Result<int, int> Make(bool succeeds, int payload) =>
        succeeds ? Result<int, int>.Success(payload) : Result<int, int>.Failure(payload);

    private static int Affine(int x, int scale, int offset) => unchecked((x * scale) + offset);

    // A bind step that fails for multiples of three (carrying the input as the error) and otherwise succeeds.
    private static Result<int, int> Step(int x, int offset) =>
        x % 3 == 0 ? Result<int, int>.Failure(x) : Result<int, int>.Success(unchecked(x + offset));

    [Property]
    public bool Map_identity_leavesTheResultUnchanged(bool succeeds, int payload)
    {
        var m = Make(succeeds, payload);

        return m.Map(x => x) == m;
    }

    [Property]
    public bool Map_composition_equalsMappingTheComposedFunction(
        bool succeeds, int payload, int scale1, int offset1, int scale2, int offset2)
    {
        var m = Make(succeeds, payload);

        return m.Map(x => Affine(x, scale1, offset1)).Map(x => Affine(x, scale2, offset2))
            == m.Map(x => Affine(Affine(x, scale1, offset1), scale2, offset2));
    }

    [Property]
    public bool Bind_leftIdentity_successThenBindEqualsApplyingTheFunction(int a, int offset)
    {
        return Result<int, int>.Success(a).Bind(x => Step(x, offset)) == Step(a, offset);
    }

    [Property]
    public bool Bind_rightIdentity_bindingSuccessLeavesTheResultUnchanged(bool succeeds, int payload)
    {
        var m = Make(succeeds, payload);

        return m.Bind(Result<int, int>.Success) == m;
    }

    [Property]
    public bool Bind_associativity_groupingDoesNotMatter(bool succeeds, int payload, int offset1, int offset2)
    {
        var m = Make(succeeds, payload);

        return m.Bind(x => Step(x, offset1)).Bind(x => Step(x, offset2))
            == m.Bind(x => Step(x, offset1).Bind(y => Step(y, offset2)));
    }

    [Property]
    public bool Map_equalsBindOfSuccess(bool succeeds, int payload, int scale, int offset)
    {
        var m = Make(succeeds, payload);

        return m.Map(x => Affine(x, scale, offset))
            == m.Bind(x => Result<int, int>.Success(Affine(x, scale, offset)));
    }

    [Property]
    public bool Failure_shortCircuits_neverCallsTheFunction(int error)
    {
        var calls = 0;
        var m = Result<int, int>.Failure(error);

        var mapped = m.Map(x =>
        {
            calls++;
            return x;
        });
        var bound = m.Bind(x =>
        {
            calls++;
            return Result<int, int>.Success(x);
        });

        return calls == 0 && mapped.Error == error && bound.Error == error;
    }

    [Property]
    public bool Success_callsTheFunctionExactlyOnce(int payload)
    {
        var calls = 0;
        var m = Result<int, int>.Success(payload);

        m.Map(x =>
        {
            calls++;
            return x;
        });
        m.Bind(x =>
        {
            calls++;
            return Result<int, int>.Success(x);
        });

        return calls == 2;
    }
}
