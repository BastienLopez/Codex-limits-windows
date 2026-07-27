using System.Globalization;
using System.Windows;
using CodexLimits.Core;

namespace CodexLimits.App;

public partial class SettingsWindow : Window
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        var days = new[]
        {
            new DayOption(DayOfWeek.Monday, "Lundi"),
            new DayOption(DayOfWeek.Tuesday, "Mardi"),
            new DayOption(DayOfWeek.Wednesday, "Mercredi"),
            new DayOption(DayOfWeek.Thursday, "Jeudi"),
            new DayOption(DayOfWeek.Friday, "Vendredi"),
            new DayOption(DayOfWeek.Saturday, "Samedi"),
            new DayOption(DayOfWeek.Sunday, "Dimanche")
        };

        StartDayCombo.ItemsSource = days;
        EndDayCombo.ItemsSource = days;
        StartDayCombo.SelectedValue = settings.StartDay;
        EndDayCombo.SelectedValue = settings.EndDay;
        StartTimeBox.Text = settings.StartTime.ToString("hh\\:mm", CultureInfo.InvariantCulture);
        EndTimeBox.Text = settings.EndTime.ToString("hh\\:mm", CultureInfo.InvariantCulture);
        RefreshIntervalCombo.ItemsSource = new[] { 15, 30, 60, 120 };
        RefreshIntervalCombo.Text = settings.RefreshIntervalMinutes.ToString(CultureInfo.InvariantCulture);
        SafetyBufferBox.Text = settings.SafetyBuffer.ToString("0.#", FrenchCulture);
        PauseOutsideCheckBox.IsChecked = settings.PauseRefreshOutsideSchedule;
    }

    public AppSettings? ResultSettings { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = string.Empty;

        if (StartDayCombo.SelectedValue is not DayOfWeek startDay ||
            EndDayCombo.SelectedValue is not DayOfWeek endDay)
        {
            ValidationText.Text = "Sélectionne un jour de début et un jour de fin.";
            return;
        }

        if (!TimeSpan.TryParseExact(StartTimeBox.Text.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var startTime) ||
            !TimeSpan.TryParseExact(EndTimeBox.Text.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var endTime))
        {
            ValidationText.Text = "Les heures doivent utiliser le format HH:mm, par exemple 09:00.";
            return;
        }

        if (startDay == endDay && startTime == endTime)
        {
            ValidationText.Text = "Le début et la fin du créneau ne peuvent pas être identiques.";
            return;
        }

        if (!int.TryParse(RefreshIntervalCombo.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval) ||
            interval is < 5 or > 240)
        {
            ValidationText.Text = "L’intervalle doit être compris entre 5 et 240 minutes.";
            return;
        }

        if (!double.TryParse(SafetyBufferBox.Text.Trim(), NumberStyles.Number, FrenchCulture, out var safetyBuffer) &&
            !double.TryParse(SafetyBufferBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out safetyBuffer))
        {
            ValidationText.Text = "La réserve doit être un nombre compris entre 0 et 30.";
            return;
        }

        if (safetyBuffer is < 0 or > 30)
        {
            ValidationText.Text = "La réserve doit être comprise entre 0 et 30 %.";
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
            SafetyBuffer = safetyBuffer
        }.Normalize();

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private sealed record DayOption(DayOfWeek Value, string Label);
}
