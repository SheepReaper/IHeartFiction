using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.SharedWeb.Services;

public class StoryService(IFictionApiClient client)
{
    public async ValueTask<Result> ConvertStoryTypeAsync(
        string id,
        ConvertStoryTypeBody body,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await client.ConvertStoryTypeAsync(id, body, cancellationToken);
            return Result.Success();
        }
        catch (ApiException ex)
        {
            return ex;
        }
    }

    public async ValueTask<Result<LinkedPagedCollectionOfListStoryChaptersItem>> ListStoryChaptersAsync(
        string storyId,
        string? fields = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.ListStoryChaptersAsync(storyId, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result<LinkedOfGetPublishedStoryResponse>> GetStoryByIdAsync(
        string storyId,
        string? fields = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await client.GetPublishedStoryAsync(storyId, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result<LinkedOfGetPublishedStoryContentResponse>> GetPublishedStoryContentAsync(
        string storyId,
        string? fields = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await client.GetPublishedStoryContentAsync(storyId, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result<LinkedPagedCollectionOfListPublishedStoriesItem>> ListPublishedStoriesAsync(
        int? page = null,
        int? pageSize = null,
        string? search = null,
        string? sort = null,
        string? fields = null,
        ListPublishedStoriesBody? body = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await client.ListPublishedStoriesAsync(page, pageSize, search, sort, fields, body, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result<LinkedPagedCollectionOfAuthorStoryItem>> GetCurrentAuthorStoriesAsync(
        GetCurrentAuthorStoriesBody body,
        int? pageSize = null,
        int? page = null,
        string? search = null,
        string? sort = null,
        string? fields = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await client.GetCurrentAuthorStoriesAsync(pageSize, page, search, sort, fields, body, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result<LinkedOfPublishStoryResponse>> PublishStoryAsync(
        string id,
        string? fields = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await client.PublishStoryAsync(id, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result<LinkedOfUnpublishStoryResponse>> UnpublishStoryAsync(
        string id,
        string? fields = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await client.UnpublishStoryAsync(id, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result> DeleteStoryAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await client.DeleteStoryAsync(id, cancellationToken);
            return Result.Success();
        }
        catch (ApiException ex)
        {
            return ex;
        }
    }

    public async ValueTask<Result<LinkedOfCreateStoryResponse>> CreateStoryAsync(
        CreateStoryBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await client.CreateStoryAsync(body, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result<LinkedOfUpdateStoryMetadataResponse>> UpdateStoryMetadataAsync(
        string id,
        UpdateStoryMetadataBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await client.UpdateStoryMetadataAsync(id, body, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result<LinkedOfUpdateStoryContentResponse>> UpdateStoryContentAsync(
        string id,
        UpdateStoryContentBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await client.UpdateStoryContentAsync(id, body, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }
}
