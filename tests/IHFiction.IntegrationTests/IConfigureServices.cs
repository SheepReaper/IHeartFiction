using Microsoft.Extensions.DependencyInjection;

namespace IHFiction.IntegrationTests;

internal interface IConfigureServices<T> where T : IConfigureServices<T>
{
    static abstract void ConfigureServices(IServiceCollection services);
}
