using System.Reflection;

using Microsoft.Extensions.DependencyInjection.Extensions;

using IHFiction.FictionApi.Common;

namespace IHFiction.FictionApi.Extensions;

internal static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services) => services.AddEndpoints(Assembly.GetExecutingAssembly());

    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var descriptors = assembly
            .DefinedTypes
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(IEndpoint)))
            .Select(t => ServiceDescriptor.Transient(typeof(IEndpoint), t));

        services.TryAddEnumerable(descriptors);

        return services;
    }

    public static IApplicationBuilder MapEndpoints(this WebApplication app)
    {
        foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>()) endpoint
            .MapEndpoint(app)
            .WithName(endpoint.Name);

        return app;
    }

    /// <summary>
    /// Registers all use case classes that implement IUseCase with the dependency injection container.
    /// Automatically discovers use case classes in the executing assembly and registers them as scoped services.
    /// </summary>
    /// <param name="services">The service collection to add use cases to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddUseCases(this IServiceCollection services) => services.AddUseCases(Assembly.GetExecutingAssembly());

    /// <summary>
    /// Registers all use case classes that implement IUseCase with the dependency injection container.
    /// Automatically discovers use case classes in the specified assembly and registers them as scoped services.
    /// </summary>
    /// <param name="services">The service collection to add use cases to.</param>
    /// <param name="assembly">The assembly to scan for use case classes.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddUseCases(this IServiceCollection services, Assembly assembly)
    {
        var useCaseTypes = assembly
            .DefinedTypes
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(IUseCase)))
            .ToList();

        foreach (var useCaseType in useCaseTypes)
        {
            // Register each use case as itself (not as IUseCase interface)
            // This allows direct injection of the concrete type in endpoints
            services.AddScoped(useCaseType);
        }

        return services;
    }
}