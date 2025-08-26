using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.Infrastructure;

using Error = IHFiction.SharedKernel.Infrastructure.DomainError;

namespace IHFiction.FictionApi.Authors;

internal sealed record RoleRepresentation(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("clientRole")] bool ClientRole,
    [property: JsonPropertyName("containerId")] Guid Composite);

internal sealed record ClientRepresentation(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("clientId")] string? ClientId);

internal sealed class KeycloakAdminService(IOptions<KeycloakAdminClientOptions> options, IHttpClientFactory httpClientFactory, TimeProvider dt)
{
    internal static class Errors
    {
        public static readonly Error TokenFetch = new("Keycloak.TokenFetch", "Failed to fetch token from Keycloak.");
        public static readonly Error SetRealmRole = new("Keycloak.SetRealmRole", "Failed to set realm role for user.");
        public static readonly Error GetRealmRole = new("Keycloak.GetRealmRole", "Failed to get realm role from Keycloak.");
        public static readonly Error SetResourceRole = new("Keycloak.SetResourceRole", "Failed to set role for user.");
        public static readonly Error GetResourceRole = new("Keycloak.GetResourceRole", "Failed to get role from Keycloak.");
        public static readonly Error GetReource = new("Keycloak.GetResource", "Failed to get resource from Keycloak.");
    }
    private const string GrantType = "client_credentials";
    private readonly TimeSpan _tokenRefreshThreshold = options.Value.TokenRefreshThreshold;
    private readonly string _clientId = options.Value.AuthClientId;
    private readonly string _clientSecret = options.Value.AuthClientSecret;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(typeof(KeycloakAdminService).Name);
    private Uri TokenEndpoint => new(_httpClient.BaseAddress!, $"realms/{options.Value.Realm}/protocol/openid-connect/token/");
    private Uri RealmRolesEndpoint => new(_httpClient.BaseAddress!, $"admin/realms/{options.Value.Realm}/roles/");
    private Uri UsersEndpoint => new(_httpClient.BaseAddress!, $"admin/realms/{options.Value.Realm}/users/");
    private Uri ResourcesEndpoint => new(_httpClient.BaseAddress!, $"admin/realms/{options.Value.Realm}/clients/");


    private string? _token;
    private DateTime? _tokenExpiration;
    private readonly ConcurrentDictionary<(string, bool), RoleRepresentation> _roles = new();
    private readonly ConcurrentDictionary<string, ClientRepresentation> _clients = new();

