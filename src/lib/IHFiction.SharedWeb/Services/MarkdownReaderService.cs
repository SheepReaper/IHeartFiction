using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.SharedWeb.Services;

public sealed record MarkdownReaderModel(
    string WorkId,
    LinkedOfGetPublishedWorkMetaResponse Meta,
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

        var effectiveMeta = requestedMeta;
        var effectiveWorkId = requestedMeta.Id;

        if (!requestedMeta.IsDirectlyReadable)
        {
            var defaultWorkId = requestedMeta.DefaultReadableWorkId;
            if (defaultWorkId is null
                || !requestedMeta.ReadableChildren.Any(child => child.Id == defaultWorkId.Value))
            {
                return NoReadableContent;
            }

            var metaResult = await workReader.GetPublishedWorkMetaAsync(
                defaultWorkId.Value,
                cancellationToken: cancellationToken);
            if (metaResult.IsFailure)
            {
                return metaResult.DomainError;
            }

            effectiveMeta = metaResult.Value;
            effectiveWorkId = defaultWorkId.Value;
            if (!effectiveMeta.IsDirectlyReadable || effectiveMeta.Id != effectiveWorkId)
            {
                return NoReadableContent;
            }
        }

        var contentResult = await workReader.GetPublishedWorkContentAsync(
            effectiveWorkId,
            cancellationToken: cancellationToken);
        if (contentResult.IsFailure)
        {
            return contentResult.DomainError;
        }

        return new MarkdownReaderModel(effectiveWorkId.ToString(), effectiveMeta, contentResult.Value);
    }
}
