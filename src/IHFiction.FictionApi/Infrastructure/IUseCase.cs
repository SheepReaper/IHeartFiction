namespace IHFiction.FictionApi.Infrastructure;

/// <summary>
/// Marker interface for use case classes to enable automatic service registration.
/// All use case classes should implement this interface to be automatically discovered
/// and registered with the dependency injection container as scoped services.
///
/// <para>
/// Use cases represent the application's business logic operations and are automatically
/// registered by the AddUseCases extension method.
/// This eliminates the need to manually register each use case in Program.cs.
/// </para>
///
/// <para>
/// Examples of use case classes: CreateStory, UpdateAuthorProfile, ListAuthors, etc.
/// Service classes like UserService, AuthorizationService should NOT implement this interface
/// as they are registered manually with specific lifetimes.
/// </para>
/// </summary>
internal interface IUseCase
{
}
