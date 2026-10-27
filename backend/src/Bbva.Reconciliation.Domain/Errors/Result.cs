using System.Diagnostics.CodeAnalysis;

namespace Bbva.Reconciliation.Domain.Errors;

/// <summary>
/// Outcome of an operation that can fail for expected reasons. Validation failures are
/// ordinary results here, not exceptions: every rule in the BBVA sandbox test plan is a
/// documented outcome, and throwing for them would make the happy path harder to read.
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly DomainError? _error;

    private Result(T value)
    {
        _value = value;
        _error = null;
    }

    private Result(DomainError error)
    {
        _value = default;
        _error = error;
    }

    [MemberNotNullWhen(false, nameof(_error))]
    public bool IsSuccess => _error is null;

    [MemberNotNullWhen(true, nameof(_error))]
    public bool IsFailure => _error is not null;

    /// <summary>The produced value. Only meaningful when <see cref="IsSuccess"/> is true.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read Value of a failed result: {_error.Message}");

    /// <summary>The failure. Only meaningful when <see cref="IsFailure"/> is true.</summary>
    public DomainError Error => _error
        ?? throw new InvalidOperationException("Cannot read Error of a successful result.");

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(DomainError error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(DomainError error) => Failure(error);

    public Result<TOut> Map<TOut>(Func<T, TOut> project) =>
        IsSuccess ? Result<TOut>.Success(project(_value!)) : Result<TOut>.Failure(_error);

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> next) =>
        IsSuccess ? next(_value!) : Result<TOut>.Failure(_error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<DomainError, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(_error);
}

public static class Result
{
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(DomainError error) => Result<T>.Failure(error);

    /// <summary>
    /// Returns the first failure among the given results, or <c>null</c> when all succeeded.
    /// Lets a caller validate several fields and surface the first problem in declared order,
    /// which is how BBVA reports validation errors: one message at a time.
    /// </summary>
    public static DomainError? FirstFailure(params ReadOnlySpan<DomainError?> errors)
    {
        foreach (var error in errors)
        {
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }
}
