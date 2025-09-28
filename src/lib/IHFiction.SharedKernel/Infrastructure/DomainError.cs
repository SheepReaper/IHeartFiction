namespace IHFiction.SharedKernel.Infrastructure;

public sealed record DomainError(string Code, string? Description = null)
{
    public static readonly DomainError None = new(string.Empty);
    public static readonly DomainError Deserialization = new("General.Deserialization", "Failed to deserialize response from Keycloak.");
    public static readonly DomainError Serialization = new("General.Serialization", "Failed to serialize request body.");
    public static readonly DomainError NotFound = new("General.NotFound", "The requested resource was not found.");
    public static readonly DomainError InvalidUlid = new("Parsing.InvalidUlid", "The provided ID is not a valid ULID.");
    public static readonly DomainError InvalidObjectId = new("Parsing.InvalidObjectId", "The provided ID is not a valid ObjectId.");
    public static readonly DomainError EmptyUlid = new("Parsing.EmptyUlid", "The provided ULID cannot be empty.");
    public static readonly DomainError EmptyObjectId = new("Parsing.EmptyObjectId", "The provided ObjectId cannot be empty.");

    public static implicit operator Result(DomainError error) => error == None ? Result.Success() : Result.Failure(error);

    public Result ToResult()
    {
        return this == None ? Result.Success() : Result.Failure(this);
    }
}
