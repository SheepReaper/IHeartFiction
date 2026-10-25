using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;

using FluentAssertions;

using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IHFiction.UnitTests.FictionApi;

public sealed class RateLimitingTests
{
    [Theory]
    [InlineData(null, 120)]
    [InlineData(RateLimitPolicies.Auth, 10)]
    public async Task Middleware_Rejection_UsesSharedProblemDetails(string? policy, int permits)
    {
        var builder = CreateBuilder();
        builder.AddCoreApiServices(TimeProvider.System);
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseRateLimiter();
        var endpoint = app.MapGet("/limited", () => Results.Ok());
        if (policy is not null)
            endpoint.RequireRateLimiting(policy);

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            BaseAddress = new Uri(app.Urls.Single())
        };
        for (var i = 0; i < permits; i++)
        {
            using var allowed = await client.GetAsync("/limited", TestContext.Current.CancellationToken);
            allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Repeated rejections must remain valid responses through exception handling and enrichment.
        for (var i = 0; i < 2; i++)
        {
            using var rejected = await client.GetAsync("/limited", TestContext.Current.CancellationToken);
            rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            rejected.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.Zero);
            rejected.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            using var body = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            body.RootElement.GetProperty("status").GetInt32().Should().Be(429);
            body.RootElement.GetProperty("title").GetString().Should().Be("Too many requests");
            body.RootElement.GetProperty("detail").GetString().Should().StartWith("Rate limit exceeded. Retry after ");
            body.RootElement.GetProperty("requestId").GetString().Should().NotBeNullOrEmpty();
            body.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void ResolveDefaultPolicy_AnonymousRead_UsesGlobalDefault()
    {
        RateLimitingExtensions.ResolveDefaultPolicy([new HttpMethodMetadata(["GET"])]).Should().BeNull();
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void ResolveDefaultPolicy_Writes_UseMutations(string method)
    {
        RateLimitingExtensions.ResolveDefaultPolicy([new HttpMethodMetadata([method]), new AuthorizeAttribute()])
            .Should().Be(RateLimitPolicies.Mutations);
    }

    [Fact]
    public void ResolveDefaultPolicy_AuthorizedRead_UsesAuthenticatedReads()
    {
        RateLimitingExtensions.ResolveDefaultPolicy([new HttpMethodMetadata(["GET"]), new AuthorizeAttribute()])
            .Should().Be(RateLimitPolicies.AuthenticatedReads);
    }

    [Fact]
    public void ResolveDefaultPolicy_AllowAnonymousRead_UsesGlobalDefault()
    {
        RateLimitingExtensions.ResolveDefaultPolicy([new HttpMethodMetadata(["GET"]), new AuthorizeAttribute(), new AllowAnonymousAttribute()])
            .Should().BeNull();
    }

    [Fact]
    public void ResolveDefaultPolicy_ExplicitPolicy_IsPreserved()
    {
        RateLimitingExtensions.ResolveDefaultPolicy([new HttpMethodMetadata(["POST"]), new EnableRateLimitingAttribute(RateLimitPolicies.Auth)])
            .Should().BeNull();
    }

    [Fact]
    public async Task GlobalLimiter_LimitsEndpointsWithoutExplicitPolicy_AndRejectsWith429ProblemDetails()
    {
        await using var provider = CreateProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;
        var limiter = options.GlobalLimiter!;

        HttpContext Create()
        {
            var context = new DefaultHttpContext { RequestServices = provider };
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
            context.Response.Body = new MemoryStream();
            return context;
        }

        for (var i = 0; i < 120; i++)
        {
            using var permitted = await limiter.AcquireAsync(Create(), 1, TestContext.Current.CancellationToken);
            permitted.IsAcquired.Should().BeTrue();
        }

        var rejectedContext = Create();
        using var lease = await limiter.AcquireAsync(rejectedContext, 1, TestContext.Current.CancellationToken);
        lease.IsAcquired.Should().BeFalse();

        await options.OnRejected!(new OnRejectedContext { HttpContext = rejectedContext, Lease = lease }, TestContext.Current.CancellationToken);

        rejectedContext.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        rejectedContext.Response.Headers.RetryAfter.ToString().Should().NotBeNullOrEmpty();
        rejectedContext.Response.ContentType.Should().StartWith("application/problem+json");
        rejectedContext.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(rejectedContext.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(429);
        body.RootElement.GetProperty("title").GetString().Should().Be("Too many requests");
        body.RootElement.GetProperty("requestId").GetString().Should().Be(rejectedContext.TraceIdentifier);
        body.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GlobalLimiter_DoesNotDoubleLimitEndpointsWithExplicitPolicy()
    {
        await using var provider = CreateProvider();
        var limiter = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter!;
        var endpoint = new Endpoint(null, new EndpointMetadataCollection(new EnableRateLimitingAttribute(RateLimitPolicies.Mutations)), "test");

        for (var i = 0; i < 200; i++)
        {
            var context = new DefaultHttpContext();
            context.SetEndpoint(endpoint);
            using var lease = await limiter.AcquireAsync(context, 1, TestContext.Current.CancellationToken);
            lease.IsAcquired.Should().BeTrue();
        }
    }

    private static ServiceProvider CreateProvider()
    {
        var builder = CreateBuilder();
        builder.AddCoreApiServices(TimeProvider.System);
        return builder.Services.BuildServiceProvider();
    }

    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration["AllowedHosts"] = "127.0.0.1";
        builder.Configuration["BaseUrl"] = "https://api.example.test";
        return builder;
    }
}
