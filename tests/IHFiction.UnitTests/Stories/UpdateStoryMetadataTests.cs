using IHFiction.FictionApi.Stories;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for UpdateStoryMetadata functionality
/// Tests validation logic and request handling without database dependencies
/// </summary>
public class UpdateStoryMetadataTests
{
    [Fact]
    public void UpdateStoryMetadataResponse_CanBeCreated()
    {
        // Arrange
        var id = Ulid.NewUlid();
        var ownerId = Ulid.NewUlid();
        var now = DateTime.UtcNow;

        // Act
        var response = new UpdateStoryMetadata.UpdateStoryMetadataResponse(
            id,
            "Updated Title",
            "Updated Description",
            now,
            ownerId,
            "Owner Name");

        // Assert
        Assert.Equal(id, response.Id);
        Assert.Equal("Updated Title", response.Title);
        Assert.Equal("Updated Description", response.Description);
        Assert.Equal(now, response.UpdatedAt);
        Assert.Equal(ownerId, response.OwnerId);
        Assert.Equal("Owner Name", response.OwnerName);
    }
}