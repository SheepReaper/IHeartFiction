using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.SharedWeb.Services;

public class AccountService(IFictionApiClient client)
{
    public async ValueTask<Result<LinkedOfRegisterAsAuthorResponse>> RegisterAsAuthorAsync(
        RegisterAsAuthorBody body,
        string? fields = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.RegisterAsAuthorAsync(body, fields, cancellationToken);
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
        CancellationToken cancellationToken = default)
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

    public async ValueTask<Result<LinkedOfGetCurrentAuthorStoryContentResponse>> GetCurrentAuthorStoryContentAsync(
        string storyId,
        string? fields = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await client.GetCurrentAuthorStoryContentAsync(storyId, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result<LinkedOfGetCurrentAuthorChapterContentResponse>> GetCurrentAuthorChapterContentAsync(
        string chapterId,
        string? fields = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await client.GetCurrentAuthorChapterContentAsync(chapterId, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }
}