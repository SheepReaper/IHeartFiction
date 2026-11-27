using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Infrastructure;

// Endpoint-owned mappings preserve domain error contracts when framework validation runs first.
internal sealed record ValidationErrorMapping(Func<HttpValidationProblemDetails, DomainError?> Resolve);
