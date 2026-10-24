using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;

using FluentAssertions;

using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IHFiction.UnitTests.FictionApi;

public sealed class RateLimitingTests
{
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
        rejectedContext.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(rejectedContext.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(429);
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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        services.AddApiRateLimiting();
        return services.BuildServiceProvider();
    }
}
