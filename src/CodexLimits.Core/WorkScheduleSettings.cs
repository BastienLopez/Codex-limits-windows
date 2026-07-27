using System.Globalization;

namespace CodexLimits.Core;

public sealed record ActiveInterval(DateTimeOffset Start, DateTimeOffset End);

public sealed record WorkScheduleSettings(
    bool Monday,
    bool Tuesday,
    bool Wednesday,
    bool Thursday,
    bool Friday,
    bool Saturday,
    bool Sunday,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int RefreshMinutes)
{
    public static WorkScheduleSettings Default { get; } = new(
        Monday: true,
        Tuesday: true,
        Wednesday: true,
        Thursday: true,
        Friday: true,
        Saturday: false,
        Sunday: false,
        StartTime: new TimeSpan(9, 0, 0),
        EndTime: new TimeSpan(18, 0, 0),
        RefreshMinutes: 30);

    public double DailyActiveHours => Math.Max((EndTime - StartTime).TotalHours, 0);

    public bool IsValid =>
        SelectedDayCount > 0 &&
        StartTime >= TimeSpan.Zero &&
        EndTime <= TimeSpan.FromDays(1) &&
        EndTime > StartTime &&
        RefreshMinutes is >= 5 and <= 240;

    public int SelectedDayCount => new[]
    {
        Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
    }.Count(selected => selected);

    public bool IsWorkingDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Monday,
        DayOfWeek.Tuesday => Tuesday,
        DayOfWeek.Wednesday => Wednesday,
        DayOfWeek.Thursday => Thursday,
        DayOfWeek.Friday => Friday,
        DayOfWeek.Saturday => Saturday,
        DayOfWeek.Sunday => Sunday,
        _ => false
    };

    public bool IsActive(DateTimeOffset instant)
    {
        var local = instant.ToLocalTime();
        return IsWorkingDay(local.DayOfWeek) &&
               local.TimeOfDay >= StartTime &&
               local.TimeOfDay < EndTime;
    }

    public IReadOnlyList<ActiveInterval> GetActiveIntervals(DateTimeOffset start, DateTimeOffset end)
    {
        if (!IsValid || end <= start)
        {
            return Array.Empty<ActiveInterval>();
        }

        var intervals = new List<ActiveInterval>();
        var firstDate = start.ToLocalTime().Date.AddDays(-1);
        var lastDate = end.ToLocalTime().Date.AddDays(1);

        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            if (!IsWorkingDay(date.DayOfWeek))
            {
                continue;
            }

            var intervalStart = AtLocalTime(date, StartTime);
            var intervalEnd = AtLocalTime(date, EndTime);
            var clippedStart = intervalStart < start ? start : intervalStart;
            var clippedEnd = intervalEnd > end ? end : intervalEnd;

            if (clippedEnd > clippedStart)
            {
                intervals.Add(new ActiveInterval(clippedStart, clippedEnd));
            }
        }

        return intervals;
    }

    public double ActiveHoursBetween(DateTimeOffset start, DateTimeOffset end) =>
        GetActiveIntervals(start, end).Sum(interval => (interval.End - interval.Start).TotalHours);

    public double ActiveEquivalentDaysBetween(DateTimeOffset start, DateTimeOffset end)
    {
        if (DailyActiveHours <= 0)
        {
            return 0;
        }

        return ActiveHoursBetween(start, end) / DailyActiveHours;
    }

    public DateTimeOffset NextActiveStart(DateTimeOffset from)
    {
        if (IsActive(from))
        {
            return from;
        }

        var local = from.ToLocalTime();
        for (var offset = 0; offset < 14; offset++)
        {
            var date = local.Date.AddDays(offset);
            if (!IsWorkingDay(date.DayOfWeek))
            {
                continue;
            }

            var candidate = AtLocalTime(date, StartTime);
            if (candidate >= from)
            {
                return candidate;
            }
        }

        return from;
    }

    public DateTimeOffset AddActiveHours(DateTimeOffset from, double activeHours)
    {
        if (activeHours <= 0 || !IsValid)
        {
            return from;
        }

        var remaining = activeHours;
        var cursor = from;

        for (var day = 0; day < 370 && remaining > 0.000001; day++)
        {
            var horizon = cursor.AddDays(14);
            var intervals = GetActiveIntervals(cursor, horizon);
            if (intervals.Count == 0)
            {
                return cursor;
            }

            foreach (var interval in intervals)
            {
                var available = (interval.End - interval.Start).TotalHours;
                if (remaining <= available)
                {
                    return interval.Start.AddHours(remaining);
                }

                remaining -= available;
                cursor = interval.End.AddTicks(1);
            }
        }

        return cursor;
    }

    public string DaysSummary
    {
        get
        {
            if (Monday && Tuesday && Wednesday && Thursday && Friday && !Saturday && !Sunday)
            {
                return "Lun–ven";
            }

            var days = new List<string>();
            if (Monday) days.Add("lun.");
            if (Tuesday) days.Add("mar.");
            if (Wednesday) days.Add("mer.");
            if (Thursday) days.Add("jeu.");
            if (Friday) days.Add("ven.");
            if (Saturday) days.Add("sam.");
            if (Sunday) days.Add("dim.");
            return string.Join(", ", days);
        }
    }

    public string Summary =>
        $"{DaysSummary} · {FormatTime(StartTime)}–{FormatTime(EndTime)} · actualisation {RefreshMinutes} min";

    private static DateTimeOffset AtLocalTime(DateTime date, TimeSpan time)
    {
        var localDateTime = DateTime.SpecifyKind(date.Date + time, DateTimeKind.Unspecified);
        var offset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset);
    }

    private static string FormatTime(TimeSpan time) =>
        DateTime.Today.Add(time).ToString("HH:mm", CultureInfo.InvariantCulture);
}
