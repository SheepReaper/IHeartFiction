using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

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

        var domainError = ReadDomainError(ex.Response) ?? ReadDomainError(JsonSerializer.Serialize(ex.Result));
        if (domainError is not null) return domainError;

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

        return new DomainError(nameof(ApiException), BuildDescription(ex, description ?? ex.Response));
    }

    public static DomainError ToDomainError(ApiException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        if (ex is ApiException<HttpValidationProblemDetails> validation) return ToDomainError(validation);
        if (ex is ApiException<ProblemDetails> problem) return ToDomainError(problem);
        var domainError = ReadDomainError(ex.Response);
        if (domainError is not null) return domainError;
        return new DomainError(nameof(ApiException), BuildDescription(ex, ex.Response));
    }

    private static DomainError? ReadDomainError(string response)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("domainError", out var error)
                || error.ValueKind != JsonValueKind.Object
                || !error.TryGetProperty("code", out var code)
                || code.ValueKind != JsonValueKind.String
                || !error.TryGetProperty("description", out var description)
                || description.ValueKind != JsonValueKind.String) return null;
            return new DomainError(code.GetString()!, description.GetString());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildDescription(ApiException ex, string baseDescription)
    {
        ArgumentNullException.ThrowIfNull(ex);

        var status = $"HTTP {ex.StatusCode}";

        if (ex.InnerException is null)
        {
            return $"{status}: {baseDescription}";
        }

        return $"{status}: {baseDescription} :: Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
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
