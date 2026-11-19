namespace Credere.Core.Results;

/// <summary>
/// The outcome of an operation that can fail in an expected way: either a value of <typeparamref name="T"/> or an
/// error of type <typeparamref name="E"/> (ADR-0006, placed in Core by ADR-0009).
/// </summary>
/// <remarks>
/// Read <see cref="Value"/> only after checking <see cref="IsSuccess"/>, and <see cref="Error"/> only on a failure.
/// Reading the wrong side, or reading a <c>default</c> instance, is a programmer error and throws
/// <see cref="InvalidOperationException"/>. Instances are created through <see cref="Success"/> and
/// <see cref="Failure"/>.
/// </remarks>
/// <typeparam name="T">The type of the success value.</typeparam>
/// <typeparam name="E">The type of the error. Failures never carry a <see langword="null"/> error.</typeparam>
// ADR-0006 specifies the type parameter name E (CA1715) and the factories Result<T, E>.Success and Failure (CA1000).
#pragma warning disable CA1715, CA1000
public readonly struct Result<T, E> : IEquatable<Result<T, E>>
{
    private readonly bool _isInitialized;
    private readonly bool _isSuccess;
    private readonly T? _value;
    private readonly E? _error;

    private Result(bool isSuccess, T? value, E? error)
    {
        _isInitialized = true;
        _isSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary>Gets a value that indicates whether the operation succeeded.</summary>
    /// <value><see langword="false"/> for a failure and for a <c>default</c> instance.</value>
    public bool IsSuccess => _isInitialized && _isSuccess;

    /// <summary>Gets the success value.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure or a <c>default</c> instance.</exception>
    public T Value
    {
        get
        {
            if (!IsSuccess)
            {
                throw new InvalidOperationException(
                    "Value was read from a result that is not a success. Check IsSuccess first.");
            }

            // _value is assigned by Success whenever IsSuccess is true; it can be null only when T allows it.
            return _value!;
        }
    }

    /// <summary>Gets the error.</summary>
    /// <exception cref="InvalidOperationException">The result is a success or a <c>default</c> instance.</exception>
    public E Error
    {
        get
        {
            if (!_isInitialized || _isSuccess)
            {
                throw new InvalidOperationException(
                    "Error was read from a result that is not a failure. Check IsSuccess first.");
            }

            // _error is assigned by Failure, which rejects null.
            return _error!;
        }
    }

    /// <summary>Creates a successful result.</summary>
    /// <param name="value">The success value.</param>
    /// <returns>A result whose <see cref="IsSuccess"/> is <see langword="true"/>.</returns>
    public static Result<T, E> Success(T value) => new(true, value, default);

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">The error. It must not be <see langword="null"/>.</param>
    /// <returns>A result whose <see cref="IsSuccess"/> is <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static Result<T, E> Failure(E error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, default, error);
    }

    /// <summary>Applies <paramref name="f"/> to the success value, or passes a failure through unchanged.</summary>
    /// <typeparam name="T2">The type of the mapped value.</typeparam>
    /// <param name="f">The mapping function. It is not called for a failure.</param>
    /// <returns>A success holding <c>f(Value)</c>, or the same error.</returns>
    /// <exception cref="InvalidOperationException">This is a <c>default</c> instance.</exception>
    public Result<T2, E> Map<T2>(Func<T, T2> f)
    {
        ArgumentNullException.ThrowIfNull(f);
        return IsSuccess ? Result<T2, E>.Success(f(Value)) : Result<T2, E>.Failure(Error);
    }

    /// <summary>
    /// Chains an operation that can itself fail: applies <paramref name="f"/> to the success value and returns its
    /// result, or passes a failure through unchanged.
    /// </summary>
    /// <typeparam name="T2">The type of the value the chained operation produces.</typeparam>
    /// <param name="f">The chained operation. It is not called for a failure.</param>
    /// <returns>The result of <c>f(Value)</c>, or the same error.</returns>
    /// <exception cref="InvalidOperationException">This is a <c>default</c> instance.</exception>
    public Result<T2, E> Bind<T2>(Func<T, Result<T2, E>> f)
    {
        ArgumentNullException.ThrowIfNull(f);
        return IsSuccess ? f(Value) : Result<T2, E>.Failure(Error);
    }

    /// <inheritdoc />
    public bool Equals(Result<T, E> other) =>
        _isInitialized == other._isInitialized
        && _isSuccess == other._isSuccess
        && EqualityComparer<T?>.Default.Equals(_value, other._value)
        && EqualityComparer<E?>.Default.Equals(_error, other._error);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T, E> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_isInitialized, _isSuccess, _value, _error);

    /// <summary>Determines whether two results are equal: both successes with equal values, or both failures with equal errors.</summary>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns><see langword="true"/> when the results are equal.</returns>
    public static bool operator ==(Result<T, E> left, Result<T, E> right) => left.Equals(right);

    /// <summary>Determines whether two results differ.</summary>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns><see langword="true"/> when the results are not equal.</returns>
    public static bool operator !=(Result<T, E> left, Result<T, E> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() =>
        !_isInitialized ? "Result(uninitialized)" : _isSuccess ? $"Success({_value})" : $"Failure({_error})";
}
#pragma warning restore CA1715, CA1000
