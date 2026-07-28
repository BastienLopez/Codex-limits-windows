using System.Windows;
using Application = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace CodexLimits.App;

public partial class App : Application
{
    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        var background = e.Args.Any(argument => argument.Equals("--background", StringComparison.OrdinalIgnoreCase));

        try
        {
            if (!await PrerequisiteBootstrapper.EnsureReadyAsync())
            {
                Shutdown();
                return;
            }
        }
        catch (Exception exception)
        {
            WpfMessageBox.Show(
                $"La vérification de Codex CLI a échoué.\n\nDétail : {exception.Message}",
                "Codex Limits Windows",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        MainWindow = new MainWindow();

        if (background)
        {
            ((MainWindow)MainWindow).StartHidden();
            return;
        }

        MainWindow.Show();
    }
}
