using FluentAssertions;

using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Services;

namespace IHFiction.UnitTests.SharedWeb;

public sealed class MarkdownReaderServiceTests
{
    [Fact]
    public async Task ResolveAsync_DirectWork_LoadsItsContent()
    {
        var workId = Ulid.NewUlid();
        var meta = CreateMeta(workId, isDirectlyReadable: true);
        var content = CreateContent(workId, "Complete body");
        var reader = new FakePublishedWorkReader { ContentResults = { [workId] = content } };

        var result = await new MarkdownReaderService(reader)
            .ResolveAsync(workId.ToString(), meta, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        var model = result.Value!;
        model.WorkId.Should().Be(workId.ToString());
        model.Meta.Should().BeSameAs(meta);
        model.Content.Should().BeSameAs(content);
    }

    [Fact]
    public async Task ResolveAsync_ContainerWork_LoadsValidatedDefaultChild()
    {
        var storyId = Ulid.NewUlid();
        var chapterId = Ulid.NewUlid();
        var storyMeta = CreateMeta(
            storyId,
            isDirectlyReadable: false,
            defaultReadableWorkId: chapterId,
            readableChildren: [CreateChild(chapterId)]);
        var chapterMeta = CreateMeta(chapterId, isDirectlyReadable: true);
        var content = CreateContent(chapterId, "First chapter body");
        var reader = new FakePublishedWorkReader
        {
            MetaResults = { [chapterId] = chapterMeta },
            ContentResults = { [chapterId] = content }
        };

        var result = await new MarkdownReaderService(reader)
            .ResolveAsync(storyId.ToString(), storyMeta, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        var model = result.Value!;
        model.WorkId.Should().Be(chapterId.ToString());
        model.Meta.Should().BeSameAs(chapterMeta);
        model.Content.Should().BeSameAs(content);
    }

    [Fact]
    public async Task ResolveAsync_DefaultChildOutsideReadableChildren_ReturnsUnavailable()
    {
        var storyId = Ulid.NewUlid();
        var storyMeta = CreateMeta(
            storyId,
            isDirectlyReadable: false,
            defaultReadableWorkId: Ulid.NewUlid(),
            readableChildren: [CreateChild(Ulid.NewUlid())]);

        var result = await new MarkdownReaderService(new FakePublishedWorkReader())
            .ResolveAsync(storyId.ToString(), storyMeta, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.DomainError.Should().NotBeNull();
        result.DomainError!.Code.Should().Be("MarkdownReader.NoReadableContent");
    }

    [Fact]
    public async Task ResolveAsync_ContentFailure_PreservesApiError()
    {
        var workId = Ulid.NewUlid();
        var meta = CreateMeta(workId, isDirectlyReadable: true);
        var expected = new DomainError("WorkContent.NotPublished", "Work is not published.");
        var reader = new FakePublishedWorkReader { ContentErrors = { [workId] = expected } };

        var result = await new MarkdownReaderService(reader)
            .ResolveAsync(workId.ToString(), meta, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.DomainError.Should().Be(expected);
    }

    [Fact]
    public async Task ResolveAsync_CanceledRequest_PropagatesCancellation()
    {
        var workId = Ulid.NewUlid();
        var meta = CreateMeta(workId, isDirectlyReadable: true);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = () => new MarkdownReaderService(new FakePublishedWorkReader())
            .ResolveAsync(workId.ToString(), meta, cancellation.Token)
            .AsTask();

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static LinkedOfGetPublishedWorkMetaResponse CreateMeta(
        Ulid id,
        bool isDirectlyReadable,
        Ulid? defaultReadableWorkId = null,
        ICollection<ReadableWorkItem>? readableChildren = null) => new()
    {
        Id = id,
        Title = $"Work {id}",
        WorkType = isDirectlyReadable ? "Chapter" : "Story",
        ReaderKind = isDirectlyReadable ? "Chapter" : "MultiChapter",
        IsDirectlyReadable = isDirectlyReadable,
        DefaultReadableWorkId = defaultReadableWorkId,
        ReadableChildren = readableChildren ?? [],
        Authors = [],
        Links = []
    };

    private static ReadableWorkItem CreateChild(Ulid id) => new()
    {
        Id = id,
        Title = $"Chapter {id}",
        Order = 0
    };

    private static LinkedOfGetPublishedWorkContentResponse CreateContent(Ulid id, string body) => new()
    {
        Id = id,
        Title = $"Work {id}",
        WorkType = "Chapter",
        Content = body,
        Links = []
    };

    private sealed class FakePublishedWorkReader : IPublishedWorkReader
    {
        public Dictionary<Ulid, LinkedOfGetPublishedWorkMetaResponse> MetaResults { get; } = [];
        public Dictionary<Ulid, LinkedOfGetPublishedWorkContentResponse> ContentResults { get; } = [];
        public Dictionary<Ulid, DomainError> ContentErrors { get; } = [];

        public ValueTask<Result<LinkedOfGetPublishedWorkMetaResponse>> GetPublishedWorkMetaAsync(
            Ulid id,
            string? fields = null,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(MetaResults.TryGetValue(id, out var value)
                ? Result.Success(value)
                : Result.Failure<LinkedOfGetPublishedWorkMetaResponse>(DomainError.NotFound));

        public ValueTask<Result<LinkedOfGetPublishedWorkContentResponse>> GetPublishedWorkContentAsync(
            Ulid id,
            string? fields = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ContentResults.TryGetValue(id, out var value)
                ? Result.Success(value)
                : Result.Failure<LinkedOfGetPublishedWorkContentResponse>(
                    ContentErrors.GetValueOrDefault(id, DomainError.NotFound)));
        }
    }
}