    private async Task<Result<string>> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        // if token not null and not expiring within threshold, return token
        return (_token is not null && _tokenExpiration is not null && _tokenExpiration > dt.GetUtcNow().Add(_tokenRefreshThreshold))
            ? _token
            : await FetchNewAccessTokenAsync(cancellationToken);
    }

    private async Task<Result<string>> FetchNewAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = GrantType,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret
        });

        var response = await _httpClient.PostAsync(TokenEndpoint, body, cancellationToken);

        if (!response.IsSuccessStatusCode) return Errors.TokenFetch;

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var token = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);

            if (token is null) return Error.Deserialization;

            if (token.TryGetValue("access_token", out var accessToken) && token.TryGetValue("expires_in", out var expiresIn))
            {
                _tokenExpiration = dt.GetUtcNow().AddSeconds(expiresIn.GetInt32()).UtcDateTime;
                _token = accessToken.GetString();

                return _token is null ? Error.Deserialization : (Result<string>)_token;
            }

            return Error.Deserialization;
        }
        catch (Exception e) when (e is JsonException or ArgumentNullException or ArgumentException)
        {
            return Error.Deserialization;
        }
    }

    public async Task<Result> AssignRoleToUserAsync(Guid userId, string roleName, string? clientId = null, CancellationToken cancellationToken = default)
    {
        var roleResult = await GetRoleAsync(roleName, clientId, cancellationToken);

        if(roleResult.IsFailure) return roleResult.DomainError;

        ClientRepresentation? client = null;

        if (clientId is not null)
        {
            var clientResult = await GetClientAsync(clientId, cancellationToken);

            if (clientResult.IsFailure) return clientResult.DomainError;

            client = clientResult.Value;
        }

        return roleResult.IsFailure ? roleResult.DomainError : await AssignRoleToUserAsync(userId, roleResult.Value, client, cancellationToken);
    }

    public async Task<Result> AssignRoleToUserAsync(Guid userId, RoleRepresentation role, ClientRepresentation? resource = null, CancellationToken cancellationToken = default)
    {
        var tokenResult = await GetAccessTokenAsync(cancellationToken);

        if (tokenResult.IsFailure) return tokenResult.DomainError;

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);

        try
        {
            var content = JsonSerializer.Serialize(new[] { role });

            using var body = new StringContent(
                content,
                Encoding.UTF8,
                MediaTypeNames.Application.Json);

            var response = await _httpClient.PostAsync(resource is null
                ? new Uri(UsersEndpoint, $"{userId}/role-mappings/realm")
                : new Uri(UsersEndpoint, $"{userId}/role-mappings/clients/{resource.Id}"), body, cancellationToken);

            var error = resource is null ? Errors.SetRealmRole : Errors.SetResourceRole;

            return response.IsSuccessStatusCode ? Result.Success() : error;
        }
        catch (NotSupportedException)
        {
            return Error.Serialization;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1172:Unused method parameters should be removed", Justification = "<Pending>")]
    private async Task<Result<ClientRepresentation>> FetchClientAsync(string clientId, CancellationToken cancellationToken)
    {
        var tokenResult = await GetAccessTokenAsync(cancellationToken);

        if (tokenResult.IsFailure) return tokenResult.DomainError;

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);

        var response = await _httpClient.GetAsync(new Uri(ResourcesEndpoint, $"?clientId={clientId}"), cancellationToken);

        if (!response.IsSuccessStatusCode) return Errors.GetReource;

        var client = await response.Content.ReadFromJsonAsync<ClientRepresentation[]>(cancellationToken: cancellationToken);

        return client is null ? Error.Deserialization : client[0];
    }

    private async Task<Result<RoleRepresentation>> FetchRoleAsync(string roleName, string? clientId, CancellationToken cancellationToken)
    {
        var tokenResult = await GetAccessTokenAsync(cancellationToken);

        if (tokenResult.IsFailure) return tokenResult.DomainError;

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);

        ClientRepresentation? client = null;

        if (clientId is not null)
        {
            var clientResult = await GetClientAsync(clientId, cancellationToken);

            if (clientResult.IsFailure) return clientResult.DomainError;

            client = clientResult.Value;
        }

        var roleResponse = await _httpClient.GetAsync(client is null
            ? new Uri(RealmRolesEndpoint, roleName)
            : new Uri(ResourcesEndpoint, $"{client.Id}/roles/{roleName}"), cancellationToken);

        if (!roleResponse.IsSuccessStatusCode) return clientId is null ? Errors.GetRealmRole : Errors.GetResourceRole;

        var response = await roleResponse.Content.ReadFromJsonAsync<RoleRepresentation>(cancellationToken: cancellationToken);

        return response is null ? Error.Deserialization : response;
    }

    private async Task<Result<RoleRepresentation>> GetRoleAsync(string roleName, string? clientId, CancellationToken cancellationToken)
    {
        if (_roles.TryGetValue((roleName, clientId is null), out var role)) return role;

        var result = await FetchRoleAsync(roleName, clientId, cancellationToken);

        if (!result.IsSuccess) return result.DomainError;

        _roles.TryAdd((roleName, clientId is null), result.Value);

        return result;
    }

    private async Task<Result<ClientRepresentation>> GetClientAsync(string clientId, CancellationToken cancellationToken)
    {
        if (_clients.TryGetValue(clientId, out var client)) return client;

        var result = await FetchClientAsync(clientId, cancellationToken);

        if (!result.IsSuccess) return result.DomainError;

        _clients.TryAdd(clientId, result.Value);

        return result;
    }
}