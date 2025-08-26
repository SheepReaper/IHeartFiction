using System.Diagnostics.CodeAnalysis;

using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.SharedWeb;

[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Extending generated partial")]
public partial class ApiException
{
    public Result<TResult> ToResult<TResult>() => ToDomainError(this);
    public Result ToResult() => ToDomainError(this);

    public DomainError ToDomainError() => ToDomainError(this);

    public static DomainError ToDomainError<TEx>(ApiException<TEx> ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        string? description = null;

        if (ex.Result is HttpValidationProblemDetails vp)
        {
            var flatErrors = string.Join("; ", vp.Errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}"));

            description = $"{vp.Title}: {vp.Detail} :: {flatErrors}";
        }
        else if (ex.Result is ProblemDetails pd)
        {
            description = $"{pd.Title}: {pd.Detail}";
        }

        return new DomainError(nameof(ApiException), $"{description ?? ex.Response}");
    }

    public static DomainError ToDomainError(ApiException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return new DomainError(nameof(ApiException), $"{ex.Response}");
    }

    public static implicit operator DomainError(ApiException ex) => ToDomainError(ex);

    public static Result<TResult> ToResult<TResult, TEx>(ApiException<TEx> ex) => ToDomainError(ex);

    public static Result<TResult> ToResult<TResult>(ApiException ex) => ToDomainError(ex);

    public static implicit operator Result(ApiException ex) => ToDomainError(ex);
}

[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Extending generated partial")]
public partial class ApiException<TResult>
{
    public DomainError ToDomainError(ApiException<TResult> ex) => ToDomainError<TResult>(ex);

    public static implicit operator DomainError(ApiException<TResult> ex) => ToDomainError<TResult>(ex);

    public Result<TResultT> ToResult<TResultT>(ApiException<TResult> ex) => ToResult<TResultT, TResult>(ex);

    public static implicit operator Result(ApiException<TResult> ex) => ToResult<TResult>((ApiException)ex);
}
