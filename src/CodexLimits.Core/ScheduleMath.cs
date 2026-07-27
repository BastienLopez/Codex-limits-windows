namespace CodexLimits.Core;

public sealed record TimeRange(DateTimeOffset Start, DateTimeOffset End);

public static class ScheduleMath
{
    public static bool IsActive(DateTimeOffset instant, AppSettings settings)
    {
        var normalized = settings.Normalize();
        return GetIntervals(instant.AddDays(-7), instant.AddDays(7), normalized)
            .Any(interval => instant >= interval.Start && instant < interval.End);
    }

    public static DateTimeOffset GetNextStart(DateTimeOffset instant, AppSettings settings)
    {
        var normalized = settings.Normalize();
        var candidate = GetIntervals(instant.AddDays(-7), instant.AddDays(21), normalized)
            .Where(interval => interval.End > instant)
            .OrderBy(interval => interval.Start)
            .FirstOrDefault();

        if (candidate is null)
        {
            throw new InvalidOperationException("Impossible de calculer le prochain créneau actif.");
        }

        return instant < candidate.Start ? candidate.Start : instant;
    }

    public static IReadOnlyList<TimeRange> GetIntervals(
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        AppSettings settings)
    {
        if (rangeEnd <= rangeStart) return Array.Empty<TimeRange>();

        var normalized = settings.Normalize();
        var localStart = rangeStart.ToLocalTime().DateTime;
        var weekStart = StartOfWeek(localStart.Date).AddDays(-7);
        var lastWeek = StartOfWeek(rangeEnd.ToLocalTime().DateTime.Date).AddDays(7);
        var intervals = new List<TimeRange>();

        for (var monday = weekStart; monday <= lastWeek; monday = monday.AddDays(7))
        {
            var startLocal = monday
                .AddDays(MondayBasedIndex(normalized.StartDay))
                .Add(normalized.StartTime);
            var endLocal = monday
                .AddDays(MondayBasedIndex(normalized.EndDay))
                .Add(normalized.EndTime);

            if (endLocal <= startLocal)
            {
                endLocal = endLocal.AddDays(7);
            }

            var start = ToLocalOffset(startLocal);
            var end = ToLocalOffset(endLocal);
            var clippedStart = start < rangeStart ? rangeStart : start;
            var clippedEnd = end > rangeEnd ? rangeEnd : end;
            if (clippedEnd > clippedStart)
            {
                intervals.Add(new TimeRange(clippedStart, clippedEnd));
            }
        }

        return intervals
            .OrderBy(interval => interval.Start)
            .ToArray();
    }

    public static IReadOnlyList<TimeRange> GetInactiveIntervals(
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        AppSettings settings)
    {
        var active = GetIntervals(rangeStart, rangeEnd, settings);
        var inactive = new List<TimeRange>();
        var cursor = rangeStart;

        foreach (var interval in active)
        {
            if (interval.Start > cursor)
            {
                inactive.Add(new TimeRange(cursor, interval.Start));
            }
            if (interval.End > cursor)
            {
                cursor = interval.End;
            }
        }

        if (cursor < rangeEnd)
        {
            inactive.Add(new TimeRange(cursor, rangeEnd));
        }

        return inactive;
    }

    public static double GetActiveHours(
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        AppSettings settings) =>
        GetIntervals(rangeStart, rangeEnd, settings)
            .Sum(interval => (interval.End - interval.Start).TotalHours);

    public static DateTimeOffset? AddActiveHours(
        DateTimeOffset start,
        double activeHours,
        DateTimeOffset limit,
        AppSettings settings)
    {
        if (activeHours <= 0) return start;
        var remaining = activeHours;

        foreach (var interval in GetIntervals(start, limit, settings))
        {
            var available = (interval.End - interval.Start).TotalHours;
            if (remaining <= available)
            {
                return interval.Start.AddHours(remaining);
            }
            remaining -= available;
        }

        return null;
    }

    public static DateTimeOffset? GetPlanningEnd(
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        AppSettings settings) =>
        GetIntervals(rangeStart, rangeEnd, settings)
            .LastOrDefault()?.End;

    private static DateTime StartOfWeek(DateTime date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    private static int MondayBasedIndex(DayOfWeek day) => ((int)day + 6) % 7;

    private static DateTimeOffset ToLocalOffset(DateTime localDateTime)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TimeZoneInfo.Local.GetUtcOffset(unspecified));
    }
}
