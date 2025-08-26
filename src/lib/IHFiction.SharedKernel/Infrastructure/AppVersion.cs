using System.Reflection;

namespace IHFiction.SharedKernel.Infrastructure;

public sealed record AppVersion(string Product, string Information, string Commit);

public static class VersionHelper
{
    public static AppVersion Get()
    {
        var asm = Assembly.GetExecutingAssembly();
        var product = asm.GetName().Version?.ToString() ?? "0.0.0";

        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? product;

        return new AppVersion(
            product,
            info,
            info.Split('+') is { Length: > 1 } parts ? parts[^1] : "");
    }
}
