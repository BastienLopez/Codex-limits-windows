namespace CodexLimits.Core;

public readonly record struct DailyQuotaBalance(
    bool HasScheduledDay,
    bool ScheduledDayHasEnded,
    double AvailablePercent,
    double ExceededPercent,
    double TargetRemainingAtEndOfDay);

public static class DailyQuotaMath
{
    public static DailyQuotaBalance Evaluate(
        double remainingPercent,
        DateTimeOffset reference,
        IReadOnlyList<TimeRange> cycle,
        double safetyBuffer)
    {
        if (cycle.Count == 0)
        {
            return default;
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

        if (todayIndex < 0)
        {
            return default;
        }

        var normalizedBuffer = Math.Clamp(safetyBuffer, 0d, 99d);
        var dailyTarget = (100d - normalizedBuffer) / cycle.Count;
        var targetRemainingAtEndOfDay = Math.Max(
            100d - dailyTarget * (todayIndex + 1),
            normalizedBuffer);

        var balance = remainingPercent - targetRemainingAtEndOfDay;
        var dayHasEnded = reference >= cycle[todayIndex].End;

        return new DailyQuotaBalance(
            HasScheduledDay: true,
            ScheduledDayHasEnded: dayHasEnded,
            AvailablePercent: dayHasEnded ? 0d : Math.Max(balance, 0d),
            ExceededPercent: Math.Max(-balance, 0d),
            TargetRemainingAtEndOfDay: targetRemainingAtEndOfDay);
    }
}
