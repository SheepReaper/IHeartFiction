namespace IHFiction.FictionApi.Common;

internal interface IEndpoint
{
    RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder);
    string Name { get; }
}
