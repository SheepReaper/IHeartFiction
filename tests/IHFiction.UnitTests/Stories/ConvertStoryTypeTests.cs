using Microsoft.EntityFrameworkCore;

using FluentAssertions;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Authors;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Stories;
using IHFiction.SharedKernel.Infrastructure;

using MongoDB.Bson;

namespace IHFiction.UnitTests.Stories;

public class ConvertStoryTypeTests
{
    [Fact]
    public void UpgradeOneShotToChaptered_MovesWorkBodyIdToNewChapter()
    {
        // Arrange - create in-memory db contexts
        var fictionOptions = new DbContextOptionsBuilder<FictionDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var storyOptions = new DbContextOptionsBuilder<StoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var fictionContext = new FictionDbContext(fictionOptions);
        using var storyContext = new StoryDbContext(storyOptions);

        // create author and story with an existing WorkBodyId
        var author = new Author { Id = Ulid.NewUlid(), Name = "Author" };
        fictionContext.Authors.Add(author);

        var originalWorkBodyId = ObjectId.GenerateNewId();

        var story = new Story
        {
            Id = Ulid.NewUlid(),
            Title = "One-shot",
            Description = "One-shot description",
            Owner = author,
            OwnerId = author.Id,
            WorkBodyId = originalWorkBodyId
        };

        fictionContext.Stories.Add(story);
        fictionContext.SaveChanges();

        // create the use-case instance - services are simple concrete instances wired to the in-memory FictionDbContext
        var entityLoader = new EntityLoaderService(fictionContext);
        var userService = new UserService(fictionContext);
        var authorization = new AuthorizationService(fictionContext, userService);

        var useCase = new ConvertStoryType(fictionContext, storyContext, entityLoader, authorization);

        // Use reflection to call the private UpgradeOneShotToChaptered method
        var method = typeof(ConvertStoryType).GetMethod("UpgradeOneShotToChaptered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull();

        var parameters = new object?[] { story, null };

        // Act
        var result = (Result)method!.Invoke(useCase, parameters)!;
        var modifiedWork = parameters[1] as Work;

        // Assert - conversion succeeded
        result.IsSuccess.Should().BeTrue();
        modifiedWork.Should().NotBeNull();
        modifiedWork.Should().BeOfType<Chapter>();

        var chapter = (Chapter)modifiedWork!;
        chapter.WorkBodyId.Should().Be(originalWorkBodyId);

        // Story should no longer have the WorkBodyId
        story.WorkBodyId.Should().BeNull();

        // The chapter should have been added to the Fiction context Chapters set
        fictionContext.Chapters.Local.Should().ContainSingle(c => c.WorkBodyId == originalWorkBodyId);
    }
}
