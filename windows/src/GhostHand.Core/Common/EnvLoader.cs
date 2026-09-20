namespace HolyHand.Core.Common;

/// <summary>
/// Lightweight helper to load key-value pairs from a local .env file into the current process environment.
/// Does not overwrite existing environment variables.
/// </summary>
public static class EnvLoader
{
    public static void Load(string? directoryPath = null)
    {
        var searchDir = directoryPath ?? Directory.GetCurrentDirectory();
        
        // Check current directory, then parent directories up to 3 levels
        string? envPath = null;
        var dir = new DirectoryInfo(searchDir);
        for (int i = 0; i < 4 && dir != null; i++)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
            {
                envPath = candidate;
                break;
            }
            dir = dir.Parent;
        }

        if (envPath == null || !File.Exists(envPath))
            return;

        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                continue;

            var splitIndex = trimmed.IndexOf('=');
            if (splitIndex <= 0)
                continue;

            var key = trimmed[..splitIndex].Trim();
            var value = trimmed[(splitIndex + 1)..].Trim();

            // Strip surrounding quotes if present
            if ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                value = value[1..^1];
            }

            // Only set if not already present in environment
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
            }
        }
    }
}
