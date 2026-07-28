using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CodexLimits.Core;

namespace CodexLimits.App;

public partial class SettingsWindow : Window
{
    private string _language;
    private bool _isInitializing;

    public SettingsWindow(AppSettings settings)
    {
        _isInitializing = true;
        _language = settings.Normalize().Language;
        InitializeComponent();

        LanguageCombo.ItemsSource = new[]
        {
            new LanguageOption("fr", "Français"),
            new LanguageOption("en", "English")
        };
        LanguageCombo.SelectedValue = _language;

        PopulateDayOptions(settings.StartDay, settings.EndDay);
        StartTimeBox.Text = settings.StartTime.ToString("hh\\:mm", CultureInfo.InvariantCulture);
        EndTimeBox.Text = settings.EndTime.ToString("hh\\:mm", CultureInfo.InvariantCulture);
        RefreshIntervalCombo.ItemsSource = new[] { 15, 30, 60, 120 };
        RefreshIntervalCombo.Text = settings.RefreshIntervalMinutes.ToString(CultureInfo.InvariantCulture);
        SafetyBufferBox.Text = settings.SafetyBuffer.ToString("0.#", UiText.Culture(_language));
        PauseOutsideCheckBox.IsChecked = settings.PauseRefreshOutsideSchedule;

        ApplyLanguage();
        _isInitializing = false;
    }

    public AppSettings? ResultSettings { get; private set; }

    private string L(string french, string english) => UiText.Get(_language, french, english);

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageCombo.SelectedValue is not string language) return;

        var startDay = StartDayCombo.SelectedValue is DayOfWeek selectedStart ? selectedStart : DayOfWeek.Monday;
        var endDay = EndDayCombo.SelectedValue is DayOfWeek selectedEnd ? selectedEnd : DayOfWeek.Friday;
        _language = language;

        if (!_isInitializing)
        {
            PopulateDayOptions(startDay, endDay);
            ApplyLanguage();
        }
    }

    private void PopulateDayOptions(DayOfWeek startDay, DayOfWeek endDay)
    {
        var days = Enum.GetValues<DayOfWeek>()
            .OrderBy(day => ((int)day + 6) % 7)
            .Select(day => new DayOption(day, UiText.FullDay(_language, day)))
            .ToArray();

        StartDayCombo.ItemsSource = days;
        EndDayCombo.ItemsSource = days;
        StartDayCombo.SelectedValue = startDay;
        EndDayCombo.SelectedValue = endDay;
    }

    private void ApplyLanguage()
    {
        Title = L("Paramètres", "Settings");
        TitleText.Text = L("Paramètres", "Settings");
        SubtitleText.Text = L(
            "Planning, réserve et fréquence d’actualisation.",
            "Schedule, reserve and refresh frequency.");
        LanguageLabel.Text = L("Langue", "Language");
        StartLabel.Text = L("Début", "Start");
        EndLabel.Text = L("Fin", "End");
        StartTimeLabel.Text = L("Heure", "Time");
        EndTimeLabel.Text = L("Heure", "Time");
        RefreshLabel.Text = L("Actualisation automatique", "Automatic refresh");
        RefreshHintText.Text = L("Intervalle en minutes", "Interval in minutes");
        ReserveLabel.Text = L("Réserve de sécurité", "Safety reserve");
        ReserveHintText.Text = L("Pourcentage à conserver", "Percentage to keep");
        PauseOutsideCheckBox.Content = L(
            "Suspendre l’actualisation automatique hors créneau",
            "Pause automatic refresh outside the schedule");
        CancelButton.Content = L("Annuler", "Cancel");
        SaveButton.Content = L("Enregistrer", "Save");
        ValidationText.Text = string.Empty;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = string.Empty;

        if (StartDayCombo.SelectedValue is not DayOfWeek startDay ||
            EndDayCombo.SelectedValue is not DayOfWeek endDay)
        {
            ValidationText.Text = L(
                "Sélectionne un jour de début et un jour de fin.",
                "Select a start day and an end day.");
            return;
        }

        if (!TimeSpan.TryParseExact(StartTimeBox.Text.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var startTime) ||
            !TimeSpan.TryParseExact(EndTimeBox.Text.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var endTime))
        {
            ValidationText.Text = L(
                "Les heures doivent utiliser le format HH:mm, par exemple 09:00.",
                "Times must use the HH:mm format, for example 09:00.");
            return;
        }

        if (startDay == endDay && startTime == endTime)
        {
            ValidationText.Text = L(
                "Le début et la fin du créneau ne peuvent pas être identiques.",
                "The schedule start and end cannot be identical.");
            return;
        }

        if (!int.TryParse(RefreshIntervalCombo.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval) ||
            interval is < 5 or > 240)
        {
            ValidationText.Text = L(
                "L’intervalle doit être compris entre 5 et 240 minutes.",
                "The interval must be between 5 and 240 minutes.");
            return;
        }

        var culture = UiText.Culture(_language);
        if (!double.TryParse(SafetyBufferBox.Text.Trim(), NumberStyles.Number, culture, out var safetyBuffer) &&
            !double.TryParse(SafetyBufferBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out safetyBuffer))
        {
            ValidationText.Text = L(
                "La réserve doit être un nombre compris entre 0 et 30.",
                "The reserve must be a number between 0 and 30.");
            return;
        }

        if (safetyBuffer is < 0 or > 30)
        {
            ValidationText.Text = L(
                "La réserve doit être comprise entre 0 et 30 %.",
                "The reserve must be between 0 and 30%.");
            return;
        }

        ResultSettings = new AppSettings
        {
            StartDay = startDay,
            StartTime = startTime,
            EndDay = endDay,
            EndTime = endTime,
            RefreshIntervalMinutes = interval,
            PauseRefreshOutsideSchedule = PauseOutsideCheckBox.IsChecked == true,
            SafetyBuffer = safetyBuffer,
            Language = _language
        }.Normalize();

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private sealed record DayOption(DayOfWeek Value, string Label);
    private sealed record LanguageOption(string Value, string Label);
}
