using FluentAssertions;

using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Features.Discovery;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

using Microsoft.AspNetCore.Http;

namespace IHFiction.UnitTests.Features.Discovery;

public class GetAuthorShapingTests
{
    [Fact]
    public void WithLinks_And_ShapeData_Flattens_LinkedWrapper()
    {
        // Arrange
        var response = new GetAuthor.GetAuthorResponse(
            UserId: Guid.NewGuid(),
            Name: "Test Author",
            AvatarUrl: null,
            CreatedAt: DateTime.UtcNow.AddDays(-1),
            UpdatedAt: DateTime.UtcNow,
            DeletedAt: null,
            Profile: new GetAuthor.GaAuthorProfile("Bio text", []),
            PublishedStories: [new GetAuthor.AuthorWorkItem(Ulid.NewUlid(), "Story 1", DateTime.UtcNow)],
            TotalStories: 1
        );

        // Wrap into domain Result and attach links
        var baseResult = Result.Success(response);
        var link = new LinkItem("/authors/01", "self", HttpMethods.Get);
        var linkedResult = baseResult.WithLinks(new[] { link });

        // Ensure wrapping succeeded
        linkedResult.IsSuccess.Should().BeTrue();
        var linkedValue = linkedResult.Value!; // Linked<GetAuthorResponse>

        // Act - shape the linked value to an ExpandoObject which should flatten Linked<T>
        var shaped = DataShapingService.ShapeData(linkedValue, null);
        var dict = (IDictionary<string, object?>)shaped;

        // Assert - top-level properties from GetAuthorResponse should exist
        dict.Should().ContainKey("UserId");
        dict.Should().ContainKey("Name");
        dict.Should().ContainKey("Profile");
        dict.Should().ContainKey("PublishedStories");
        dict.Should().ContainKey("TotalStories");

        // Assert - links should be present at top level as "links"
        dict.Should().ContainKey("links");
        dict["links"].Should().BeAssignableTo<IEnumerable<LinkItem>>();

        var links = ((IEnumerable<LinkItem>)dict["links"]!).ToList();
        links.Should().ContainSingle();
        links.First().Href.Should().Be("/authors/01");
        links.First().Rel.Should().Be("self");
    }

    [Fact]
    public void WithLinks_And_ShapeData_WithSelectedFields_FiltersInnerPropertiesAndRetainsLinks()
    {
        var response = new GetAuthor.GetAuthorResponse(
            UserId: Guid.NewGuid(),
            Name: "Test Author",
            AvatarUrl: null,
            CreatedAt: DateTime.UtcNow.AddDays(-1),
            UpdatedAt: DateTime.UtcNow,
            DeletedAt: null,
            Profile: new GetAuthor.GaAuthorProfile("Bio text", []),
            PublishedStories: [new GetAuthor.AuthorWorkItem(Ulid.NewUlid(), "Story 1", DateTime.UtcNow)],
            TotalStories: 1
        );

        var linkedValue = Result.Success(response)
            .WithLinks([new LinkItem("/authors/01", "self", HttpMethods.Get)])
            .Value!;

        DataShapingService.HasRegisteredShaper<GetAuthor.GetAuthorResponse>().Should().BeTrue();

        var shaped = DataShapingService.ShapeData(linkedValue, "name,totalStories");
        var dict = (IDictionary<string, object?>)shaped;

        dict.Keys.Should().BeEquivalentTo("Name", "TotalStories", "links");
        dict["Name"].Should().Be("Test Author");
        dict["TotalStories"].Should().Be(1);
    }
}
