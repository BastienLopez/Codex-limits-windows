namespace CodexLimits.Core;

public enum PaceStatus
{
    SlowDown,
    OnTrack,
    RoomToUseMore
}

public sealed record UsageWindow(
    double RemainingPercent,
    DateTimeOffset ResetsAt,
    int DurationMinutes)
{
    public DateTimeOffset StartsAt => ResetsAt.AddMinutes(-DurationMinutes);
}

public sealed record LimitReading(
    string LimitId,
    string Name,
    UsageWindow Window);

public sealed record TokenDay(
    DateTimeOffset Date,
    long Tokens);

public sealed record UsageSnapshot(
    LimitReading MainLimit,
    IReadOnlyList<LimitReading> OtherLimits,
    IReadOnlyList<TokenDay> TokenHistory,
    DateTimeOffset FetchedAt);

public sealed record UsageSample(
    DateTimeOffset ObservedAt,
    double RemainingPercent,
    DateTimeOffset ResetsAt);

public sealed record Forecast(
    PaceStatus Status,
    double ExpectedRemainingAtReset,
    double SafetyRemainingAtReset,
    double HistoricalRemainingAtReset,
    double RecommendedPercentPerDay,
    double CurrentPercentPerDay,
    double HistoricalPercentPerDay,
    double SafetyPercentPerDay);

public sealed record ChartPoint(
    DateTimeOffset Time,
    double RemainingPercent);

public sealed record ChartState(
    UsageWindow Window,
    DateTimeOffset Now,
    double SafetyBuffer,
    IReadOnlyList<ChartPoint> Target,
    IReadOnlyList<ChartPoint> Actual,
    IReadOnlyList<ChartPoint> CurrentProjection,
    IReadOnlyList<ChartPoint> HistoricalProjection);
