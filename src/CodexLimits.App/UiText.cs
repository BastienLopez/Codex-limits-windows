using System.Globalization;

namespace CodexLimits.App;

internal static class UiText
{
    public static bool IsEnglish(string? language) =>
        string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);

    public static string Get(string? language, string french, string english) =>
        IsEnglish(language) ? english : french;

    public static CultureInfo Culture(string? language) =>
        CultureInfo.GetCultureInfo(IsEnglish(language) ? "en-US" : "fr-FR");

    public static string FullDay(string? language, DayOfWeek day) =>
        (IsEnglish(language), day) switch
        {
            (true, DayOfWeek.Monday) => "Monday",
            (true, DayOfWeek.Tuesday) => "Tuesday",
            (true, DayOfWeek.Wednesday) => "Wednesday",
            (true, DayOfWeek.Thursday) => "Thursday",
            (true, DayOfWeek.Friday) => "Friday",
            (true, DayOfWeek.Saturday) => "Saturday",
            (true, DayOfWeek.Sunday) => "Sunday",
            (false, DayOfWeek.Monday) => "Lundi",
            (false, DayOfWeek.Tuesday) => "Mardi",
            (false, DayOfWeek.Wednesday) => "Mercredi",
            (false, DayOfWeek.Thursday) => "Jeudi",
            (false, DayOfWeek.Friday) => "Vendredi",
            (false, DayOfWeek.Saturday) => "Samedi",
            (false, DayOfWeek.Sunday) => "Dimanche",
            _ => day.ToString()
        };

    public static string ShortDay(string? language, DayOfWeek day) =>
        (IsEnglish(language), day) switch
        {
            (true, DayOfWeek.Monday) => "Mon",
            (true, DayOfWeek.Tuesday) => "Tue",
            (true, DayOfWeek.Wednesday) => "Wed",
            (true, DayOfWeek.Thursday) => "Thu",
            (true, DayOfWeek.Friday) => "Fri",
            (true, DayOfWeek.Saturday) => "Sat",
            (true, DayOfWeek.Sunday) => "Sun",
            (false, DayOfWeek.Monday) => "Lun",
            (false, DayOfWeek.Tuesday) => "Mar",
            (false, DayOfWeek.Wednesday) => "Mer",
            (false, DayOfWeek.Thursday) => "Jeu",
            (false, DayOfWeek.Friday) => "Ven",
            (false, DayOfWeek.Saturday) => "Sam",
            (false, DayOfWeek.Sunday) => "Dim",
            _ => day.ToString()
        };
}
