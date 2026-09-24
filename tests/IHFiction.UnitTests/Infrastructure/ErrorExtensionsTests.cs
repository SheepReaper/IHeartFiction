using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using FluentAssertions;

using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.UnitTests.Infrastructure;

public class ErrorExtensionsTests
{
    [Theory]
    [InlineData("MergeCanonicalTags.InvalidSelection", StatusCodes.Status400BadRequest)]
    [InlineData("MergeCanonicalTags.InvalidReason", StatusCodes.Status400BadRequest)]
    [InlineData("RenameCanonicalTag.InvalidTag", StatusCodes.Status400BadRequest)]
    public void ToProblemDetailsTypedResult_MapsTaxonomyErrorsToClientStatusCodes(
        string code,
        int expectedStatusCode)
    {
        var result = new DomainError(code, "Test error").ToProblemDetailsTypedResult();

        result.Should().BeOfType<ProblemHttpResult>();
        result.StatusCode.Should().Be(expectedStatusCode);
    }
}
