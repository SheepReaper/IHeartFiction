using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Services;
using System.Net;
using System.Text;

namespace IHFiction.UnitTests.SharedWeb;

public sealed class ApiExceptionTests
{
    [Fact]
    public async Task RenameConflict_ThroughGeneratedClient_PreservesExplanation()
    {
        using var handler = new ConflictHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example/") };
        var result = await new AdminService(new FictionApiClient(http)).RenameCanonicalTagAsync(
            Ulid.NewUlid(), "genre", null, "Drama", "Inline rename", TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal("RenameCanonicalTag.Conflict", result.DomainError.Code);
        Assert.Equal("That tag spelling already belongs to another tag family.", result.DomainError.Description);
    }

    private sealed class ConflictHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("{\"status\":409,\"domainError\":{\"code\":\"RenameCanonicalTag.Conflict\",\"description\":\"That tag spelling already belongs to another tag family.\"}}", Encoding.UTF8, "application/problem+json")
            });
    }
    [Fact]
    public void Conflict_PreservesActionableDomainError()
    {
        var exception = new ApiException("Conflict", 409,
            "{\"status\":409,\"domainError\":{\"code\":\"RenameCanonicalTag.Conflict\",\"description\":\"That tag spelling already belongs to another tag family.\"}}",
            new Dictionary<string, IEnumerable<string>>(), null);
        var error = exception.ToDomainError();
        Assert.Equal("RenameCanonicalTag.Conflict", error.Code);
        Assert.Equal("That tag spelling already belongs to another tag family.", error.Description);
    }
}
