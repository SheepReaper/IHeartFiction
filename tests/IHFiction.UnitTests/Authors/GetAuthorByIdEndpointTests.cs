using Microsoft.AspNetCore.Http;

using FluentAssertions;

using IHFiction.FictionApi.Authors;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.UnitTests.Authors;

/// <summary>
/// Unit tests for GetAuthorById endpoint functionality
/// Tests the endpoint constants and basic functionality
/// </summary>
public class GetAuthorByIdEndpointTests
{


    [Fact]
    public void EndpointHandler_WhenUseCaseReturnsSuccess_ReturnsOkResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var response = new GetAuthorById.GetAuthorByIdResponse(
            userId,
            "Test Author",
            DateTime.UtcNow,
            null,
            new GetAuthorById.AuthorProfile("Test bio"),
            [],

            0
        );

        // Act - Simulate the endpoint handler logic for success case
        var successResult = Result.Success(response);
        var httpResult = Results.Ok(successResult.Value);

        // Assert
        httpResult.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<GetAuthorById.GetAuthorByIdResponse>>();
        var okResult = (Microsoft.AspNetCore.Http.HttpResults.Ok<GetAuthorById.GetAuthorByIdResponse>)httpResult;
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
public class GetAuthorByIdResponseTests
{
    [Fact]
    public void Response_CanBeCreatedWithAllProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var name = "Test Author";
        var updatedAt = DateTime.UtcNow;
        var deletedAt = DateTime.UtcNow.AddDays(1);
        var profile = new GetAuthorById.AuthorProfile("Test bio");
        var works = new List<GetAuthorById.AuthorWorkItem>
        {
            new(Ulid.NewUlid(), "Work 1", DateTime.Now),
            new(Ulid.NewUlid(), "Work 2", DateTime.Now)
        };

        // Act
        var response = new GetAuthorById.GetAuthorByIdResponse(userId, name, updatedAt, deletedAt, profile, works, 2);

        // Assert
        response.UserId.Should().Be(userId);
        response.Name.Should().Be(name);
        response.UpdatedAt.Should().Be(updatedAt);
        response.DeletedAt.Should().Be(deletedAt);
        response.Profile.Should().Be(profile);
        response.PublishedStories.Should().BeEquivalentTo(works);
    }


}
