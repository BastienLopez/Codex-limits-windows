namespace CodexLimits.Core;

public sealed record AppSettings
{
    public DayOfWeek StartDay { get; init; } = DayOfWeek.Monday;
    public TimeSpan StartTime { get; init; } = TimeSpan.FromHours(9);
    public DayOfWeek EndDay { get; init; } = DayOfWeek.Friday;
    public TimeSpan EndTime { get; init; } = TimeSpan.FromHours(18);
    public int RefreshIntervalMinutes { get; init; } = 30;
    public bool PauseRefreshOutsideSchedule { get; init; } = true;
    public double SafetyBuffer { get; init; } = 3;
    public string Language { get; init; } = "fr";

    public AppSettings Normalize() => this with
    {
        StartTime = ClampTime(StartTime),
        EndTime = ClampTime(EndTime),
        RefreshIntervalMinutes = Math.Clamp(RefreshIntervalMinutes, 5, 240),
        SafetyBuffer = Math.Clamp(SafetyBuffer, 0, 30),
        Language = string.Equals(Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "fr"
    };

    private static TimeSpan ClampTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero) return TimeSpan.Zero;
        var lastMinute = TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59);
        return value > lastMinute ? lastMinute : value;
    }
}
