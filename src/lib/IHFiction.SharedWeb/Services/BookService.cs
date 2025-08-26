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
}
