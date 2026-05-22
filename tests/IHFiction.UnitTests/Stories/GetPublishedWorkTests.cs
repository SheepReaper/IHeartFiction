using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Stories;

using MongoDB.Bson;

namespace IHFiction.UnitTests.Stories;

public sealed class GetPublishedWorkTests(MongoDbFixture mongoDbFixture) : IClassFixture<MongoDbFixture>
{
    [Fact]
    public async Task WorkMeta_ForOneShotStory_IsDirectlyReadableAndIndexable()
    {
        await using var context = CreateContext();
        var author = AddAuthor(context);
        var story = AddStory(context, author, workBodyId: ObjectId.GenerateNewId(), published: true);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new GetPublishedWorkMeta(context);

        var result = await useCase.HandleAsync(story.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDirectlyReadable);
        Assert.True(result.Value.CanIndex);
        Assert.Equal($"/read/{story.Id}", result.Value.CanonicalUrl);
        Assert.Equal(nameof(Story), result.Value.WorkType);
        Assert.Equal(StoryType.SingleBody, result.Value.ReaderKind);
        Assert.Empty(result.Value.ReadableChildren);
    }

    [Fact]
    public async Task WorkMeta_ForChapter_IsDirectlyReadableAndIncludesParentContext()
    {
        await using var context = CreateContext();
        var author = AddAuthor(context);
        var story = AddStory(context, author, published: true);
        var chapter1 = AddStoryChapter(story, author, "Chapter 1", 1, published: true);
        var chapter2 = AddStoryChapter(story, author, "Chapter 2", 2, published: true);
        context.Chapters.AddRange(chapter1, chapter2);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new GetPublishedWorkMeta(context);

        var result = await useCase.HandleAsync(chapter1.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDirectlyReadable);
        Assert.True(result.Value.CanIndex);
        Assert.Equal($"/read/{chapter1.Id}", result.Value.CanonicalUrl);
        Assert.Equal(nameof(Chapter), result.Value.WorkType);
        Assert.Equal(story.Id, result.Value.StoryId);
        Assert.Equal(story.Title, result.Value.StoryTitle);
        Assert.Equal(new[] { chapter1.Id, chapter2.Id }, result.Value.ReadableChildren.Select(child => child.Id));
    }

    [Fact]
    public async Task WorkMeta_ForMultiChapterStory_IsNotIndexableAndProvidesDefaultReadableChild()
    {
        await using var context = CreateContext();
        var author = AddAuthor(context);
        var story = AddStory(context, author, published: true);
        var chapter1 = AddStoryChapter(story, author, "Chapter 1", 1, published: true);
        var chapter2 = AddStoryChapter(story, author, "Chapter 2", 2, published: true);
        context.Chapters.AddRange(chapter1, chapter2);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new GetPublishedWorkMeta(context);

        var result = await useCase.HandleAsync(story.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsDirectlyReadable);
        Assert.False(result.Value.CanIndex);
        Assert.Null(result.Value.CanonicalUrl);
        Assert.Equal(StoryType.MultiChapter, result.Value.ReaderKind);
        Assert.Equal(chapter1.Id, result.Value.DefaultReadableWorkId);
        Assert.Equal(new[] { chapter1.Id, chapter2.Id }, result.Value.ReadableChildren.Select(child => child.Id));
    }

    [Fact]
    public async Task WorkMeta_ForBook_IsNotIndexableAndProvidesBookChapters()
    {
        await using var context = CreateContext();
        var author = AddAuthor(context);
        var story = AddStory(context, author, published: true);
        var book = AddBook(story, author, published: true);
        var chapter1 = AddBookChapter(book, author, "Book Chapter 1", 1, published: true);
        var chapter2 = AddBookChapter(book, author, "Book Chapter 2", 2, published: true);
        context.Books.Add(book);
        context.Chapters.AddRange(chapter1, chapter2);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new GetPublishedWorkMeta(context);

        var result = await useCase.HandleAsync(book.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsDirectlyReadable);
        Assert.False(result.Value.CanIndex);
        Assert.Null(result.Value.CanonicalUrl);
        Assert.Equal(nameof(Book), result.Value.WorkType);
        Assert.Equal(book.Id, result.Value.BookId);
        Assert.Equal(chapter1.Id, result.Value.DefaultReadableWorkId);
        Assert.All(result.Value.ReadableChildren, child => Assert.Equal(book.Id, child.BookId));
    }

