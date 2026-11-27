using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Features.Following;
using IHFiction.UnitTests.SharedWeb;

using NSubstitute;

namespace IHFiction.UnitTests.Features.Following;

public sealed class AuthenticationRefreshTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitialLookup_AuthenticationChanges_DiscardsOldFollowState(bool story)
    {
        var id = Ulid.NewUlid();
        var oldLookup = new TaskCompletionSource<GetDeviceFollowsResponse>();
        var deviceCalls = 0;
        var client = Client();
        client.GetDeviceFollowsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++deviceCalls == 1 ? oldLookup.Task : Task.FromResult(new GetDeviceFollowsResponse()));
        await using var host = new AuthenticationComponentHost(story ? typeof(StoryFollowButton) : typeof(AuthorFollowButton), client,
            new() { [story ? "StoryId" : "AuthorId"] = story ? id : id.ToString() });
        await host.Renderer.Dispatcher.InvokeAsync(async () =>
        {
            await host.RenderAsync(AuthenticationComponentHost.State(null));
            var initial = host.HydrateAsync();
            await host.ChangeAuthenticationAsync(AuthenticationComponentHost.State("new-user"));
            await host.ParametersChanged;
            oldLookup.SetResult(new GetDeviceFollowsResponse
            {
                Authors = { new DeviceFollowedAuthorItem { AuthorId = id, Name = "Old author" } },
                Stories = { new DeviceFollowedStoryItem { StoryId = id, Title = "Old story", IsPublished = true } }
            });
            await initial;
            Assert.DoesNotContain(story ? "Unfollow Story" : "Unfollow Author", host.Root.ToHtmlString());
            Assert.Contains(story ? "Follow Story" : "Follow Author", host.Root.ToHtmlString());
            await client.Received(1).GetOwnFollowsAsync(null, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Following_OlderReloadCompletesLast_PreservesLatestDataAndRequestIdentity()
    {
        var pending = new TaskCompletionSource<GetOwnFollowsResponse>();
        var client = Client();
        client.GetOwnFollowsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        await using var host = new AuthenticationComponentHost(typeof(IHFiction.SharedWeb.Features.Following.Following), client);
        await host.Renderer.Dispatcher.InvokeAsync(async () =>
        {
            await host.RenderAsync(AuthenticationComponentHost.State("old-user"));
            var initial = host.HydrateAsync();
            await host.ChangeAuthenticationAsync(AuthenticationComponentHost.State(null));
            await host.ParametersChanged;
            pending.SetResult(new GetOwnFollowsResponse
            {
                Authors = { new OwnFollowedAuthorItem { AuthorId = Ulid.NewUlid(), Name = "Old account author" } }
            });
            await initial;
            Assert.DoesNotContain("Old account author", host.Root.ToHtmlString());
            Assert.Contains("No authors followed", host.Root.ToHtmlString());
            await client.DidNotReceive().GetOwnNotificationsAsync(Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Following_OldReloadFinishesWhileNewReloadPending_DoesNotHideLoader()
    {
        var old = new TaskCompletionSource<GetOwnFollowsResponse>();
        var current = new TaskCompletionSource<GetOwnFollowsResponse>();
        var client = Client();
        client.GetOwnFollowsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(old.Task, current.Task);
        await using var host = new AuthenticationComponentHost(typeof(IHFiction.SharedWeb.Features.Following.Following), client);
        await host.Renderer.Dispatcher.InvokeAsync(async () =>
        {
            await host.RenderAsync(AuthenticationComponentHost.State("first"));
            var initial = host.HydrateAsync();
            await host.ChangeAuthenticationAsync(AuthenticationComponentHost.State("second"));
            var latest = host.ParametersChanged;
            old.SetResult(new GetOwnFollowsResponse());
            await initial;
            var stillLoading = host.Loader.IsLoading;
            current.SetResult(new GetOwnFollowsResponse());
            await latest;
            Assert.True(stillLoading);
            Assert.False(host.Loader.IsLoading);
        });
    }

    private static FictionApiClient Client()
    {
        var client = Substitute.For<FictionApiClient>(new HttpClient());
        client.GetOwnFollowsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new GetOwnFollowsResponse());
        client.GetDeviceFollowsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new GetDeviceFollowsResponse());
        client.GetOwnNotificationsAsync(Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new GetOwnNotificationsResponse());
        client.GetDeviceNotificationsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new GetDeviceNotificationsResponse());
        return client;
    }
}
