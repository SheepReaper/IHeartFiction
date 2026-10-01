using FluentAssertions;

using IHFiction.SharedWeb.Components;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IHFiction.UnitTests.SharedWeb;

public sealed class AppBreadcrumbsRenderingTests
{
    [Fact]
    public async Task RenderAsync_WithEmptyItems_RendersEmptyString()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<AppBreadcrumbs>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(AppBreadcrumbs.Items)] = Array.Empty<BreadcrumbItem>()
                }));
            return component.ToHtmlString();
        });

        html.Should().BeEmpty();
    }

    [Fact]
    public async Task RenderAsync_WithItems_RendersAccessibleBulmaBreadcrumbStructure()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        var items = new BreadcrumbItem[]
        {
            new("Home", "/"),
            new("Stories", "/stories"),
            new("A Great Tale", null, IsActive: true)
        };

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<AppBreadcrumbs>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(AppBreadcrumbs.Items)] = items
                }));
            return component.ToHtmlString();
        });

        html.Should().Contain("class=\"breadcrumb has-succeeds-separator mb-4\"");
        html.Should().Contain("aria-label=\"breadcrumbs\"");
        html.Should().Contain("<a href=\"/\">Home</a>");
        html.Should().Contain("<a href=\"/stories\">Stories</a>");
        html.Should().Contain("class=\"is-active\"");
        html.Should().Contain("aria-current=\"page\">A Great Tale</a>");
    }
}
