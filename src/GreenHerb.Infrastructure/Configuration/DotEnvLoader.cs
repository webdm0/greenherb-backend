namespace GreenHerb.Infrastructure.Configuration;

public static class DotEnvLoader
{
    public static void LoadIfExists(string? baseDirectory = null)
    {
        var envPath = ResolveDotEnvPath(baseDirectory);
        if (envPath is null)
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(envPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
            {
                line = line["export ".Length..].Trim();
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            if (key.Length == 0 || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                continue;
            }

            var value = line[(separatorIndex + 1)..].Trim();
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            value = value
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\r", "\r", StringComparison.Ordinal);

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string? ResolveDotEnvPath(string? baseDirectory)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var normalizedBaseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? null
            : Path.GetFullPath(baseDirectory);

        var candidates = new[]
        {
            normalizedBaseDirectory is null ? null : Path.Combine(normalizedBaseDirectory, ".env"),
            normalizedBaseDirectory is null ? null : Path.Combine(normalizedBaseDirectory, "..", ".env"),
            Path.Combine(currentDirectory, ".env"),
            Path.Combine(AppContext.BaseDirectory, ".env"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env")
        }.Where(path => path is not null)
            .Select(path => Path.GetFullPath(path!))
            .Distinct()
            .ToArray();

        return candidates.FirstOrDefault(File.Exists);
    }
}
