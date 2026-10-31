using System.Text.Json;

using FluentAssertions;

using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IHFiction.UnitTests.FictionApi;

public sealed class OpenApiRateLimitingTests
{
    [Theory]
    [InlineData("v1")]
    [InlineData("cloudflare")]
    public async Task Documents_DescribeGlobalAndExplicitLimits_AndRespectOptOut(string document)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["AllowedHosts"] = "127.0.0.1";
        builder.Services.AddOptions<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>();
        builder.Services.Configure<BaseUrlOptions>(options => options.BaseUrl = new Uri("https://api.example.test"));
        builder.Services.AddOpenApiWithAuth();
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapOpenApi();
        app.MapGet("/global", () => TypedResults.Ok()).ApplyDefaultRateLimitPolicy();
        app.MapPost("/explicit", () => TypedResults.Ok()).RequireRateLimiting(RateLimitPolicies.Mutations).ApplyDefaultRateLimitPolicy();
        app.MapGet("/disabled", () => Results.Ok()).ApplyDefaultRateLimitPolicy().DisableRateLimiting();
        app.MapGet("/custom", () => Results.Ok()).Produces(200).ProducesProblem(429).ApplyDefaultRateLimitPolicy();
        var group = app.MapGroup("/group").RequireRateLimiting(RateLimitPolicies.Auth).ApplyDefaultRateLimitPolicy();
        group.MapPost("/limited", () => TypedResults.Ok());
        group.MapGet("/disabled", () => TypedResults.Ok()).DisableRateLimiting();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            BaseAddress = new Uri(app.Urls.Single())
        };

        using var json = JsonDocument.Parse(await client.GetStringAsync($"/openapi/{document}.json", TestContext.Current.CancellationToken));
        var paths = json.RootElement.GetProperty("paths");
        foreach (var (path, method) in new[] { ("/global", "get"), ("/explicit", "post"), ("/custom", "get"), ("/group/limited", "post") })
        {
            var responses = paths.GetProperty(path).GetProperty(method).GetProperty("responses");
            responses.TryGetProperty("200", out _).Should().BeTrue();
            var rejection = responses.GetProperty("429");
            var schema = rejection.GetProperty("content").GetProperty("application/problem+json").GetProperty("schema");
            schema.GetProperty("$ref").GetString().Should().Be("#/components/schemas/ProblemDetails");
            schema = json.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("ProblemDetails");
            schema.GetProperty("properties").TryGetProperty("status", out _).Should().BeTrue();
            var header = rejection.GetProperty("headers").GetProperty("Retry-After");
            header.GetProperty("description").GetString().Should().Contain("seconds").And.Contain("If absent");
            header.GetProperty("schema").GetProperty("type").GetString().Should().Be("integer");
            header.GetProperty("schema").GetProperty("minimum").GetInt32().Should().Be(1);
        }
        paths.GetProperty("/disabled").GetProperty("get").GetProperty("responses")
            .TryGetProperty("429", out _).Should().BeFalse();
        paths.GetProperty("/group/disabled").GetProperty("get").GetProperty("responses")
            .TryGetProperty("429", out _).Should().BeFalse();
    }
}
