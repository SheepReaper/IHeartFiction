using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.SharedWeb.Services;

public class ChapterService(IFictionApiClient client)
{
    public async ValueTask<Result<LinkedOfGetCurrentAuthorChapterContentResponse>> GetCurrentAuthorChapterContentAsync(
        string chapterId,
        string? fields = null,
        CancellationToken cancellationToken = default)
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
    public async ValueTask<Result<LinkedOfGetChapterContentResponse>> GetChapterContentAsync(
        string chapterId,
        string? fields = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.GetChapterContentAsync(chapterId, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result<LinkedOfAddChapterToStoryResponse>> AddChapterToStoryAsync(
        string storyId,
        AddChapterToStoryBody body,
        string? fields = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.AddChapterToStoryAsync(storyId, body, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result> DeleteChapterAsync(
        string chapterId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.DeleteChapterAsync(chapterId, cancellationToken);
            return Result.Success();
        }
        catch (ApiException ex)
        {
            return ex;
        }
    }

    public async ValueTask<Result<LinkedOfUpdateChapterMetadataResponse>> UpdateChapterMetadataAsync(
        string chapterId,
        UpdateChapterMetadataBody body,
        string? fields = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.UpdateChapterMetadataAsync(chapterId, body, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result<LinkedOfUpdateChapterContentResponse>> UpdateChapterContentAsync(
        string chapterId,
        UpdateChapterContentBody body,
        string? fields = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.UpdateChapterContentAsync(chapterId, body, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }
}