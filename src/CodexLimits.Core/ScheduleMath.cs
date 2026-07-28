namespace CodexLimits.Core;

public sealed record TimeRange(DateTimeOffset Start, DateTimeOffset End);

public static class ScheduleMath
{
    public static bool IsActive(DateTimeOffset instant, AppSettings settings)
    {
        var normalized = settings.Normalize();
        return GetIntervals(instant.AddDays(-1), instant.AddDays(1), normalized)
            .Any(interval => instant >= interval.Start && instant < interval.End);
    }

    public static DateTimeOffset GetNextStart(DateTimeOffset instant, AppSettings settings)
    {
        var normalized = settings.Normalize();
        var candidate = GetIntervals(instant.AddDays(-1), instant.AddDays(21), normalized)
            .Where(interval => interval.End > instant)
            .OrderBy(interval => interval.Start)
            .FirstOrDefault();

        if (candidate is null)
        {
            throw new InvalidOperationException("Impossible de calculer le prochain créneau actif.");
        }

        return instant < candidate.Start ? candidate.Start : instant;
    }

    public static IReadOnlyList<TimeRange> GetCurrentCycleIntervals(
        DateTimeOffset reference,
        AppSettings settings)
    {
        var normalized = settings.Normalize();
        var referenceDate = reference.ToLocalTime().Date;
        var startIndex = MondayBasedIndex(normalized.StartDay);
        var endIndex = MondayBasedIndex(normalized.EndDay);
        var referenceIndex = MondayBasedIndex(referenceDate.DayOfWeek);
        var daysSinceStart = (referenceIndex - startIndex + 7) % 7;
        var cycleStartDate = referenceDate.AddDays(-daysSinceStart);
        var dayCount = ((endIndex - startIndex + 7) % 7) + 1;
        var intervals = new List<TimeRange>(dayCount);

        for (var offset = 0; offset < dayCount; offset++)
        {
            var localDate = cycleStartDate.AddDays(offset);
            var startLocal = localDate.Add(normalized.StartTime);
            var endLocal = localDate.Add(normalized.EndTime);
            if (endLocal <= startLocal)
            {
                endLocal = endLocal.AddDays(1);
            }

            intervals.Add(new TimeRange(ToLocalOffset(startLocal), ToLocalOffset(endLocal)));
        }

        return intervals;
    }


    public static IReadOnlyList<TimeRange> GetPlanningCycle(
        DateTimeOffset reference,
        AppSettings settings) =>
        GetCurrentCycleIntervals(reference, settings);

    public static double GetActiveHours(
        IReadOnlyList<TimeRange> intervals,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd) =>
        intervals.Sum(interval =>
        {
            var start = interval.Start > rangeStart ? interval.Start : rangeStart;
            var end = interval.End < rangeEnd ? interval.End : rangeEnd;
            return end > start ? (end - start).TotalHours : 0;
        });

    public static DateTimeOffset GetCurrentCycleStart(DateTimeOffset reference, AppSettings settings) =>
        GetCurrentCycleIntervals(reference, settings)[0].Start;

    public static DateTimeOffset GetCurrentCycleEnd(DateTimeOffset reference, AppSettings settings) =>
        GetCurrentCycleIntervals(reference, settings)[^1].End;

    public static IReadOnlyList<TimeRange> GetIntervals(
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        AppSettings settings)
    {
        if (rangeEnd <= rangeStart) return Array.Empty<TimeRange>();

        var normalized = settings.Normalize();
        var firstLocalDate = rangeStart.ToLocalTime().Date.AddDays(-1);
        var lastLocalDate = rangeEnd.ToLocalTime().Date.AddDays(1);
        var intervals = new List<TimeRange>();

        for (var localDate = firstLocalDate; localDate <= lastLocalDate; localDate = localDate.AddDays(1))
        {
            if (!IsSelectedDay(localDate.DayOfWeek, normalized.StartDay, normalized.EndDay))
            {
                continue;
            }

            var startLocal = localDate.Add(normalized.StartTime);
            var endLocal = localDate.Add(normalized.EndTime);
            if (endLocal <= startLocal)
            {
                endLocal = endLocal.AddDays(1);
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

        return intervals.OrderBy(interval => interval.Start).ToArray();
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

    public static double GetNominalDailyHours(AppSettings settings)
    {
        var normalized = settings.Normalize();
        var duration = normalized.EndTime - normalized.StartTime;
        if (duration <= TimeSpan.Zero)
        {
            duration = duration.Add(TimeSpan.FromDays(1));
        }

        return Math.Max(duration.TotalHours, 1d / 60d);
    }

    public static double GetActiveWorkDays(
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        AppSettings settings) =>
        GetActiveHours(rangeStart, rangeEnd, settings) / GetNominalDailyHours(settings);

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
        GetIntervals(rangeStart, rangeEnd, settings).LastOrDefault()?.End;

    private static bool IsSelectedDay(DayOfWeek day, DayOfWeek startDay, DayOfWeek endDay)
    {
        var dayIndex = MondayBasedIndex(day);
        var startIndex = MondayBasedIndex(startDay);
        var endIndex = MondayBasedIndex(endDay);

        return startIndex <= endIndex
            ? dayIndex >= startIndex && dayIndex <= endIndex
            : dayIndex >= startIndex || dayIndex <= endIndex;
    }

    private static int MondayBasedIndex(DayOfWeek day) => ((int)day + 6) % 7;

    private static DateTimeOffset ToLocalOffset(DateTime localDateTime)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TimeZoneInfo.Local.GetUtcOffset(unspecified));
    }
}
