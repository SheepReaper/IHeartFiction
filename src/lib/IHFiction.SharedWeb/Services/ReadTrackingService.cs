using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Services;

public sealed class ReadTrackingService(IFictionApiClient client, DeviceIdentityService deviceIdentity)
{
    public async Task<Result<RecordWorkReadResponse>> RecordQualifiedReadAsync(
        Ulid workId,
        int activeSeconds,
        CancellationToken cancellationToken = default)
    {
        var body = new RecordWorkReadBody
        {
            ActiveSeconds = activeSeconds,
            HasMeaningfulInteraction = true
        };

        return await client.RecordWorkReadAsync(workId, body, await deviceIdentity.GetOrCreateAsync(), cancellationToken)
            .HandleApiException();
    }
}
