namespace IHFiction.AppHost.Extensions;

internal static class SwarmEnvironment
{
    public static void RemoveConnectionVariables(IDictionary<string, string> environment, params string[] connectionNames)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(connectionNames);
        foreach (var name in connectionNames)
        {
            var normalized = name.Replace('-', '_');
            var prefix = normalized + "_";
            // Aspire emits both original and normalized connection names, plus component properties.
            foreach (var key in environment.Keys.Where(key =>
                key.Equals($"ConnectionStrings__{name}", StringComparison.OrdinalIgnoreCase)
                || key.Equals($"ConnectionStrings__{normalized}", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
                environment.Remove(key);
        }
    }

    public static void ValidateNoInterpolation(string serviceName, IEnumerable<KeyValuePair<string, string>> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        foreach (var (key, value) in environment)
        {
            if (HasInterpolation(value))
                throw new InvalidOperationException($"Swarm service '{serviceName}' environment variable '{key}' requires interpolation. Supply a literal value or a mounted secret instead.");
        }
    }

    private static bool HasInterpolation(string value)
    {
        var offset = 0;
        while (offset + 1 < value.Length)
        {
            if (value[offset++] != '$') continue;
            var next = value[offset++];
            if (next == '$') continue; // Compose escapes a literal dollar with $$.
            if (next == '{' || next == '_' || char.IsAsciiLetter(next)) return true;
        }
        return false;
    }
}
