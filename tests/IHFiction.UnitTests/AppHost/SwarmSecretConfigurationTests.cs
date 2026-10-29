using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace IHFiction.UnitTests.AppHost;

public sealed class SwarmSecretConfigurationTests
{
    [Fact]
    public void ServiceDefaults_LoadsNamedConnectionsAndMultilineSigningKey_FromMountedSecretFiles()
    {
        var directory = Directory.CreateTempSubdirectory("ihfiction-swarm-secrets-");
        try
        {
            using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var pem = signingKey.ExportECPrivateKeyPem();
            var secrets = new Dictionary<string, string>
            {
                ["ConnectionStrings__fiction-db"] = "Host=database;Database=fiction-db;Username=test;Password=fixture",
                ["ConnectionStrings__stories-db"] = "mongodb://test:fixture@documents/stories-db",
                ["ConnectionStrings__redis"] = "cache,password=fixture",
                ["AgentAuth__SigningKeyPem"] = pem
            };
            foreach (var (name, value) in secrets)
                File.WriteAllText(Path.Combine(directory.FullName, name), value);

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Configuration["SecretsPath"] = directory.FullName;
            builder.AddServiceDefaults();

            builder.Configuration.GetConnectionString("fiction-db").Should().Be(secrets["ConnectionStrings__fiction-db"]);
            builder.Configuration.GetConnectionString("stories-db").Should().Be(secrets["ConnectionStrings__stories-db"]);
            builder.Configuration.GetConnectionString("redis").Should().Be(secrets["ConnectionStrings__redis"]);
            using var loaded = ECDsa.Create();
            loaded.ImportFromPem(builder.Configuration["AgentAuth:SigningKeyPem"]);
            loaded.ExportSubjectPublicKeyInfo().Should().Equal(signingKey.ExportSubjectPublicKeyInfo());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
