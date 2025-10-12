using IHFiction.FictionApi.Infrastructure;

namespace IHFiction.FictionApi.Extensions;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3251:Implementations should be provided for \"partial\" methods", Justification = "Implementation is source-generated")]
internal static partial class EndpointExtensions
{
    /// <summary>
    /// Registers all endpoint implementations with the dependency injection container.
    /// This method uses source-generated registration for AOT compatibility.
    /// </summary>
    /// <param name="services">The service collection to add endpoints to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddEndpoints(this IServiceCollection services)
    {
        // Use the source-generated method instead of reflection
        services.AddGeneratedEndpoints();

        return services;
    }

    static partial void AddGeneratedEndpoints(this IServiceCollection services);

    /// <summary>
    /// Maps all registered endpoints to the application's request pipeline.
    /// </summary>
    /// <param name="app">The web application to configure.</param>
    /// <returns>The application builder for method chaining.</returns>
    public static IApplicationBuilder MapEndpoints(this WebApplication app)
    {
        foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint
                .MapEndpoint(app)
                .WithName(endpoint.Name);
        }

        return app;
    }

    /// <summary>
    /// Registers all use case classes that implement IUseCase with the dependency injection container.
    /// This method uses source-generated registration for AOT compatibility.
    /// </summary>
    /// <param name="services">The service collection to add use cases to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddUseCases(this IServiceCollection services)
    {
        // Use the source-generated method instead of reflection
        services.AddGeneratedUseCases();

        return services;
    }

    static partial void AddGeneratedUseCases(this IServiceCollection services);
}