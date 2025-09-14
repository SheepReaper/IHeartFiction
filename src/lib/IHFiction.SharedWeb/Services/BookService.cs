using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.SharedWeb.Services;

public class BookService(IFictionApiClient client)
{
    public async ValueTask<Result<LinkedOfGetCurrentAuthorBookContentResponse>> GetCurrentAuthorBookContentAsync(
        string bookId,
        string? fields = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await client.GetCurrentAuthorBookContentAsync(bookId, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result<LinkedOfUpdateBookMetadataResponse>> UpdateBookMetadataAsync(
        string bookId,
        UpdateBookMetadataBody body,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // The generated client expects (id, body, fields, cancellationToken), pass null for fields
            return await client.UpdateBookMetadataAsync(bookId, body, null, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }
}
