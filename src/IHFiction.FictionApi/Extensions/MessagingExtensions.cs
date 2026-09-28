using IHFiction.Data;
using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.FictionApi.Notifications;
using IHFiction.FictionApi.Stories;

using JasperFx.CodeGeneration.Model;
using JasperFx.Resources;

using StackExchange.Redis;

using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Persistence;
using Wolverine.Postgresql;
using Wolverine.Redis;

namespace IHFiction.FictionApi.Extensions;

internal static class MessagingExtensions
{
    public static WebApplicationBuilder AddMessagingServices(
        this WebApplicationBuilder builder,
        bool isBuildEnvironment)
    {
        if (isBuildEnvironment)
        {
            return builder;
        }

        // Resource setup is a blocking Wolverine hosted service. Gate it on Redis readiness so
        // a rolling Swarm restart cannot terminate the API while Redis is being rescheduled.
        builder.Services.AddHostedService<RedisStartupReadinessService>();

        var fictionDbConnectionString = builder.Configuration.GetConnectionString("fiction-db")
            ?? throw new InvalidOperationException("The fiction-db connection string is required for Wolverine persistence.");

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.AddSource("Wolverine");
            });

        ConfigureWolverine(builder.Host, fictionDbConnectionString);

        return builder;
    }

    private static void ConfigureWolverine(IHostBuilder host, string fictionDbConnectionString)
    {
        host.UseWolverine(opts =>
        {
            // NotificationFanoutHandler is internal — Wolverine only scans exported (public) types by default,
            // so we must register it explicitly.
            opts.Discovery.IncludeType<NotificationFanoutHandler>();

            opts.CodeGeneration.AlwaysUseServiceLocationFor<FictionDbContext>();
            opts.ServiceLocationPolicy = ServiceLocationPolicy.NotAllowed;

            // PostgreSQL is the durable store for Wolverine's inbox, outbox, and
            // durable local queues. Redis Streams is the cross-instance transport.
            opts.PersistMessagesWithPostgresql(fictionDbConnectionString, Schemas.Wolverine);
            // Handlers that need resilient multi-operation transactions create them
            // through EF's execution strategy. Lightweight mode avoids opening an
            // unsupported user transaction around those handlers.
            opts.UseEntityFrameworkCoreTransactions(TransactionMiddlewareMode.Lightweight);

            opts.UseRedisTransport((sp) => sp.GetRequiredService<IConnectionMultiplexer>())
                .AutoProvision()
                .ConfigureDefaultConsumerName((runtime, _) => $"{runtime.Options.ServiceName}-{runtime.DurabilitySettings.AssignedNodeNumber}")
                .DeleteStreamEntryOnAck(true);

            const string notificationStream = "ihfiction-notifications";
            opts.PublishMessage<StoryPublishedNotificationRequested>()
                .ToRedisStream(notificationStream)
                .UseDurableOutbox();
            opts.PublishMessage<StoryCompletedNotificationRequested>()
                .ToRedisStream(notificationStream)
                .UseDurableOutbox();
            opts.PublishMessage<ChapterPublishedNotificationRequested>()
                .ToRedisStream(notificationStream)
                .UseDurableOutbox();

            opts.ListenToRedisStream(notificationStream, "fiction-notification-fanout")
                .StartFromBeginning()
                .EnableNativeDeadLetterQueue()
                .UseDurableInbox();

            const string tagStream = "ihfiction-tags";
            opts.PublishMessage<TagCreatedRequested>()
                .ToRedisStream(tagStream)
                .UseDurableOutbox();
            opts.ListenToRedisStream(tagStream, "fiction-tag-reconciler")
                .StartFromBeginning()
                .EnableNativeDeadLetterQueue()
                .UseDurableInbox();

            const string workReadStream = "ihfiction-work-reads";
            opts.PublishMessage<RecordWorkReadRequested>()
                .ToRedisStream(workReadStream)
                .UseDurableOutbox();
            opts.ListenToRedisStream(workReadStream, "fiction-work-read-recorder")
                .StartFromBeginning()
                .EnableNativeDeadLetterQueue()
                .UseDurableInbox();

            // Any local queues added later inherit PostgreSQL durability instead of
            // silently becoming process-memory-only queues.
            opts.Policies.UseDurableLocalQueues();

            // Wolverine 6.x provisions its own PostgreSQL envelope tables and the
            // Redis stream/consumer group through the unified resource setup model.
            opts.Services.AddResourceSetupOnStartup();
        });
    }
}
