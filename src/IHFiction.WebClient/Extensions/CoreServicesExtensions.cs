using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Notifications;
using IHFiction.SharedWeb.Components.Disqus;
using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Csp;
using IHFiction.SharedWeb.Reporting;
using IHFiction.SharedWeb.Sitemap;
using IHFiction.WebClient.MarkdownResponses;

using Markdig;

using Sidio.Sitemap.Blazor;
using Sidio.Sitemap.Core.Services;

namespace IHFiction.WebClient.Extensions;

internal static class CoreServicesExtensions
{
    public static IHostApplicationBuilder AddCoreWebServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddOptions<WebPushOptions>()
            .Bind(builder.Configuration.GetSection("WebPush"))
            .Validate(options =>
                !string.IsNullOrWhiteSpace(options.PublicKey),
                "WebPush options must have PublicKey configured.");

        builder.Services.AddOptions<SiteUrlOptions>()
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

        builder.Services.AddOptions<ApiUrlOptions>()
            .Configure(options =>
            {
                var configuredBaseUrl = builder.Configuration["ApiBaseUrl"];
                options.BaseUrl = Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var uri) ? uri : null;
            })
            .Validate(
                options => options.BaseUrl is { IsAbsoluteUri: true }
                    && (options.BaseUrl.Scheme == Uri.UriSchemeHttps || options.BaseUrl.Scheme == Uri.UriSchemeHttp),
                "ApiBaseUrl must be an absolute HTTP(S) URL.")
            .ValidateOnStart();

        builder.Services.AddOptions<DisqusOptions>()
            .Bind(builder.Configuration.GetSection(DisqusOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ShortName), "Disqus ShortName must be configured.")
            .ValidateOnStart();

        builder.Services.AddSingleton<IComponentBaseProvider, ComponentBaseProvider>();

        builder.Services.AddRoutingCore()
            .Configure<RouteOptions>(options => options.SetParameterPolicy<UlidRouteConstraint>("ulid"));

        builder.Services.AddDefaultSitemapServices<HttpContextBaseUrlProvider>()
            .AddCustomSitemapNodeProvider<DynamicSitemapNodeProvider>();

        builder.Services.AddCspReportStorage();
        builder.Services.AddCspProvider();

        builder.Services.AddSingleton(new MarkdownPipelineBuilder()
            .UseEmphasisExtras()
            .Build());

        builder.Services.AddSingleton(VersionHelper.Get());
        builder.Services.AddSingleton<HtmlToMarkdownConverter>();

        builder.Services.AddRequestTimeouts();
        builder.Services.AddOutputCache(options =>
        {
            // Apply to middleware-generated sitemap response
            options.AddBasePolicy(policy => policy
                .With(ctx =>
                    HttpMethods.IsGet(ctx.HttpContext.Request.Method) &&
                    ctx.HttpContext.Request.Path.Equals("/sitemap.xml", StringComparison.OrdinalIgnoreCase))
                .Expire(TimeSpan.FromHours(1))
                .Tag("sitemap"));

            options.AddPolicy("Robots", policy => policy
                .Expire(TimeSpan.FromHours(6))
                .SetVaryByHost(false)
                .Tag("robots"));
        });

        return builder;
    }
}
