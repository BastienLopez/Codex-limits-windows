using Application = System.Windows.Application;
using StartupEventArgs = System.Windows.StartupEventArgs;

namespace CodexLimits.App;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var demo = e.Args.Any(argument => argument.Equals("--demo", StringComparison.OrdinalIgnoreCase));
        MainWindow = new MainWindow(demo);
        MainWindow.Show();
    }
}
