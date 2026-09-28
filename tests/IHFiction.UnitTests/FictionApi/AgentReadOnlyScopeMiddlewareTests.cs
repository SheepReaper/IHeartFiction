using System.Security.Claims;

using FluentAssertions;

using IHFiction.FictionApi.AgentAuth;

using Microsoft.AspNetCore.Http;

namespace IHFiction.UnitTests.FictionApi;

public sealed class AgentReadOnlyScopeMiddlewareTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task InvokeAsync_AllowsSafeAgentRequests(string method)
    {
        var nextCalled = false;
        var middleware = new AgentReadOnlyScopeMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAgentContext(method);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task InvokeAsync_RejectsAgentMutationRequests(string method)
    {
        var nextCalled = false;
        var middleware = new AgentReadOnlyScopeMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAgentContext(method);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
    }

    private static DefaultHttpContext CreateAgentContext(string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            AgentTokenService.AuthenticationScheme));
        return context;
    }
}
