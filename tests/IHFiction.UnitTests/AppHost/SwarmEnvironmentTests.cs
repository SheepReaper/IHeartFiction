using FluentAssertions;
using IHFiction.AppHost.Extensions;

namespace IHFiction.UnitTests.AppHost;

public sealed class SwarmEnvironmentTests
{
    [Fact]
    public void ConnectionCleanup_RemovesBothAliasesAndDerivedProperties_PreservingServiceDiscovery()
    {
        var environment = new Dictionary<string, string>
        {
            ["ConnectionStrings__fiction-db"] = "legacy",
            ["ConnectionStrings__fiction_db"] = "${POSTGRES_PASSWORD}",
            ["FICTION_DB_JDBCCONNECTIONSTRING"] = "unused",
            ["FICTION_DB_HOST"] = "unused",
            ["ConnectionStrings__stories_db"] = "${MONGO_PASSWORD}",
            ["STORIES_DB_PASSWORD"] = "unused",
            ["ConnectionStrings__redis"] = "unused",
            ["REDIS_URI"] = "unused",
            ["FICTION_HTTP"] = "http://fiction:8080",
            ["services__fiction__http__0"] = "http://fiction:8080"
        };
        SwarmEnvironment.RemoveConnectionVariables(environment, "fiction-db", "stories-db", "redis");
        environment.Keys.Should().BeEquivalentTo("FICTION_HTTP", "services__fiction__http__0");
    }

    [Theory]
    [InlineData("${POSTGRES_PASSWORD}", true)]
    [InlineData("Host=db;Password=${PASSWORD};", true)]
    [InlineData("$TOKEN", true)]
    [InlineData("$$$TOKEN", true)]
    [InlineData("/run/secrets/connection", false)]
    [InlineData("http://fiction:8080", false)]
    [InlineData("$$(cat /run/secrets/password)", false)]
    public void Publication_RejectsHostInterpolation_WithoutPrintingSecretValues(string value, bool reject)
    {
        var action = () => SwarmEnvironment.ValidateNoInterpolation("fiction", new Dictionary<string, string> { ["Setting"] = value });
        if (reject)
            action.Should().Throw<InvalidOperationException>().WithMessage("*fiction*Setting*").Which.Message.Should().NotContain(value);
        else
            action.Should().NotThrow();
    }
}
