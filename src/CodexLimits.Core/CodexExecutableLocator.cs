namespace CodexLimits.Core;

public static class CodexExecutableLocator
{
    public static string? Find() => FindCandidates().FirstOrDefault();

    public static IReadOnlyList<string> FindCandidates()
    {
        var candidates = new List<string>();

        static void AddCandidate(List<string> list, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var trimmed = path.Trim().Trim('"');
            if (!File.Exists(trimmed))
            {
                return;
            }

            if (!list.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(trimmed);
            }
        }

        AddCandidate(candidates, Environment.GetEnvironmentVariable("CODEX_LIMITS_CODEX_PATH"));

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathValue.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var fileName in new[] { "codex.exe", "codex.cmd", "codex.bat", "codex" })
            {
                AddCandidate(candidates, Path.Combine(directory.Trim('"'), fileName));
            }
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(codexHome))
        {
            codexHome = Path.Combine(home, ".codex");
        }

        foreach (var candidate in new[]
                 {
                     Path.Combine(localAppData, "Programs", "OpenAI", "Codex", "bin", "codex.exe"),
                     Path.Combine(codexHome, "packages", "standalone", "current", "bin", "codex.exe"),
                     Path.Combine(codexHome, "packages", "standalone", "current", "codex.exe"),
                     Path.Combine(codexHome, "bin", "codex.exe"),
                     Path.Combine(appData, "npm", "codex.cmd"),
                     Path.Combine(appData, "npm", "codex.exe"),
                     Path.Combine(localAppData, "Programs", "codex", "codex.exe"),
                     Path.Combine(home, ".local", "bin", "codex.exe"),
                     Path.Combine(home, ".local", "bin", "codex")
                 })
        {
            AddCandidate(candidates, candidate);
        }

        return candidates;
    }
}
