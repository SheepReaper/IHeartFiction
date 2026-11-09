using FluentAssertions;

using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Features.Reading;

using Markdig;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IHFiction.UnitTests.Features.Reading;

public sealed class MarkdownStoryReaderRenderingTests
{
    [Fact]
    public async Task RenderAsync_IncludesCompleteBodyNotesAndNavigationWithoutLoadingState()
    {
        var storyId = Ulid.NewUlid();
        var chapterId = Ulid.NewUlid();
        var nextChapterId = Ulid.NewUlid();
        var meta = new LinkedOfGetPublishedWorkMetaResponse
        {
            Id = chapterId,
            Title = "Chapter One",
            StoryId = storyId,
            StoryTitle = "Test Story",
            WorkType = "Chapter",
            ReaderKind = "Chapter",
            IsDirectlyReadable = true,
            Authors = [new WorkAuthor { Id = Ulid.NewUlid(), Name = "Test Author" }],
            ReadableChildren =
            [
                new ReadableWorkItem { Id = chapterId, Title = "Chapter One", Order = 0 },
                new ReadableWorkItem { Id = nextChapterId, Title = "Chapter Two", Order = 1 }
            ],
            Links = []
        };
        var content = new LinkedOfGetPublishedWorkContentResponse
        {
            Id = chapterId,
            Title = "Chapter One",
            WorkType = "Chapter",
            StoryId = storyId,
            StoryTitle = "Test Story",
            Content = "## Complete body\n\nThe full chapter text.",
            Note1 = "Opening note",
            Note2 = "Closing note",
            ContentUpdatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            Links = []
        };

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IPublishedWorkReader>(new FakePublishedWorkReader(content))
            .AddTransient<MarkdownReaderService>()
            .AddSingleton(new MarkdownPipelineBuilder().Build())
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<MarkdownStoryReader>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(MarkdownStoryReader.WorkId)] = chapterId.ToString(),
                    [nameof(MarkdownStoryReader.Meta)] = meta
                }));
            return component.ToHtmlString();
        });

        html.Should().Contain("Chapter One");
        html.Should().Contain("Opening note");
        html.Should().Contain("Complete body");
        html.Should().Contain("The full chapter text.");
        html.Should().Contain("Closing note");
        html.Should().Contain($"/read/{nextChapterId}");
        html.Should().NotContain("Loading...");
        html.Should().NotContain("Finding your place...");
    }

    [Theory]
    [InlineData("InProgress", "Caught up (story in progress)", "End of story")]
    [InlineData("Complete", "End of story", "Caught up (story in progress)")]
    public async Task RenderAsync_FinalPublishedChapter_DistinguishesCaughtUpFromCompletedStory(
        string storyCompletionStatus,
        string expectedItemText,
        string unexpectedItemText)
    {
        var storyId = Ulid.NewUlid();
        var chapter1Id = Ulid.NewUlid();
        var chapter2Id = Ulid.NewUlid();
        var meta = new LinkedOfGetPublishedWorkMetaResponse
        {
            Id = chapter2Id,
            Title = "Chapter Two",
            StoryId = storyId,
            StoryTitle = "Test Story",
            StoryCompletionStatus = storyCompletionStatus,
            WorkType = "Chapter",
            ReaderKind = "Chapter",
            IsDirectlyReadable = true,
            Authors = [new WorkAuthor { Id = Ulid.NewUlid(), Name = "Test Author" }],
            ReadableChildren =
            [
                new ReadableWorkItem { Id = chapter1Id, Title = "Chapter One", Order = 0 },
                new ReadableWorkItem { Id = chapter2Id, Title = "Chapter Two", Order = 1 }
            ],
            Links = []
        };
        var content = new LinkedOfGetPublishedWorkContentResponse
        {
            Id = chapter2Id,
            Title = "Chapter Two",
            WorkType = "Chapter",
            StoryId = storyId,
            StoryTitle = "Test Story",
            Content = "The final published chapter text.",
            ContentUpdatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            Links = []
        };

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IPublishedWorkReader>(new FakePublishedWorkReader(content))
            .AddTransient<MarkdownReaderService>()
            .AddSingleton(new MarkdownPipelineBuilder().Build())
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<MarkdownStoryReader>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(MarkdownStoryReader.WorkId)] = chapter2Id.ToString(),
                    [nameof(MarkdownStoryReader.Meta)] = meta
                }));
            return component.ToHtmlString();
        });

        html.Should().Contain(expectedItemText);
        html.Should().NotContain(unexpectedItemText);
        html.Should().Contain($"href=\"/stories/{storyId}\"");
    }

    private sealed class FakePublishedWorkReader(LinkedOfGetPublishedWorkContentResponse content)
        : IPublishedWorkReader
    {
        public ValueTask<Result<LinkedOfGetPublishedWorkMetaResponse>> GetPublishedWorkMetaAsync(
            Ulid id,
            string? fields = null,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Result.Failure<LinkedOfGetPublishedWorkMetaResponse>(DomainError.NotFound));

        public ValueTask<Result<LinkedOfGetPublishedWorkContentResponse>> GetPublishedWorkContentAsync(
            Ulid id,
            string? fields = null,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Result.Success(content));
    }
}
