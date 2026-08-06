using System.Security.Claims;

using IHFiction.FictionApi.Stories;

using NSubstitute;

using Wolverine;

namespace IHFiction.UnitTests.Stories;

public sealed class RecordWorkReadDispatchTests
{
    [Fact]
    public async Task QualifiedAnonymousRead_PublishesDurableWorkReadRequest()
    {
        var bus = Substitute.For<IMessageBus>();
        var useCase = new RecordWorkRead(bus, TimeProvider.System);
        var workId = Ulid.NewUlid();

        var result = await useCase.HandleAsync(
            workId,
            new RecordWorkRead.RecordWorkReadBody(10, true),
            new ClaimsPrincipal(new ClaimsIdentity()),
            "valid-device-id",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        await bus.Received(1).PublishAsync(Arg.Is<RecordWorkReadRequested>(request =>
            request != null &&
            request.WorkId == workId
            && request.AuthenticatedUserId == null
            && request.DeviceId == "valid-device-id"
            && request.QualifiedAt != default));
    }

    [Fact]
    public async Task UnqualifiedRead_DoesNotPublishWorkReadRequest()
    {
        var bus = Substitute.For<IMessageBus>();
        var useCase = new RecordWorkRead(bus, TimeProvider.System);

        var result = await useCase.HandleAsync(
            Ulid.NewUlid(),
            new RecordWorkRead.RecordWorkReadBody(9, true),
            new ClaimsPrincipal(new ClaimsIdentity()),
            "valid-device-id",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        await bus.DidNotReceive().PublishAsync(Arg.Any<RecordWorkReadRequested>());
    }
}
