using IHFiction.SharedKernel.Notifications;
using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Features.Following;
using IHFiction.UnitTests.SharedWeb;

using NSubstitute;

namespace IHFiction.UnitTests.Features.Following;

public sealed class AuthenticationActionTests
{
    [Theory]
    [InlineData(false, "prompt")]
    [InlineData(true, "prompt")]
    [InlineData(false, "registration")]
    [InlineData(true, "registration")]
    [InlineData(false, "mutation")]
    [InlineData(true, "mutation")]
    [InlineData(false, "failure")]
    [InlineData(true, "failure")]
    public async Task Toggle_AuthenticationChangesAtAwait_DoesNotContinueOrModifyNewAccount(bool story, string stage)
    {
        var id = Ulid.NewUlid();
        var pending = new TaskCompletionSource();
        var client = Client();
        client.GetOwnFollowsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new GetOwnFollowsResponse
        {
            Authors = { new OwnFollowedAuthorItem { AuthorId = id, Name = "New author" } },
            Stories = { new OwnFollowedStoryItem { StoryId = id, Title = "New story", IsPublished = true } }
        });
        client.RegisterDevicePushSubscriptionAsync(Arg.Any<RegisterDevicePushSubscriptionBody>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ => { if (stage == "registration") await pending.Task; return new LinkedOfRegisterDevicePushSubscriptionResponse(); });
        client.FollowAuthorForDeviceAsync(id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ => { if (stage is "mutation" or "failure") await pending.Task; if (stage == "failure") throw Failure(); return new LinkedOfFollowAuthorForDeviceResponse(); });
        client.FollowStoryForDeviceAsync(id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ => { if (stage is "mutation" or "failure") await pending.Task; if (stage == "failure") throw Failure(); return new LinkedOfFollowStoryForDeviceResponse(); });
        await using var host = new AuthenticationComponentHost(story ? typeof(StoryFollowButton) : typeof(AuthorFollowButton), client,
            new() { [story ? "StoryId" : "AuthorId"] = story ? id : id.ToString() }, async () =>
            {
                if (stage == "prompt") await pending.Task;
                return new BrowserPushSubscription(new PushSubscription("https://push.example.test", null, new("key", "auth")), "test");
            });
        await host.Renderer.Dispatcher.InvokeAsync(async () =>
        {
            await host.RenderAsync(AuthenticationComponentHost.State(null));
            await host.HydrateAsync();
            var action = host.InvokeActionAsync("ToggleFollowAsync");
            await host.ChangeAuthenticationAsync(AuthenticationComponentHost.State("replacement"));
            await host.ParametersChanged;
            pending.SetResult();
            await action;
            await host.RenderCurrentAsync();

            Assert.Contains(story ? "Unfollow Story" : "Unfollow Author", host.Root.ToHtmlString());
            Assert.DoesNotContain("help is-danger", host.Root.ToHtmlString());
            Assert.DoesNotContain(client.ReceivedCalls(), call => call.GetMethodInfo().Name.StartsWith("Unfollow", StringComparison.Ordinal));
            Assert.DoesNotContain(client.ReceivedCalls(), call => call.GetMethodInfo().Name == "RegisterOwnPushSubscriptionAsync");
            if (stage is "prompt" or "registration")
                Assert.DoesNotContain(client.ReceivedCalls(), call => call.GetMethodInfo().Name is "FollowAuthorForDeviceAsync" or "FollowStoryForDeviceAsync");
        });
    }

    [Theory]
    [InlineData("OnUnfollowAuthorAsync", false)]
    [InlineData("OnUnfollowStoryAsync", false)]
    [InlineData("OnMarkReadAsync", false)]
    [InlineData("OnUnfollowAuthorAsync", true)]
    [InlineData("OnUnfollowStoryAsync", true)]
    [InlineData("OnMarkReadAsync", true)]
    public async Task InboxAction_AccountChanges_DiscardsOldSuccessAndErrors(string handler, bool fail)
    {
        var id = Ulid.NewUlid();
        var pending = new TaskCompletionSource();
        var client = Client();
        client.GetOwnFollowsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new GetOwnFollowsResponse
        {
            Authors = { new OwnFollowedAuthorItem { AuthorId = id, Name = "Current author" } },
            Stories = { new OwnFollowedStoryItem { StoryId = id, Title = "Current story", IsPublished = true } }
        });
        var inboxCalls = 0;
        client.GetOwnNotificationsAsync(Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => new GetOwnNotificationsResponse
        {
            UnreadCount = 1,
            Items = { new OwnNotificationItem { NotificationId = ++inboxCalls == 1 ? id : Ulid.NewUlid(), AuthorId = id, Title = "Current notification", AuthorName = "Current author", Kind = "story_published", TargetPath = "/stories/test" } }
        });
        client.UnfollowAuthorAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ => { await pending.Task; if (fail) throw Failure(); return new LinkedOfUnfollowAuthorResponse(); });
        client.UnfollowStoryAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ => { await pending.Task; if (fail) throw Failure(); return new LinkedOfUnfollowStoryResponse(); });
        client.MarkOwnNotificationReadAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ => { await pending.Task; if (fail) throw Failure(); return new LinkedOfMarkOwnNotificationReadResponse(); });
        // Mark-read's own failure falls back to the device endpoint in NotificationService.
        client.MarkDeviceNotificationReadAsync(id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<MarkDeviceNotificationReadResponse>(Failure()));
        await using var host = new AuthenticationComponentHost(typeof(IHFiction.SharedWeb.Features.Following.Following), client);
        await host.Renderer.Dispatcher.InvokeAsync(async () =>
        {
            await host.RenderAsync(AuthenticationComponentHost.State("first"));
            await host.HydrateAsync();
            var action = host.InvokeActionAsync(handler, id);
            await host.ChangeAuthenticationAsync(AuthenticationComponentHost.State("replacement"));
            await host.ParametersChanged;
            pending.SetResult();
            await action;
            await host.RenderCurrentAsync();
            var html = host.Root.ToHtmlString();
            Assert.Contains($"href=\"/authors/{id}\">Current author</a>", html);
            Assert.Contains($"href=\"/stories/{id}\">Current story</a>", html);
            Assert.Contains("1 unread", html);
            Assert.DoesNotContain("notification is-danger", html);
        });
    }

    private static ApiException Failure() => new("Old request failed", 500, "", new Dictionary<string, IEnumerable<string>>(), null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OldPromptCompletes_DoesNotUnlockReplacementAccountsPendingToggle(bool story)
    {
        var oldPrompt = new TaskCompletionSource<BrowserPushSubscription?>();
        var newPrompt = new TaskCompletionSource<BrowserPushSubscription?>();
        var prompts = 0;
        var id = Ulid.NewUlid();
        await using var host = new AuthenticationComponentHost(story ? typeof(StoryFollowButton) : typeof(AuthorFollowButton), Client(),
            new() { [story ? "StoryId" : "AuthorId"] = story ? id : id.ToString() },
            () => ++prompts == 1 ? oldPrompt.Task : newPrompt.Task);
        await host.Renderer.Dispatcher.InvokeAsync(async () =>
        {
            await host.RenderAsync(AuthenticationComponentHost.State(null));
            await host.HydrateAsync();
            var oldAction = host.InvokeActionAsync("ToggleFollowAsync");
            await host.ChangeAuthenticationAsync(AuthenticationComponentHost.State("replacement"));
            await host.ParametersChanged;
            var currentAction = host.InvokeActionAsync("ToggleFollowAsync");
            oldPrompt.SetResult(null);
            await oldAction;
            await host.RenderCurrentAsync();
            var pendingHtml = host.Root.ToHtmlString();
            newPrompt.SetResult(null);
            await currentAction;
            await host.RenderCurrentAsync();
            Assert.Contains("disabled", pendingHtml);
            Assert.DoesNotContain("disabled", host.Root.ToHtmlString());
        });
    }

    [Fact]
    public async Task OldInboxActionCompletes_DoesNotUnlockReplacementAccountsPendingAction()
    {
        var oldRequest = new TaskCompletionSource<LinkedOfUnfollowAuthorResponse>();
        var currentRequest = new TaskCompletionSource<LinkedOfUnfollowStoryResponse>();
        var client = Client();
        var id = Ulid.NewUlid();
        client.GetOwnFollowsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new GetOwnFollowsResponse
        {
            Authors = { new OwnFollowedAuthorItem { AuthorId = id, Name = "Author" } },
            Stories = { new OwnFollowedStoryItem { StoryId = id, Title = "Story", IsPublished = true } }
        });
        client.UnfollowAuthorAsync(Arg.Any<Ulid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(oldRequest.Task);
        client.UnfollowStoryAsync(Arg.Any<Ulid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(currentRequest.Task);
        await using var host = new AuthenticationComponentHost(typeof(IHFiction.SharedWeb.Features.Following.Following), client);
        await host.Renderer.Dispatcher.InvokeAsync(async () =>
        {
            await host.RenderAsync(AuthenticationComponentHost.State("first"));
            await host.HydrateAsync();
            var oldAction = host.InvokeActionAsync("OnUnfollowAuthorAsync", id);
            await host.ChangeAuthenticationAsync(AuthenticationComponentHost.State("replacement"));
            await host.ParametersChanged;
            var currentAction = host.InvokeActionAsync("OnUnfollowStoryAsync", id);
            oldRequest.SetResult(new LinkedOfUnfollowAuthorResponse());
            await oldAction;
            await host.RenderCurrentAsync();
            var pendingHtml = host.Root.ToHtmlString();
            currentRequest.SetResult(new LinkedOfUnfollowStoryResponse());
            await currentAction;
            await host.RenderCurrentAsync();
            Assert.Matches("<button[^>]*disabled[^>]*>\\s*Refresh", pendingHtml);
            Assert.DoesNotMatch("<button[^>]*disabled[^>]*>\\s*Refresh", host.Root.ToHtmlString());
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
