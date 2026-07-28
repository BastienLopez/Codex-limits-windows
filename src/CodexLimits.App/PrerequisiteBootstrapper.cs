using System.Diagnostics;
using System.IO;
using System.Windows;
using CodexLimits.Core;
using WpfMessageBox = System.Windows.MessageBox;

namespace CodexLimits.App;

internal static class PrerequisiteBootstrapper
{
    private const string OfficialInstallerUrl = "https://chatgpt.com/codex/install.ps1";

    public static async Task<bool> EnsureReadyAsync()
    {
        RefreshProcessPath();

        var codexPath = CodexExecutableLocator.Find();
        if (!string.IsNullOrWhiteSpace(codexPath))
        {
            // Reprend exactement le fonctionnement historique : si Codex existe,
            // l'application le réutilise tel quel, sans contrôle de version ni de connexion.
            Environment.SetEnvironmentVariable("CODEX_LIMITS_CODEX_PATH", codexPath);
            return true;
        }

        var installChoice = WpfMessageBox.Show(
            "Codex CLI est nécessaire pour lire les quotas, mais aucune installation locale n’a été détectée.\n\n" +
            "Codex Limits Windows peut lancer l’installateur Windows officiel d’OpenAI. " +
            "Une connexion Internet est requise.\n\n" +
            "Installer Codex CLI maintenant ?",
            "Codex CLI manquant",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (installChoice != MessageBoxResult.Yes)
        {
            return false;
        }

        var installSucceeded = await RunOfficialCodexInstallerAsync();
        RefreshProcessPath();
        codexPath = CodexExecutableLocator.Find();

        if (!installSucceeded || string.IsNullOrWhiteSpace(codexPath))
        {
            WpfMessageBox.Show(
                "L’installation automatique de Codex CLI n’a pas abouti.\n\n" +
                "Relance l’application pour réessayer, ou installe Codex CLI depuis la documentation officielle d’OpenAI.",
                "Installation incomplète",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        Environment.SetEnvironmentVariable("CODEX_LIMITS_CODEX_PATH", codexPath);
        return true;
    }

    private static async Task<bool> RunOfficialCodexInstallerAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                WindowStyle = ProcessWindowStyle.Normal
            };

            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(
                "$ErrorActionPreference='Stop'; " +
                $"Invoke-RestMethod '{OfficialInstallerUrl}' | Invoke-Expression");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception exception)
        {
            WpfMessageBox.Show(
                $"Impossible de lancer l’installateur officiel Codex CLI.\n\nDétail : {exception.Message}",
                "Installation impossible",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    private static void RefreshProcessPath()
    {
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
        var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? string.Empty;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(codexHome))
        {
            codexHome = Path.Combine(home, ".codex");
        }

        var entries = new[]
        {
            Path.Combine(localAppData, "Programs", "OpenAI", "Codex", "bin"),
            Path.Combine(codexHome, "packages", "standalone", "current", "bin"),
            Path.Combine(codexHome, "packages", "standalone", "current"),
            Path.Combine(codexHome, "bin"),
            Path.Combine(appData, "npm"),
            currentPath,
            userPath,
            machinePath
        };

        var normalized = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .SelectMany(entry => entry.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator, normalized));
    }
}
