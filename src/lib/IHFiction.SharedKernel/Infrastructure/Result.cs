using System.Diagnostics.CodeAnalysis;

namespace IHFiction.SharedKernel.Infrastructure;

public interface IDomainResult
{
    [MemberNotNullWhen(false, nameof(DomainError))]
    bool IsSuccess { get; init; }
    [MemberNotNullWhen(true, nameof(DomainError))]
    bool IsFailure { get; }
    DomainError? DomainError { get; init; }

    static abstract Result Failure(DomainError domainError);
    static abstract Result<TValue> Failure<TValue>(DomainError domainError);
    static abstract Result Success();
    static abstract Result<TValue> Success<TValue>(TValue value);
    bool ToBoolean();
}

public interface IDomainResult<TValue> : IDomainResult
{
    [MemberNotNullWhen(true, nameof(Value))]
    new bool IsSuccess { get; init; }

    [MemberNotNullWhen(false, nameof(Value))]
    new bool IsFailure { get; }
    TValue? Value { get; init; }
    Result<TValue> FromDomainError(DomainError domainError);
    Result<TValue> FromTValue(TValue value);
}

public record Result : IDomainResult
{
    [MemberNotNullWhen(false, nameof(DomainError))]
    public virtual bool IsSuccess { get; init; }

    [MemberNotNullWhen(true, nameof(DomainError))]
    public virtual bool IsFailure => !IsSuccess;

    public DomainError? DomainError { get; init; }

    protected Result(bool isSuccess, DomainError? error)
    {
        if ((isSuccess && error != DomainError.None) || (!isSuccess && (error is null || error == DomainError.None)))
        {
            throw new ArgumentException("Success result cannot have an error", nameof(error));
        }
        IsSuccess = isSuccess;
        DomainError = error;
    }
    public static implicit operator bool(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess;
    }

    public bool ToBoolean() => IsSuccess;

    public static Result Success() => new(true, DomainError.None);

    public static Result<TValue> Success<TValue>(TValue value) => new(true, value, DomainError.None);
    public static Result Failure(DomainError domainError) => new(false, domainError);
    public static Result<TValue> Failure<TValue>(DomainError domainError) => new(false, default, domainError);
}

[SuppressMessage("Sonar Bug", "S3453:Classes should not have only \"private\" constructors", Justification = "Must use factory methods")]
public sealed record Result<TValue> : Result, IDomainResult<TValue>
{
    [MemberNotNullWhen(true, nameof(Value))]
    public override bool IsSuccess => base.IsSuccess;

    [MemberNotNullWhen(false, nameof(Value))]
    public override bool IsFailure => base.IsFailure;
    public TValue? Value { get; init; }

    internal Result(bool isSuccess, TValue? value, DomainError? error) : base(isSuccess, error)
    {
        Value = value;
    }

    public static implicit operator Result<TValue>(TValue value) => Success(value);
    public static implicit operator Result<TValue>(DomainError error) => Failure<TValue>(error);
    public Result<TValue> FromTValue(TValue value) => Success(value);
    public Result<TValue> FromDomainError(DomainError domainError) => Failure<TValue>(domainError);
}