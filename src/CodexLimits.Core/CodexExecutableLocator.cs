namespace CodexLimits.Core;

public static class CodexExecutableLocator
{
    public static string? Find()
    {
        var overridePath = Environment.GetEnvironmentVariable("CODEX_LIMITS_CODEX_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var fileName in new[] { "codex.exe", "codex.cmd", "codex" })
            {
                var candidate = Path.Combine(directory.Trim('"'), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(appData, "npm", "codex.cmd"),
            Path.Combine(appData, "npm", "codex.exe"),
            Path.Combine(localAppData, "Programs", "codex", "codex.exe"),
            Path.Combine(home, ".local", "bin", "codex.exe"),
            Path.Combine(home, ".local", "bin", "codex")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
