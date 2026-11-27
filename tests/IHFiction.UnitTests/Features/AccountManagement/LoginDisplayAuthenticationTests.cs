using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Features.AccountManagement;
using IHFiction.UnitTests.SharedWeb;

using NSubstitute;

namespace IHFiction.UnitTests.Features.AccountManagement;

public sealed class LoginDisplayAuthenticationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SwitchingAccount_ClearsRenderedAvatarWhenProfileHasNoEmailOrFails(bool fail)
    {
        var client = Substitute.For<FictionApiClient>(new HttpClient());
        var next = new TaskCompletionSource<LinkedOfGetOwnUserProfileResponse>();
        client.GetOwnUserProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new LinkedOfGetOwnUserProfileResponse { GravatarEmail = "first@example.test" }), next.Task);
        await using var host = new AuthenticationComponentHost(typeof(LoginDisplay), client);
        await host.Renderer.Dispatcher.InvokeAsync(async () =>
        {
            await host.RenderAsync(AuthenticationComponentHost.State("first"));
            await host.HydrateAsync();
            Assert.Contains("gravatar.com/avatar/", host.Root.ToHtmlString());
            await host.ChangeAuthenticationAsync(AuthenticationComponentHost.State("second"));
            var load = host.HydrateAsync(false);
            if (fail) next.SetException(new ApiException("Failed", 500, "", new Dictionary<string, IEnumerable<string>>(), null));
            else next.SetResult(new LinkedOfGetOwnUserProfileResponse());
            await load;
            Assert.DoesNotContain("gravatar.com/avatar/", host.Root.ToHtmlString());
        });
    }

    [Fact]
    public async Task OldProfileCompletesAfterAccountChange_DoesNotRestoreOldAvatar()
    {
        var client = Substitute.For<FictionApiClient>(new HttpClient());
        var old = new TaskCompletionSource<LinkedOfGetOwnUserProfileResponse>();
        client.GetOwnUserProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(old.Task, Task.FromResult(new LinkedOfGetOwnUserProfileResponse()));
        await using var host = new AuthenticationComponentHost(typeof(LoginDisplay), client);
        await host.Renderer.Dispatcher.InvokeAsync(async () =>
        {
            await host.RenderAsync(AuthenticationComponentHost.State("first"));
            var initial = host.HydrateAsync();
            await host.ChangeAuthenticationAsync(AuthenticationComponentHost.State("second"));
            await host.HydrateAsync(false);
            old.SetResult(new LinkedOfGetOwnUserProfileResponse { GravatarEmail = "first@example.test" });
            await initial;
            Assert.DoesNotContain("gravatar.com/avatar/", host.Root.ToHtmlString());
        });
    }
}
