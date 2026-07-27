using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CodexLimits.Core;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace CodexLimits.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IUsageProvider _provider;
    private readonly UsageHistoryStore _history = new();
    private readonly AppSettingsStore _settingsStore = new();
    private readonly bool _demo;
    private readonly List<UsageSample> _samples = new();
    private PaceStatus? _previousStatus;
    private UsageSnapshot? _snapshot;
    private Forecast? _forecast;
    private bool _isRefreshing;
    private string? _errorMessage;
    private double _safetyBuffer = 3;
    private ChartState? _chart;
    private WorkScheduleSettings _settings = WorkScheduleSettings.Default;

    public MainViewModel(bool demo)
    {
        _demo = demo;
        _provider = demo ? new DemoUsageProvider() : new CodexAppServerClient();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<OtherLimitViewModel> OtherLimits { get; } = new();

    public WorkScheduleSettings Settings => _settings;
    public int RefreshIntervalMinutes => _settings.RefreshMinutes;

    public ChartState? Chart
    {
        get => _chart;
        private set => SetField(ref _chart, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (SetField(ref _isRefreshing, value))
            {
                OnPropertyChanged(nameof(RefreshButtonText));
            }
        }
    }

    public string RefreshButtonText => IsRefreshing ? "…" : "↻";
    public string RemainingText => _snapshot is null ? "—" : $"{_snapshot.MainLimit.Window.RemainingPercent:0}";
    public double RemainingProgress => _snapshot?.MainLimit.Window.RemainingPercent ?? 0;
    public string UsedText => _snapshot is null ? "—" : $"{100 - _snapshot.MainLimit.Window.RemainingPercent:0} % utilisés";
    public string PlanText => _demo ? "Mode démo · stockage local" : "Codex CLI · historique local uniquement";
    public string ErrorText => _errorMessage ?? string.Empty;
    public bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);
    public string UpdatedText => _snapshot is null ? "Pas encore actualisé" : $"Actualisé {RelativeTime(_snapshot.FetchedAt)}";
    public string ResetText => _snapshot is null ? "—" : _snapshot.MainLimit.Window.ResetsAt.ToLocalTime().ToString("dd MMM yyyy 'à' HH:mm");
    public string ScheduleText => _settings.Summary;
    public string ActiveWindowText => $"{_settings.DaysSummary} · {_settings.StartTime:hh\\:mm}–{_settings.EndTime:hh\\:mm}";

    public bool IsScheduleActive => _settings.IsActive(DateTimeOffset.Now);

    public string ScheduleStateText
    {
        get
        {
            var now = DateTimeOffset.Now;
            if (_settings.IsActive(now))
            {
                return $"Actif maintenant · mise à jour automatique toutes les {_settings.RefreshMinutes} min";
            }

            var next = _settings.NextActiveStart(now).ToLocalTime();
            var prefix = next.Date == now.Date ? "Reprise aujourd’hui" : "Reprise";
            return $"En pause · {prefix} {next:ddd dd MMM 'à' HH:mm}";
        }
    }

    public Brush ScheduleStateBrush => IsScheduleActive ? Brushes.MediumSeaGreen : Brushes.DarkOrange;

    public string SuggestedPaceText
    {
        get
        {
            if (_snapshot is null || _forecast is null || _settings.DailyActiveHours <= 0) return "—";
            var activeHoursLeft = _settings.ActiveHoursBetween(_snapshot.FetchedAt, _snapshot.MainLimit.Window.ResetsAt);
            return activeHoursLeft <= _settings.DailyActiveHours
                ? $"Jusqu’à {_forecast.RecommendedPercentPerDay / _settings.DailyActiveHours:0.0} % par heure active"
                : $"Jusqu’à {_forecast.RecommendedPercentPerDay:0.0} % par jour travaillé";
        }
    }

    public string ExhaustionText
    {
        get
        {
            var exhaustion = EstimateExhaustionAt();
            if (exhaustion is null) return "Non estimé";
            if (_snapshot is not null && exhaustion >= _snapshot.MainLimit.Window.ResetsAt)
            {
                return "Après le prochain reset";
            }
            return exhaustion.Value.ToLocalTime().ToString("ddd dd MMM 'à' HH:mm");
        }
    }

    public string CurrentRateText => _forecast is null
        ? "—"
        : $"{_forecast.CurrentPercentPerDay:0.0} % / jour travaillé";

    public string StatusTitle => _forecast?.Status switch
    {
        PaceStatus.SlowDown => "Ralentis",
        PaceStatus.OnTrack => "Dans le rythme",
        PaceStatus.RoomToUseMore => "Marge disponible",
        _ => "Apprentissage"
    };

    public Brush StatusBrush => _forecast?.Status switch
    {
        PaceStatus.SlowDown => Brushes.IndianRed,
        PaceStatus.OnTrack => Brushes.MediumSeaGreen,
        PaceStatus.RoomToUseMore => Brushes.DodgerBlue,
        _ => Brushes.Gray
    };

    public string StatusMessage
    {
        get
        {
            if (_snapshot is null || _forecast is null) return "Collecte des premières données…";
            return _forecast.Status switch
            {
                PaceStatus.SlowDown => BuildSlowDownMessage(),
                PaceStatus.OnTrack => $"À ce rythme, il resterait environ {_forecast.ExpectedRemainingAtReset:0} % au reset, hors périodes inactives.",
                PaceStatus.RoomToUseMore => $"Tu peux utiliser environ {Math.Max(_forecast.ExpectedRemainingAtReset - SafetyBuffer, 0):0} % de plus pendant les heures actives.",
                _ => "Collecte des premières données…"
            };
        }
    }

    public double SafetyBuffer
    {
        get => _safetyBuffer;
        set
        {
            var clamped = Math.Clamp(value, 0, 30);
            if (SetField(ref _safetyBuffer, clamped))
            {
                Recalculate();
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _settingsStore.LoadAsync(cancellationToken);
        NotifyScheduleProperties();

        if (!_demo)
        {
            _samples.AddRange(await _history.LoadAsync(cancellationToken: cancellationToken));
        }

        await RefreshAsync(cancellationToken);
    }

    public async Task ApplySettingsAsync(
        WorkScheduleSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!settings.IsValid)
        {
            throw new ArgumentException("Les jours ou horaires sélectionnés sont invalides.", nameof(settings));
        }

        await _settingsStore.SaveAsync(settings, cancellationToken);
        _settings = settings;
        Recalculate();
        NotifyScheduleProperties();
    }

    public bool IsAutomaticRefreshAllowed(DateTimeOffset now) => _settings.IsActive(now);

    public void UpdateClock()
    {
        OnPropertyChanged(nameof(IsScheduleActive));
        OnPropertyChanged(nameof(ScheduleStateText));
        OnPropertyChanged(nameof(ScheduleStateBrush));
        OnPropertyChanged(nameof(UpdatedText));
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        _errorMessage = null;
        NotifyErrorProperties();

        try
        {
            var snapshot = await _provider.FetchAsync(cancellationToken);
            _snapshot = snapshot;

            if (_demo && _samples.Count == 0)
            {
                _samples.AddRange(DemoUsageProvider.CreateSeedSamples(snapshot.MainLimit.Window, snapshot.FetchedAt));
            }

            var sample = new UsageSample(
                snapshot.FetchedAt,
                snapshot.MainLimit.Window.RemainingPercent,
                snapshot.MainLimit.Window.ResetsAt);

            if (_samples.Count == 0 ||
                _samples[^1].ObservedAt != sample.ObservedAt ||
                Math.Abs(_samples[^1].RemainingPercent - sample.RemainingPercent) > 0.001)
            {
                _samples.Add(sample);
                if (!_demo)
                {
                    await _history.RecordAsync(sample, cancellationToken);
                }
            }

            Recalculate();
            UpdateOtherLimits();
            NotifySnapshotProperties();
            NotifyScheduleProperties();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _errorMessage = exception.Message;
            NotifyErrorProperties();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void Recalculate()
    {
        if (_snapshot is null) return;
        _forecast = ForecastEngine.Evaluate(
            _snapshot.MainLimit.Window,
            _samples,
            _snapshot.TokenHistory,
            SafetyBuffer,
            _settings,
            _snapshot.FetchedAt,
            _previousStatus);
        _previousStatus = _forecast.Status;
        Chart = ChartSeriesBuilder.Build(_snapshot, _samples, _forecast, SafetyBuffer, _settings);
        NotifyForecastProperties();
    }

    private void UpdateOtherLimits()
    {
        OtherLimits.Clear();
        if (_snapshot is null) return;
        foreach (var limit in _snapshot.OtherLimits)
        {
            OtherLimits.Add(new OtherLimitViewModel(
                limit.Name,
                $"{limit.Window.RemainingPercent:0} %",
                limit.Window.ResetsAt.ToLocalTime().ToString("dd MMM à HH:mm")));
        }
    }

    private DateTimeOffset? EstimateExhaustionAt()
    {
        if (_snapshot is null || _forecast is null ||
            _forecast.CurrentPercentPerDay <= 0 || _settings.DailyActiveHours <= 0)
        {
            return null;
        }

        var percentPerActiveHour = _forecast.CurrentPercentPerDay / _settings.DailyActiveHours;
        if (percentPerActiveHour <= 0)
        {
            return null;
        }

        var activeHoursToEmpty = _snapshot.MainLimit.Window.RemainingPercent / percentPerActiveHour;
        return _settings.AddActiveHours(_snapshot.FetchedAt, activeHoursToEmpty);
    }

    private string BuildSlowDownMessage()
    {
        var exhaustion = EstimateExhaustionAt();
        if (_snapshot is null || exhaustion is null)
        {
            return "Ton rythme actuel est trop proche de la limite.";
        }

        if (exhaustion >= _snapshot.MainLimit.Window.ResetsAt)
        {
            return "Ton rythme actuel est trop proche de la réserve choisie.";
        }

        var activeHoursEarly = _settings.ActiveHoursBetween(exhaustion.Value, _snapshot.MainLimit.Window.ResetsAt);
        if (activeHoursEarly >= _settings.DailyActiveHours)
        {
            var workingDays = Math.Max((int)Math.Round(activeHoursEarly / _settings.DailyActiveHours), 1);
            return $"À ce rythme, le quota pourrait être épuisé {workingDays} jour(s) travaillé(s) trop tôt.";
        }

        return $"À ce rythme, le quota pourrait être épuisé {Math.Max((int)Math.Round(activeHoursEarly), 1)} heure(s) active(s) trop tôt.";
    }

    private static string RelativeTime(DateTimeOffset timestamp)
    {
        var elapsed = DateTimeOffset.Now - timestamp.ToLocalTime();
        if (elapsed.TotalMinutes < 1) return "à l’instant";
        if (elapsed.TotalHours < 1) return $"il y a {(int)elapsed.TotalMinutes} min";
        if (elapsed.TotalDays < 1) return $"il y a {(int)elapsed.TotalHours} h";
        return $"il y a {(int)elapsed.TotalDays} j";
    }

    private void NotifySnapshotProperties()
    {
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(RemainingProgress));
        OnPropertyChanged(nameof(UsedText));
        OnPropertyChanged(nameof(UpdatedText));
        OnPropertyChanged(nameof(ResetText));
        OnPropertyChanged(nameof(PlanText));
    }

    private void NotifyForecastProperties()
    {
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(SuggestedPaceText));
        OnPropertyChanged(nameof(ExhaustionText));
        OnPropertyChanged(nameof(CurrentRateText));
    }

    private void NotifyScheduleProperties()
    {
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(RefreshIntervalMinutes));
        OnPropertyChanged(nameof(ScheduleText));
        OnPropertyChanged(nameof(ActiveWindowText));
        OnPropertyChanged(nameof(IsScheduleActive));
        OnPropertyChanged(nameof(ScheduleStateText));
        OnPropertyChanged(nameof(ScheduleStateBrush));
        OnPropertyChanged(nameof(SuggestedPaceText));
        OnPropertyChanged(nameof(ExhaustionText));
    }

    private void NotifyErrorProperties()
    {
        OnPropertyChanged(nameof(ErrorText));
        OnPropertyChanged(nameof(HasError));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record OtherLimitViewModel(string Name, string Remaining, string Reset);
