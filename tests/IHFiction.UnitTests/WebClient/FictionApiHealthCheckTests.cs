using System.Net;
using FluentAssertions;
using IHFiction.WebClient.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;

namespace IHFiction.UnitTests.WebClient;

public sealed class FictionApiHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenApiReturnsSuccess_ReturnsHealthy()
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(FictionApiHealthCheck.ClientName)
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") });

        var check = new FictionApiHealthCheck(factory);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Fiction API is healthy.");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenApiReturns503_ReturnsUnhealthy()
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(FictionApiHealthCheck.ClientName)
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") });

        var check = new FictionApiHealthCheck(factory);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("503");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenApiThrowsException_ReturnsUnhealthy()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused"));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(FictionApiHealthCheck.ClientName)
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") });

        var check = new FictionApiHealthCheck(factory);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Fiction API is unreachable.");
        result.Exception.Should().NotBeNull();
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }
}
