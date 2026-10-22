using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.SharedWeb.Services;

public sealed record MarkdownReaderModel(
    string WorkId,
    LinkedOfGetPublishedWorkContentResponse Content);

public sealed class MarkdownReaderService(IPublishedWorkReader workReader)
{
    private static readonly DomainError NoReadableContent = new(
        "MarkdownReader.NoReadableContent",
        "This work does not have any published readable content.");

    public async ValueTask<Result<MarkdownReaderModel>> ResolveAsync(
        string requestedWorkId,
        LinkedOfGetPublishedWorkMetaResponse requestedMeta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestedMeta);

        if (!requestedMeta.IsDirectlyReadable
            || !Ulid.TryParse(requestedWorkId, out var workId)
            || requestedMeta.Id != workId)
        {
            return NoReadableContent;
        }

        var contentResult = await workReader.GetPublishedWorkContentAsync(
            workId,
            cancellationToken: cancellationToken);
        if (contentResult.IsFailure)
        {
            return contentResult.DomainError;
        }

        return new MarkdownReaderModel(workId.ToString(), contentResult.Value);
    }
}
