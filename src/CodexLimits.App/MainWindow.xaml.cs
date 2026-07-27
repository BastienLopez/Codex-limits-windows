using System.ComponentModel;
using System.Drawing;
using CodexLimits.Core;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Window = System.Windows.Window;
using Forms = System.Windows.Forms;

namespace CodexLimits.App;

public partial class MainWindow : Window, IDisposable
{
    private readonly MainViewModel _viewModel;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly System.Windows.Threading.DispatcherTimer _refreshTimer;
    private readonly System.Windows.Threading.DispatcherTimer _clockTimer;
    private bool _exitRequested;
    private bool _lastScheduleActive;

    public MainWindow(bool demo)
    {
        InitializeComponent();
        _viewModel = new MainViewModel(demo);
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Afficher", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("Actualiser", null, (_, _) => Dispatcher.Invoke(() => _ = RefreshAsync()));
        menu.Items.Add("Planning…", null, (_, _) => Dispatcher.Invoke(() => { ShowFromTray(); _ = OpenSettingsAsync(); }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => Dispatcher.Invoke(RequestExit));

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Codex Limits Windows",
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);

        _refreshTimer = new System.Windows.Threading.DispatcherTimer();
        _refreshTimer.Tick += RefreshTimer_Tick;

        _clockTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _clockTimer.Tick += ClockTimer_Tick;
    }

    private async void Window_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        await _viewModel.StartAsync();
        RefreshChart();
        ConfigureRefreshTimer();
        _lastScheduleActive = _viewModel.IsAutomaticRefreshAllowed(DateTimeOffset.Now);
        _refreshTimer.Start();
        _clockTimer.Start();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        _viewModel.UpdateClock();
        if (_viewModel.IsAutomaticRefreshAllowed(DateTimeOffset.Now))
        {
            await RefreshAsync();
        }
    }

    private async void ClockTimer_Tick(object? sender, EventArgs e)
    {
        _viewModel.UpdateClock();
        var isActive = _viewModel.IsAutomaticRefreshAllowed(DateTimeOffset.Now);
        if (isActive && !_lastScheduleActive)
        {
            await RefreshAsync();
        }
        _lastScheduleActive = isActive;
    }

    private async void RefreshButton_Click(object sender, System.Windows.RoutedEventArgs e) => await RefreshAsync();

    private async void SettingsButton_Click(object sender, System.Windows.RoutedEventArgs e) => await OpenSettingsAsync();

    private async Task OpenSettingsAsync()
    {
        var dialog = new ScheduleSettingsWindow(_viewModel.Settings)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        await _viewModel.ApplySettingsAsync(dialog.Result);
        ConfigureRefreshTimer();
        _lastScheduleActive = _viewModel.IsAutomaticRefreshAllowed(DateTimeOffset.Now);
        RefreshChart();
    }

    private void ConfigureRefreshTimer()
    {
        _refreshTimer.Interval = TimeSpan.FromMinutes(Math.Max(_viewModel.RefreshIntervalMinutes, 5));
    }

    private async Task RefreshAsync()
    {
        await _viewModel.RefreshAsync();
        RefreshChart();
    }

    private void RefreshChart()
    {
        BurnChart.Data = _viewModel.Chart;
        BurnChart.InvalidateVisual();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Chart))
        {
            RefreshChart();
        }

        if (e.PropertyName == nameof(MainViewModel.RemainingText))
        {
            _trayIcon.Text = $"Codex : {_viewModel.RemainingText} % restant";
        }

        if (e.PropertyName is nameof(MainViewModel.ErrorText) or nameof(MainViewModel.HasError))
        {
            ErrorPanel.Visibility = _viewModel.HasError
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }
    }

    private void SafetyBufferBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            SafetyBufferBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
            System.Windows.Input.Keyboard.ClearFocus();
        }
    }

    private void HideButton_Click(object sender, System.Windows.RoutedEventArgs e) => Hide();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested) return;
        e.Cancel = true;
        Hide();
    }

    private void ShowFromTray()
    {
        Show();
        if (WindowState == System.Windows.WindowState.Minimized)
        {
            WindowState = System.Windows.WindowState.Normal;
        }
        Activate();
    }

    private void RequestExit()
    {
        _exitRequested = true;
        Dispose();
        Close();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _clockTimer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        GC.SuppressFinalize(this);
    }
}
