using System.ComponentModel;
using System.IO;
using System.Reflection;
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
    private readonly UiStateStore _uiStateStore = new();
    private UiState _uiState;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly System.Drawing.Image? _trayBaseImage;
    private System.Drawing.Icon? _trayRenderedIcon;
    private readonly Forms.ToolStripMenuItem _showMenuItem;
    private readonly Forms.ToolStripMenuItem _refreshMenuItem;
    private readonly Forms.ToolStripMenuItem _settingsMenuItem;
    private readonly Forms.ToolStripMenuItem _aboutMenuItem;
    private readonly Forms.ToolStripMenuItem _exitMenuItem;
    private readonly System.Windows.Threading.DispatcherTimer _heartbeatTimer;
    private DateTimeOffset _nextRefreshAt;
    private bool _exitRequested;

    public MainWindow()
    {
        InitializeComponent();

        _uiState = _uiStateStore.Load();
        var settings = _settingsStore.Load();
        _viewModel = new MainViewModel(settings);
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        var menu = new Forms.ContextMenuStrip();
        _showMenuItem = new Forms.ToolStripMenuItem();
        _refreshMenuItem = new Forms.ToolStripMenuItem();
        _settingsMenuItem = new Forms.ToolStripMenuItem();
        _aboutMenuItem = new Forms.ToolStripMenuItem();
        _exitMenuItem = new Forms.ToolStripMenuItem();

        _showMenuItem.Click += (_, _) => Dispatcher.Invoke(ShowFromTray);
        _refreshMenuItem.Click += (_, _) => Dispatcher.Invoke(() => _ = RefreshAsync());
        _settingsMenuItem.Click += (_, _) => Dispatcher.Invoke(ShowSettings);
        _aboutMenuItem.Click += (_, _) => Dispatcher.Invoke(ShowAbout);
        _exitMenuItem.Click += (_, _) => Dispatcher.Invoke(RequestExit);

        menu.Items.Add(_showMenuItem);
        menu.Items.Add(_refreshMenuItem);
        menu.Items.Add(_settingsMenuItem);
        menu.Items.Add(_aboutMenuItem);
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
        if (DateTimeOffset.Now < _nextRefreshAt)
        {
            return;
        }

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
        if (dialog.ShowDialog() != true || dialog.ResultSettings is null)
        {
            return;
        }

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

    private void ShowAbout()
    {
        var language = _viewModel.Settings.Language;
        var version = GetDisplayVersion();
        var message = UiText.Get(
            language,
            $"Codex Limits Windows {version}\n\n" +
            "Projet indépendant et non officiel. Il n’est ni affilié, ni approuvé, ni soutenu par OpenAI.\n\n" +
            "Les prévisions de quota sont indicatives et ne garantissent pas la disponibilité future du service. " +
            "Aucune télémétrie n’est envoyée par l’application ; l’historique reste local sur cet ordinateur.\n\n" +
            "Licence : MIT. Consulte README.md, PRIVACY.md et THIRD_PARTY_NOTICES.md dans le dossier de distribution.",
            $"Codex Limits Windows {version}\n\n" +
            "Independent, unofficial project. It is not affiliated with, endorsed by, or sponsored by OpenAI.\n\n" +
            "Quota forecasts are estimates and do not guarantee future service availability. " +
            "The app sends no telemetry; usage history stays local on this computer.\n\n" +
            "License: MIT. See README.md, PRIVACY.md and THIRD_PARTY_NOTICES.md in the distribution folder.");

        MessageBox.Show(
            this,
            message,
            UiText.Get(language, "À propos de Codex Limits Windows", "About Codex Limits Windows"),
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private static string GetDisplayVersion()
    {
        var assembly = typeof(MainWindow).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var separator = informational.IndexOf('+');
            return separator > 0 ? informational[..separator] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.6.4";
    }

    private void UpdateLocalizedShellText()
    {
        var language = _viewModel.Settings.Language;
        _showMenuItem.Text = UiText.Get(language, "Afficher", "Show");
        _refreshMenuItem.Text = UiText.Get(language, "Actualiser", "Refresh");
        _settingsMenuItem.Text = UiText.Get(language, "Paramètres…", "Settings…");
        _aboutMenuItem.Text = UiText.Get(language, "À propos…", "About…");
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
            $"Codex : {_viewModel.RemainingText} % restant | Aujourd’hui : {_viewModel.AvailableTodayPercent:0} % utilisables",
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
        if (_exitRequested)
        {
            return;
        }

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
        ShowBackgroundNoticeOnce();
        ShowInTaskbar = false;
        Hide();
    }

    private void ShowBackgroundNoticeOnce()
    {
        if (_uiState.BackgroundNoticeShown)
        {
            return;
        }

        var language = _viewModel.Settings.Language;
        var message = UiText.Get(
            language,
            "Codex Limits Windows continue de fonctionner en arrière-plan.\n\n" +
            "Double-clique sur son icône dans la zone de notification pour rouvrir la fenêtre. " +
            "Utilise le clic droit puis « Quitter » pour arrêter complètement l’application.\n\n" +
            "Ce message ne sera affiché qu’une seule fois.",
            "Codex Limits Windows keeps running in the background.\n\n" +
            "Double-click its notification-area icon to reopen the window. " +
            "Right-click it and choose “Exit” to stop the app completely.\n\n" +
            "This message is shown only once.");

        MessageBox.Show(
            message,
            "Codex Limits Windows",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);

        _uiState = _uiState with { BackgroundNoticeShown = true };
        _uiStateStore.TrySave(_uiState);
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
