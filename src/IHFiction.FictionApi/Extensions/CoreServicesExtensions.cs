using System.Text.Json;
using System.Threading.RateLimiting;

using Cysharp.Serialization.Json;

using IHFiction.Data.Infrastructure;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Extensions;

internal static class CoreServicesExtensions
{
    public static IHostApplicationBuilder AddCoreApiServices(this IHostApplicationBuilder builder, TimeProvider timeProvider)
    {
        builder.Services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions.Add("requestId", context.HttpContext.TraceIdentifier));

        builder.Services.AddValidation(); // .NET 10 built-in validation support for minimal APIs
        builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
        builder.Services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        builder.Services.AddRoutingCore()
            .Configure<RouteOptions>(options => options.SetParameterPolicy<UlidRouteConstraint>("ulid"));

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddRateLimiter(options => options.AddPolicy("qualified-reads", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                })));

        // Configure JSON serialization for AOT compatibility
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.WriteIndented = builder.Environment.IsDevelopment();

            // Add custom converters (these work with AOT)
            options.SerializerOptions.Converters.Add(new UlidJsonConverter());
            options.SerializerOptions.Converters.Add(new ObjectIdJsonConverter());
            options.SerializerOptions.Converters.Add(new LinkedConverterFactory());

            // Use source-generated JSON context for AOT compatibility
            // Combine with default resolver to support types not yet in the context
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, FictionApiJsonSerializerContext.Default);
        });

        // Configure the API's canonical public URL.
        builder.Services.AddOptions<BaseUrlOptions>()
            .Configure(options =>
            {
                var configuredBaseUrl = builder.Configuration["BaseUrl"];
                options.BaseUrl = Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var uri) ? uri : null;
            })
            .Validate(
                options => options.BaseUrl is { IsAbsoluteUri: true }
                    && (options.BaseUrl.Scheme == Uri.UriSchemeHttps || options.BaseUrl.Scheme == Uri.UriSchemeHttp),
                "BaseUrl must be an absolute HTTP(S) URL.")
            .ValidateOnStart();

        // Configure CORS
        if (builder.Environment.IsProduction())
        {
            string[] allowedOrigins = [.. (builder.Configuration["AllowedOrigins"]
                ?? throw new InvalidOperationException("AllowedOrigins configuration is required in production"))
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

            builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
                .WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()));
        }

        builder.Services.AddSingleton(timeProvider);
        builder.Services.AddSingleton(FictionApiJsonSerializerContext.Default);

        builder.Services.AddRequestTimeouts();
        builder.Services.AddOutputCache();

        return builder;
    }
}
