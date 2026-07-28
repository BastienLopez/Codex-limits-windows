using System.Windows;
using Application = System.Windows.Application;

namespace CodexLimits.App;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var demo = e.Args.Any(argument => argument.Equals("--demo", StringComparison.OrdinalIgnoreCase));
        var background = e.Args.Any(argument => argument.Equals("--background", StringComparison.OrdinalIgnoreCase));

        MainWindow = new MainWindow(demo);

        if (background)
        {
            ((MainWindow)MainWindow).StartHidden();
            return;
        }

        MainWindow.Show();
    }
}