    [Fact]
    public async Task WorkContent_ForOneShotStory_ReturnsMongoBody()
    {
        if (!mongoDbFixture.IsAvailable)
        {
            return;
        }

        await using var context = CreateContext();
        var author = AddAuthor(context);
        var workBodyId = ObjectId.GenerateNewId();
        var story = AddStory(context, author, workBodyId, published: true);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var workBodies = mongoDbFixture.Client!
            .GetDatabase($"unit-test-{Guid.NewGuid():N}")
            .GetCollection<WorkBody>("works");
        await workBodies.InsertOneAsync(new WorkBody
        {
            Id = workBodyId,
            Content = "One-shot content",
            Note1 = "Before",
            Note2 = "After",
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken: TestContext.Current.CancellationToken);
        var useCase = new GetPublishedWorkContent(workBodies, context);

        var result = await useCase.HandleAsync(story.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(story.Id, result.Value.Id);
        Assert.Equal(nameof(Story), result.Value.WorkType);
        Assert.Equal("One-shot content", result.Value.Content);
        Assert.Equal("Before", result.Value.Note1);
        Assert.Equal("After", result.Value.Note2);
    }

    [Fact]
    public async Task WorkContent_ForNonDirectStory_ReturnsNotDirectlyReadable()
    {
        await using var context = CreateContext();
        var author = AddAuthor(context);
        var story = AddStory(context, author, published: true);
        context.Chapters.Add(AddStoryChapter(story, author, "Chapter 1", 1, published: true));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new GetPublishedWorkContent(workBodies: null!, context);

        var result = await useCase.HandleAsync(story.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(GetPublishedWorkContent.Errors.NotDirectlyReadable.Code, result.DomainError.Code);
    }

    private static FictionDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FictionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new FictionDbContext(options);
    }

    private static Author AddAuthor(FictionDbContext context)
    {
        var author = new Author
        {
            Id = Ulid.NewUlid(),
            Name = "Test Author"
        };

        context.Authors.Add(author);
        return author;
    }

    private static Story AddStory(
        FictionDbContext context,
        Author author,
        ObjectId? workBodyId = null,
        bool published = false)
    {
        var story = new Story
        {
            Id = Ulid.NewUlid(),
            Title = "Test Story",
            Description = "Story description",
            Owner = author,
            OwnerId = author.Id,
            WorkBodyId = workBodyId,
            PublishedAt = published ? DateTime.UtcNow : null
        };

        story.Authors.Add(author);
        context.Stories.Add(story);
        return story;
    }

    private static Book AddBook(Story story, Author author, bool published)
    {
        var book = new Book
        {
            Id = Ulid.NewUlid(),
            Title = "Test Book",
            Description = "Book description",
            Order = 1,
            Owner = author,
            OwnerId = author.Id,
            Story = story,
            StoryId = story.Id,
            PublishedAt = published ? DateTime.UtcNow : null
        };

        book.Authors.Add(author);
        story.Books.Add(book);
        return book;
    }

    private static Chapter AddStoryChapter(
        Story story,
        Author author,
        string title,
        int order,
        bool published)
    {
        var chapter = CreateChapter(author, title, order, published);
        chapter.Story = story;
        chapter.StoryId = story.Id;
        story.Chapters.Add(chapter);
        return chapter;
    }

    private static Chapter AddBookChapter(
        Book book,
        Author author,
        string title,
        int order,
        bool published)
    {
        var chapter = CreateChapter(author, title, order, published);
        chapter.Book = book;
        chapter.BookId = book.Id;
        book.Chapters.Add(chapter);
        return chapter;
    }

    private static Chapter CreateChapter(
        Author author,
        string title,
        int order,
        bool published)
    {
        var chapter = new Chapter
        {
            Id = Ulid.NewUlid(),
            Title = title,
            Order = order,
            Owner = author,
            OwnerId = author.Id,
            WorkBodyId = ObjectId.GenerateNewId(),
            PublishedAt = published ? DateTime.UtcNow : null
        };

        chapter.Authors.Add(author);
        return chapter;
    }
}
