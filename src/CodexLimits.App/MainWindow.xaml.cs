using System.ComponentModel;
using System.IO;
using CodexLimits.Core;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Visibility = System.Windows.Visibility;
using Window = System.Windows.Window;
using Forms = System.Windows.Forms;

namespace CodexLimits.App;

public partial class MainWindow : Window, IDisposable
{
    private readonly MainViewModel _viewModel;
    private readonly SettingsStore _settingsStore = new();
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly System.Drawing.Image? _trayBaseImage;
    private System.Drawing.Icon? _trayRenderedIcon;
    private readonly Forms.ToolStripMenuItem _showMenuItem;
    private readonly Forms.ToolStripMenuItem _refreshMenuItem;
    private readonly Forms.ToolStripMenuItem _settingsMenuItem;
    private readonly Forms.ToolStripMenuItem _exitMenuItem;
    private readonly System.Windows.Threading.DispatcherTimer _heartbeatTimer;
    private DateTimeOffset _nextRefreshAt;
    private bool _exitRequested;

    public MainWindow(bool demo)
    {
        InitializeComponent();

        var settings = _settingsStore.Load();
        _viewModel = new MainViewModel(demo, settings);
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        var menu = new Forms.ContextMenuStrip();
        _showMenuItem = new Forms.ToolStripMenuItem();
        _refreshMenuItem = new Forms.ToolStripMenuItem();
        _settingsMenuItem = new Forms.ToolStripMenuItem();
        _exitMenuItem = new Forms.ToolStripMenuItem();

        _showMenuItem.Click += (_, _) => Dispatcher.Invoke(ShowFromTray);
        _refreshMenuItem.Click += (_, _) => Dispatcher.Invoke(() => _ = RefreshAsync());
        _settingsMenuItem.Click += (_, _) => Dispatcher.Invoke(ShowSettings);
        _exitMenuItem.Click += (_, _) => Dispatcher.Invoke(RequestExit);

        menu.Items.Add(_showMenuItem);
        menu.Items.Add(_refreshMenuItem);
        menu.Items.Add(_settingsMenuItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_exitMenuItem);

        _trayBaseImage = TrayIconRenderer.LoadBaseImage();
        _trayRenderedIcon = TrayIconRenderer.CreateTrayIcon(_trayBaseImage);
        Icon = TrayIconRenderer.CreateWindowIcon(_trayBaseImage);

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _trayRenderedIcon,
            Visible = true,
            Text = "Codex Limits Windows",
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);

        _heartbeatTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _heartbeatTimer.Tick += HeartbeatTimer_Tick;

        UpdateLocalizedShellText();
    }

    private async void Window_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        await _viewModel.StartAsync();
        UpdateChart();
        ConfigureNextRefresh();
        _heartbeatTimer.Start();
    }

    private async void RefreshButton_Click(object sender, System.Windows.RoutedEventArgs e) =>
        await RefreshAsync();

    private void SettingsButton_Click(object sender, System.Windows.RoutedEventArgs e) => ShowSettings();

    private async void HeartbeatTimer_Tick(object? sender, EventArgs e)
    {
        _viewModel.UpdateClock();
        if (DateTimeOffset.Now < _nextRefreshAt) return;

        if (_viewModel.CanAutoRefreshNow)
        {
            await RefreshAsync();
        }
        else
        {
            ConfigureNextRefresh();
        }
    }

    private async Task RefreshAsync()
    {
        await _viewModel.RefreshAsync();
        UpdateChart();
        ConfigureNextRefresh();
    }

    private void ShowSettings()
    {
        ShowFromTray();
        var dialog = new SettingsWindow(_viewModel.Settings) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.ResultSettings is null) return;

        try
        {
            _settingsStore.Save(dialog.ResultSettings);
            _viewModel.ApplySettings(dialog.ResultSettings);
            UpdateLocalizedShellText();
            UpdateChart();
            ConfigureNextRefresh();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var message = UiText.Get(
                _viewModel.Settings.Language,
                $"Impossible d’enregistrer les paramètres : {exception.Message}",
                $"Unable to save settings: {exception.Message}");

            MessageBox.Show(
                this,
                message,
                "Codex Limits Windows",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void UpdateLocalizedShellText()
    {
        var language = _viewModel.Settings.Language;
        _showMenuItem.Text = UiText.Get(language, "Afficher", "Show");
        _refreshMenuItem.Text = UiText.Get(language, "Actualiser", "Refresh");
        _settingsMenuItem.Text = UiText.Get(language, "Paramètres…", "Settings…");
        _exitMenuItem.Text = UiText.Get(language, "Quitter", "Exit");
        UpdateTrayText();
    }

    private void ConfigureNextRefresh()
    {
        var now = DateTimeOffset.Now;
        _nextRefreshAt = _viewModel.CanAutoRefreshNow
            ? now.AddMinutes(_viewModel.RefreshIntervalMinutes)
            : ScheduleMath.GetNextStart(now, _viewModel.Settings);
        _viewModel.UpdateClock();
    }

    private void UpdateChart()
    {
        BurnChart.UiLanguage = _viewModel.Settings.Language;
        BurnChart.Settings = _viewModel.Settings;
        BurnChart.Data = _viewModel.Chart;
        BurnChart.InvalidateVisual();
    }

    private void UpdateTrayText()
    {
        var trayText = UiText.Get(
            _viewModel.Settings.Language,
            $"Codex : {_viewModel.RemainingText} % restant | Aujourd'hui : {_viewModel.AvailableTodayPercent:0} % utilisables",
            $"Codex: {_viewModel.RemainingText}% remaining | Today: {_viewModel.AvailableTodayPercent:0}% available");

        _trayIcon.Text = trayText.Length <= 63 ? trayText : trayText[..63];

    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Chart))
        {
            UpdateChart();
        }

        if (e.PropertyName is nameof(MainViewModel.RemainingText) or nameof(MainViewModel.AvailableTodayPercent))
        {
            UpdateTrayText();
        }

        if (e.PropertyName is nameof(MainViewModel.ErrorText) or nameof(MainViewModel.HasError))
        {
            ErrorPanel.Visibility = _viewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void HideButton_Click(object sender, System.Windows.RoutedEventArgs e) => HideToTray();

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (!_exitRequested && WindowState == System.Windows.WindowState.Minimized)
        {
            Dispatcher.BeginInvoke(new Action(HideToTray));
        }
    }


    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested) return;
        e.Cancel = true;
        HideToTray();
    }

    public void StartHidden()
    {
        ShowInTaskbar = false;
        WindowState = System.Windows.WindowState.Minimized;
        Show();
        Hide();
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        WindowState = System.Windows.WindowState.Normal;
        Show();
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
        _heartbeatTimer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayRenderedIcon?.Dispose();
        _trayBaseImage?.Dispose();
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        GC.SuppressFinalize(this);
    }
}

