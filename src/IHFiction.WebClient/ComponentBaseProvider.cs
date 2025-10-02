using System.Reflection;

using Sidio.Sitemap.Blazor;

namespace IHFiction.WebClient;

internal sealed class ComponentBaseProvider : IComponentBaseProvider
{
    public IReadOnlyCollection<Type> GetComponentBaseTypes()
    {
        List<Assembly?> assemblies = [Assembly.GetEntryAssembly(), typeof(SharedWeb._Imports).Assembly];

        return [.. assemblies.Where(a => a is not null)
        .SelectMany(a => a!.GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Components.ComponentBase).IsAssignableFrom(t)))
            ];
    }
}