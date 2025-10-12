namespace IHFiction.FictionApi.Infrastructure;

internal interface IEndpoint
{
    RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder);
    string Name { get; }
}
