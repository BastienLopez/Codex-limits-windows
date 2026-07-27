using System.Globalization;
using CodexLimits.Core;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using Window = System.Windows.Window;

namespace CodexLimits.App;

public partial class ScheduleSettingsWindow : Window
{
    private static readonly string[] TimeOptions = Enumerable
        .Range(0, 48)
        .Select(index => TimeSpan.FromMinutes(index * 30))
        .Select(FormatTime)
        .ToArray();

    private static readonly int[] RefreshOptions = { 15, 30, 60, 120 };

    public ScheduleSettingsWindow(WorkScheduleSettings current)
    {
        InitializeComponent();

        StartTimeBox.ItemsSource = TimeOptions;
        EndTimeBox.ItemsSource = TimeOptions;
        RefreshMinutesBox.ItemsSource = RefreshOptions.Select(minutes => $"Toutes les {minutes} min").ToArray();

        MondayBox.IsChecked = current.Monday;
        TuesdayBox.IsChecked = current.Tuesday;
        WednesdayBox.IsChecked = current.Wednesday;
        ThursdayBox.IsChecked = current.Thursday;
        FridayBox.IsChecked = current.Friday;
        SaturdayBox.IsChecked = current.Saturday;
        SundayBox.IsChecked = current.Sunday;
        StartTimeBox.SelectedItem = FormatTime(current.StartTime);
        EndTimeBox.SelectedItem = FormatTime(current.EndTime);

        var refreshIndex = Array.IndexOf(RefreshOptions, current.RefreshMinutes);
        RefreshMinutesBox.SelectedIndex = refreshIndex >= 0 ? refreshIndex : 1;
    }

    public WorkScheduleSettings? Result { get; private set; }

    private void SaveButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (StartTimeBox.SelectedItem is not string startText ||
            EndTimeBox.SelectedItem is not string endText ||
            !TimeSpan.TryParseExact(startText, "hh\\:mm", CultureInfo.InvariantCulture, out var startTime) ||
            !TimeSpan.TryParseExact(endText, "hh\\:mm", CultureInfo.InvariantCulture, out var endTime))
        {
            ShowValidationError("Sélectionne une heure de début et une heure de fin.");
            return;
        }

        if (endTime <= startTime)
        {
            ShowValidationError("L’heure de fin doit être postérieure à l’heure de début.");
            return;
        }

        var refreshIndex = RefreshMinutesBox.SelectedIndex;
        var refreshMinutes = refreshIndex >= 0 && refreshIndex < RefreshOptions.Length
            ? RefreshOptions[refreshIndex]
            : 30;

        var settings = new WorkScheduleSettings(
            MondayBox.IsChecked == true,
            TuesdayBox.IsChecked == true,
            WednesdayBox.IsChecked == true,
            ThursdayBox.IsChecked == true,
            FridayBox.IsChecked == true,
            SaturdayBox.IsChecked == true,
            SundayBox.IsChecked == true,
            startTime,
            endTime,
            refreshMinutes);

        if (!settings.IsValid)
        {
            ShowValidationError("Sélectionne au moins un jour actif.");
            return;
        }

        Result = settings;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, System.Windows.RoutedEventArgs e) => DialogResult = false;

    private static void ShowValidationError(string message) =>
        MessageBox.Show(message, "Planning invalide", MessageBoxButton.OK, MessageBoxImage.Warning);

    private static string FormatTime(TimeSpan time) =>
        DateTime.Today.Add(time).ToString("HH:mm", CultureInfo.InvariantCulture);
}
