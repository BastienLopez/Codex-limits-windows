using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using CodexLimits.Core;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace CodexLimits.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IUsageProvider _provider;
    private readonly UsageHistoryStore _history = new();
    private readonly bool _demo;
    private readonly List<UsageSample> _samples = new();
    private AppSettings _settings;
    private PaceStatus? _previousStatus;
    private UsageSnapshot? _snapshot;
    private Forecast? _forecast;
    private bool _isRefreshing;
    private string? _errorMessage;
    private ChartState? _chart;

    public MainViewModel(bool demo, AppSettings settings)
    {
        _demo = demo;
        _settings = settings.Normalize();
        _provider = demo ? new DemoUsageProvider() : new CodexAppServerClient();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<OtherLimitViewModel> OtherLimits { get; } = new();

    public AppSettings Settings => _settings;

    private string L(string french, string english) => UiText.Get(_settings.Language, french, english);
    private CultureInfo Culture => UiText.Culture(_settings.Language);

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
    public double RemainingValue => _snapshot?.MainLimit.Window.RemainingPercent ?? 0;
    public double RemainingProgress => RemainingValue;
    public string RemainingText => _snapshot is null ? "—" : $"{RemainingValue:0}";

    public double AvailableTodayPercent
    {
        get
        {
            if (_snapshot is null)
            {
                return 0;
            }

            var cycle = ScheduleMath.GetPlanningCycle(_snapshot.FetchedAt, _settings);
            return CalculateAvailableToday(
                _snapshot.MainLimit.Window.RemainingPercent,
                _snapshot.FetchedAt,
                cycle);
        }
    }
    public string UsedValueText => _snapshot is null ? "—" : $"{100 - RemainingValue:0}";
    public string UsedText => _snapshot is null
        ? L("En attente des données", "Waiting for data")
        : $"{100 - RemainingValue:0} {UsedLabel}";

    public string RemainingLabel => L("% restant", "% remaining");
    public string UsedLabel => L("% utilisés", "% used");
    public string SettingsTooltip => L("Paramètres", "Settings");
    public string RefreshTooltip => L("Actualiser maintenant", "Refresh now");
    public string ConsumptionTitle => L("Consommation", "Usage");
    public string LegendTarget => L("Cible", "Target");
    public string LegendActual => L("Réel", "Actual");
    public string LegendProjection => L("Projection", "Projection");
    public string LegendHistorical => L("Historique", "Historical");
    public string ModifyLabel => L("Modifier", "Edit");
    public string ResetLabel => L("Reset", "Reset");
    public string ExhaustionLabel => L("Épuisement estimé", "Estimated exhaustion");
    public string SuggestedPaceLabel => L("Rythme conseillé", "Recommended pace");
    public string ObservedPaceLabel => L("Rythme observé", "Observed pace");
    public string HideLabel => L("Masquer", "Hide");

    public string PlanText => _demo
        ? L("Mode démo", "Demo mode")
        : L("Codex CLI • historique local uniquement", "Codex CLI • local history only");

    public string ErrorText => _errorMessage ?? string.Empty;
    public bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);

    public string UpdatedText => _snapshot is null
        ? L("Pas encore actualisé", "Not updated yet")
        : L($"Actualisé {RelativeTime(_snapshot.FetchedAt)}", $"Updated {RelativeTime(_snapshot.FetchedAt)}");

    public string ResetText => _snapshot is null
        ? "—"
        : FormatDate(_snapshot.MainLimit.Window.ResetsAt.ToLocalTime());

    public bool IsWithinSchedule => ScheduleMath.IsActive(DateTimeOffset.Now, _settings);
    public bool CanAutoRefreshNow => !_settings.PauseRefreshOutsideSchedule || IsWithinSchedule;
    public int RefreshIntervalMinutes => _settings.RefreshIntervalMinutes;
    public string ScheduleStateText => IsWithinSchedule
        ? L("Créneau actif", "Active schedule")
        : L("En pause", "Paused");
    public Brush ScheduleStateBrush => IsWithinSchedule ? Brushes.MediumSeaGreen : Brushes.Goldenrod;
    public string ScheduleSummaryText =>
        $"{UiText.ShortDay(_settings.Language, _settings.StartDay)} {_settings.StartTime:hh\\:mm} → {UiText.ShortDay(_settings.Language, _settings.EndDay)} {_settings.EndTime:hh\\:mm}";

    public string AutoRefreshText
    {
        get
        {
            if (_settings.PauseRefreshOutsideSchedule && !IsWithinSchedule)
            {
                var next = ScheduleMath.GetNextStart(DateTimeOffset.Now, _settings).ToLocalTime();
                return L($"Auto-pause • reprise {FormatShortDate(next)}", $"Auto-pause • resumes {FormatShortDate(next)}");
            }

            return L(
                $"Actualisation automatique toutes les {_settings.RefreshIntervalMinutes} min",
                $"Automatic refresh every {_settings.RefreshIntervalMinutes} min");
        }
    }

    public string SchedulePauseTitle
    {
        get
        {
            if (IsWithinSchedule)
            {
                return L("Suivi actif • créneau en cours", "Active tracking • schedule in progress");
            }

            var next = ScheduleMath.GetNextStart(DateTimeOffset.Now, _settings).ToLocalTime();
            return L($"En pause • Reprise {FormatDate(next)}", $"Paused • Resumes {FormatDate(next)}");
        }
    }

    public string ScheduleDetailText =>
        $"{CompactDayRange(_settings.StartDay, _settings.EndDay)} · {_settings.StartTime:hh\\:mm}–{_settings.EndTime:hh\\:mm} · " +
        L($"actualisation {_settings.RefreshIntervalMinutes} min", $"refresh {_settings.RefreshIntervalMinutes} min");

    public string SuggestedPaceText
    {
        get
        {
            if (_snapshot is null || _forecast is null) return "—";
            if (_forecast.ActiveHoursLeft <= 0)
            {
                return L("Créneau terminé • aucune consommation planifiée", "Schedule finished • no planned usage");
            }

            var dailyHours = ScheduleMath.GetNominalDailyHours(_settings);
            return _forecast.ActiveHoursLeft <= dailyHours
                ? L(
                    $"Jusqu’à {_forecast.RecommendedPercentPerDay / dailyHours:0.0} % par heure active",
                    $"Up to {_forecast.RecommendedPercentPerDay / dailyHours:0.0}% per active hour")
                : L(
                    $"Jusqu’à {_forecast.RecommendedPercentPerDay:0.0} % par jour travaillé",
                    $"Up to {_forecast.RecommendedPercentPerDay:0.0}% per workday");
        }
    }

    public string EstimatedExhaustionText
    {
        get
        {
            if (_snapshot is null || _forecast is null) return "—";
            if (_forecast.EstimatedExhaustionAt is { } exhaustion)
            {
                return FormatDate(exhaustion.ToLocalTime());
            }

            var cycle = ScheduleMath.GetPlanningCycle(_snapshot.FetchedAt, _settings);
            var planningEnd = cycle.LastOrDefault()?.End;
            return planningEnd is { } end
                ? L(
                    $"Pas d’épuisement prévu avant {FormatDate(end.ToLocalTime())}",
                    $"No exhaustion expected before {FormatDate(end.ToLocalTime())}")
                : L("Pas d’épuisement prévu", "No exhaustion expected");
        }
    }

    public string CurrentPaceText
    {
        get
        {
            if (_snapshot is null || _forecast is null || _forecast.CurrentPercentPerDay <= 0) return "—";
            return L(
                $"{_forecast.CurrentPercentPerDay:0.0} % / jour travaillé",
                $"{_forecast.CurrentPercentPerDay:0.0}% / workday");
        }
    }

    public string StatusTitle => _forecast?.Status switch
    {
        PaceStatus.SlowDown => L("Risque", "Risk"),
        PaceStatus.OnTrack => L("Dans le rythme", "On track"),
        PaceStatus.RoomToUseMore => L("Marge disponible", "Room available"),
        _ => L("Apprentissage", "Learning")
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
            if (_snapshot is null || _forecast is null)
            {
                return L("Collecte des premières données…", "Collecting initial data…");
            }

            return BuildQuotaSummaryMessage(
                _snapshot.MainLimit.Window,
                _forecast,
                _snapshot.FetchedAt);
        }
    }

    private string BuildQuotaSummaryMessage(
        UsageWindow window,
        Forecast forecast,
        DateTimeOffset reference)
    {
        var cycle = ScheduleMath.GetPlanningCycle(reference, _settings);
        var planningEnd = cycle.LastOrDefault()?.End;

        var endLabel = planningEnd is { } end
            ? FormatScheduleEnd(end.ToLocalTime())
            : L("fin du planning", "end of the schedule");

        var availableToday = CalculateAvailableToday(
            window.RemainingPercent,
            reference,
            cycle);

        return L(
            $"Aujourd'hui : encore {availableToday:0} % utilisables pour rester sur la cible.\n{UppercaseFirst(endLabel)} : environ {forecast.ExpectedRemainingAtReset:0} % restants au rythme actuel.",
            $"Today: {availableToday:0}% still available while staying on target.\n{UppercaseFirst(endLabel)}: about {forecast.ExpectedRemainingAtReset:0}% remaining at the current pace.");
    }

    private double CalculateAvailableToday(
        double remainingPercent,
        DateTimeOffset reference,
        IReadOnlyList<TimeRange> cycle)
    {
        if (cycle.Count == 0)
        {
            return 0;
        }

        var today = reference.ToLocalTime().Date;
        var todayIndex = -1;

        for (var index = 0; index < cycle.Count; index++)
        {
            if (cycle[index].Start.ToLocalTime().Date == today)
            {
                todayIndex = index;
                break;
            }
        }

        if (todayIndex < 0 || reference >= cycle[todayIndex].End)
        {
            return 0;
        }

        var dailyTarget = (100d - _settings.SafetyBuffer) / cycle.Count;
        var targetRemainingAtEndOfToday = Math.Max(
            100d - dailyTarget * (todayIndex + 1),
            _settings.SafetyBuffer);

        return Math.Max(
            remainingPercent - targetRemainingAtEndOfToday,
            0);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_demo)
        {
            _samples.AddRange(await _history.LoadAsync(cancellationToken: cancellationToken));
        }
        await RefreshAsync(cancellationToken);
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

    public void ApplySettings(AppSettings settings)
    {
        var previousLanguage = _settings.Language;
        _settings = settings.Normalize();
        _previousStatus = null;
        Recalculate();
        UpdateOtherLimits();
        NotifyScheduleProperties();
        NotifySnapshotProperties();
        NotifyForecastProperties();

        if (!string.Equals(previousLanguage, _settings.Language, StringComparison.OrdinalIgnoreCase))
        {
            NotifyLocalizedProperties();
        }
    }

    public void UpdateClock()
    {
        OnPropertyChanged(nameof(UpdatedText));
        OnPropertyChanged(nameof(AvailableTodayPercent));
        NotifyScheduleProperties();
        NotifyForecastProperties();
    }

    private void Recalculate()
    {
        if (_snapshot is null) return;
        _forecast = ForecastEngine.Evaluate(
            _snapshot.MainLimit.Window,
            _samples,
            _snapshot.TokenHistory,
            _settings.SafetyBuffer,
            _snapshot.FetchedAt,
            _previousStatus,
            _settings);
        _previousStatus = _forecast.Status;
        Chart = ChartSeriesBuilder.Build(
            _snapshot,
            _samples,
            _forecast,
            _settings.SafetyBuffer,
            _settings);
        NotifyForecastProperties();
    }

    private void UpdateOtherLimits()
    {
        OtherLimits.Clear();
        if (_snapshot is null) return;

        foreach (var limit in _snapshot.OtherLimits)
        {
            OtherLimits.Add(new OtherLimitViewModel(
                TranslateLimitName(limit.Name),
                $"{limit.Window.RemainingPercent:0} %",
                FormatCompactDate(limit.Window.ResetsAt.ToLocalTime())));
        }
    }

    private string TranslateLimitName(string name)
    {
        if (!UiText.IsEnglish(_settings.Language)) return name;
        if (name.Equals("Fenêtre hebdomadaire", StringComparison.OrdinalIgnoreCase)) return "Weekly window";
        if (name.StartsWith("Fenêtre de ", StringComparison.OrdinalIgnoreCase)) return name.Replace("Fenêtre de ", "Window of ", StringComparison.OrdinalIgnoreCase);
        if (name.Equals("Fenêtre supplémentaire", StringComparison.OrdinalIgnoreCase)) return "Additional window";
        return name;
    }

    private string BuildSlowDownMessage(UsageWindow window, Forecast forecast)
    {
        if (forecast.EstimatedExhaustionAt is not { } exhaustion)
        {
            return L("Ton rythme actuel est trop proche de la limite.", "Your current pace is too close to the limit.");
        }

        var cycle = ScheduleMath.GetPlanningCycle(DateTimeOffset.Now, _settings);
        var planningEnd = cycle.LastOrDefault()?.End;
        if (planningEnd is null || exhaustion >= planningEnd)
        {
            return L("Ton rythme actuel est trop proche de la limite.", "Your current pace is too close to the limit.");
        }

        var activeHoursEarly = ScheduleMath.GetActiveHours(cycle, exhaustion, planningEnd.Value);
        var dailyHours = ScheduleMath.GetNominalDailyHours(_settings);
        return activeHoursEarly >= dailyHours
            ? L(
                $"À ce rythme, le quota pourrait être épuisé {Math.Max((int)Math.Round(activeHoursEarly / dailyHours), 1)} jour(s) travaillé(s) trop tôt.",
                $"At this pace, the quota could run out {Math.Max((int)Math.Round(activeHoursEarly / dailyHours), 1)} workday(s) too early.")
            : L(
                $"À ce rythme, le quota pourrait être épuisé {Math.Max((int)Math.Round(activeHoursEarly), 1)} heure(s) active(s) trop tôt.",
                $"At this pace, the quota could run out {Math.Max((int)Math.Round(activeHoursEarly), 1)} active hour(s) too early.");
    }

    private string FormatScheduleEnd(DateTimeOffset value) => UiText.IsEnglish(_settings.Language)
        ? value.ToString("dddd 'at' HH:mm", Culture)
        : value.ToString("dddd 'à' HH:mm", Culture);

    private string UppercaseFirst(string value) =>
        string.IsNullOrEmpty(value)
            ? value
            : char.ToUpper(value[0], Culture) + value[1..];

    private string RelativeTime(DateTimeOffset timestamp)
    {
        var elapsed = DateTimeOffset.Now - timestamp.ToLocalTime();
        if (elapsed.TotalMinutes < 1) return L("à l’instant", "just now");
        if (elapsed.TotalHours < 1) return L($"il y a {(int)elapsed.TotalMinutes} min", $"{(int)elapsed.TotalMinutes} min ago");
        if (elapsed.TotalDays < 1) return L($"il y a {(int)elapsed.TotalHours} h", $"{(int)elapsed.TotalHours} h ago");
        return L($"il y a {(int)elapsed.TotalDays} j", $"{(int)elapsed.TotalDays} d ago");
    }

    private string CompactDayRange(DayOfWeek start, DayOfWeek end)
    {
        var startLabel = UiText.ShortDay(_settings.Language, start);
        var endLabel = UiText.ShortDay(_settings.Language, end);
        return start == end ? startLabel : $"{startLabel}–{endLabel.ToLowerInvariant()}";
    }

    private string FormatDate(DateTimeOffset value) => UiText.IsEnglish(_settings.Language)
        ? value.ToString("ddd, MMM dd 'at' HH:mm", Culture)
        : value.ToString("ddd dd MMM 'à' HH:mm", Culture);

    private string FormatShortDate(DateTimeOffset value) => UiText.IsEnglish(_settings.Language)
        ? value.ToString("ddd HH:mm", Culture)
        : value.ToString("ddd HH:mm", Culture);

    private string FormatCompactDate(DateTimeOffset value) => UiText.IsEnglish(_settings.Language)
        ? value.ToString("MMM dd 'at' HH:mm", Culture)
        : value.ToString("dd MMM 'à' HH:mm", Culture);

    private void NotifySnapshotProperties()
    {
        OnPropertyChanged(nameof(RemainingValue));
        OnPropertyChanged(nameof(RemainingProgress));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(AvailableTodayPercent));
        OnPropertyChanged(nameof(UsedValueText));
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
        OnPropertyChanged(nameof(EstimatedExhaustionText));
        OnPropertyChanged(nameof(CurrentPaceText));
    }

    private void NotifyScheduleProperties()
    {
        OnPropertyChanged(nameof(IsWithinSchedule));
        OnPropertyChanged(nameof(CanAutoRefreshNow));
        OnPropertyChanged(nameof(RefreshIntervalMinutes));
        OnPropertyChanged(nameof(ScheduleStateText));
        OnPropertyChanged(nameof(ScheduleStateBrush));
        OnPropertyChanged(nameof(ScheduleSummaryText));
        OnPropertyChanged(nameof(AutoRefreshText));
        OnPropertyChanged(nameof(SchedulePauseTitle));
        OnPropertyChanged(nameof(ScheduleDetailText));
    }

    private void NotifyLocalizedProperties()
    {
        OnPropertyChanged(nameof(RemainingLabel));
        OnPropertyChanged(nameof(UsedLabel));
        OnPropertyChanged(nameof(SettingsTooltip));
        OnPropertyChanged(nameof(RefreshTooltip));
        OnPropertyChanged(nameof(ConsumptionTitle));
        OnPropertyChanged(nameof(LegendTarget));
        OnPropertyChanged(nameof(LegendActual));
        OnPropertyChanged(nameof(LegendProjection));
        OnPropertyChanged(nameof(LegendHistorical));
        OnPropertyChanged(nameof(ModifyLabel));
        OnPropertyChanged(nameof(ResetLabel));
        OnPropertyChanged(nameof(ExhaustionLabel));
        OnPropertyChanged(nameof(SuggestedPaceLabel));
        OnPropertyChanged(nameof(ObservedPaceLabel));
        OnPropertyChanged(nameof(HideLabel));
        OnPropertyChanged(nameof(UsedText));
        OnPropertyChanged(nameof(PlanText));
        OnPropertyChanged(nameof(UpdatedText));
        OnPropertyChanged(nameof(ResetText));
        NotifyScheduleProperties();
        NotifyForecastProperties();
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

