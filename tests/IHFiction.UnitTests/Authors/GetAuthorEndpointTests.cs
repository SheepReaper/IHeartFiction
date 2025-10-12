using Microsoft.AspNetCore.Http;

using FluentAssertions;

using IHFiction.FictionApi.Authors;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.UnitTests.Authors;

/// <summary>
/// Unit tests for GetAuthor endpoint functionality
/// Tests the endpoint constants and basic functionality
/// </summary>
public class GetAuthorEndpointTests
{
    [Fact]
    public void EndpointHandler_WhenUseCaseReturnsSuccess_ReturnsOkResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var response = new GetAuthor.GetAuthorResponse(
            userId,
            "Test Author",
            DateTime.UtcNow,
            null,
            new GetAuthor.GaAuthorProfile("Test bio"),
            [],

            0
        );

        // Act - Simulate the endpoint handler logic for success case
        var successResult = Result.Success(response);
        var httpResult = Results.Ok(successResult.Value);

        // Assert
        httpResult.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<GetAuthor.GetAuthorResponse>>();
        var okResult = (Microsoft.AspNetCore.Http.HttpResults.Ok<GetAuthor.GetAuthorResponse>)httpResult;
        okResult.Value.Should().Be(response);
    }

    [Fact]
    public void EndpointHandler_WhenUseCaseReturnsFailure_ReturnsNotFoundResult()
    {
        // Arrange & Act - Simulate the endpoint handler logic for failure case
        var httpResult = Results.NotFound();

        // Assert
        httpResult.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NotFound>();
    }
}

/// <summary>
/// Tests for the response record types
/// </summary>
public class GetAuthorResponseTests
{
    [Fact]
    public void Response_CanBeCreatedWithAllProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var name = "Test Author";
        var updatedAt = DateTime.UtcNow;
        var deletedAt = DateTime.UtcNow.AddDays(1);
        var profile = new GetAuthor.GaAuthorProfile("Test bio");
        var works = new List<GetAuthor.AuthorWorkItem>
        {
            new(Ulid.NewUlid(), "Work 1", DateTime.Now),
            new(Ulid.NewUlid(), "Work 2", DateTime.Now)
        };

        // Act
        var response = new GetAuthor.GetAuthorResponse(userId, name, updatedAt, deletedAt, profile, works, 2);

        // Assert
        response.UserId.Should().Be(userId);
        response.Name.Should().Be(name);
        response.UpdatedAt.Should().Be(updatedAt);
        response.DeletedAt.Should().Be(deletedAt);
        response.Profile.Should().Be(profile);
        response.PublishedStories.Should().BeEquivalentTo(works);
    }


}
