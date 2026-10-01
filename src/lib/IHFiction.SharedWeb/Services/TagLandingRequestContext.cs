using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.SharedWeb.Services;

public sealed class TagLandingRequestContext
{
    public string? RequestedSpec { get; set; }
    public Result<ResolveTagLandingResponse>? Resolution { get; set; }
}
