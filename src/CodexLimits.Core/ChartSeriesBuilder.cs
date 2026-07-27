namespace CodexLimits.Core;

public static class ChartSeriesBuilder
{
    public static ChartState Build(
        UsageSnapshot snapshot,
        IReadOnlyList<UsageSample> samples,
        Forecast forecast,
        double safetyBuffer,
        WorkScheduleSettings schedule)
    {
        var window = snapshot.MainLimit.Window;
        var target = ScheduledLine(window, window.StartsAt, 100, safetyBuffer, schedule);
        var actual = BuildActual(window, samples, snapshot.TokenHistory, snapshot.FetchedAt);
        var currentProjection = Projection(
            window,
            snapshot.FetchedAt,
            window.RemainingPercent,
            forecast.CurrentPercentPerDay,
            forecast.ExpectedRemainingAtReset,
            schedule);
        var historicalProjection = Projection(
            window,
            snapshot.FetchedAt,
            window.RemainingPercent,
            forecast.HistoricalPercentPerDay,
            forecast.HistoricalRemainingAtReset,
            schedule);

        return new ChartState(
            window,
            snapshot.FetchedAt,
            safetyBuffer,
            target,
            actual,
            currentProjection,
            historicalProjection);
    }

    private static IReadOnlyList<ChartPoint> BuildActual(
        UsageWindow window,
        IReadOnlyList<UsageSample> samples,
        IReadOnlyList<TokenDay> tokenHistory,
        DateTimeOffset fetchedAt)
    {
        var current = new ChartPoint(fetchedAt, window.RemainingPercent);
        var local = samples
            .Where(sample => sample.ResetsAt == window.ResetsAt)
            .Where(sample => sample.ObservedAt > window.StartsAt && sample.ObservedAt < fetchedAt)
            .OrderBy(sample => sample.ObservedAt)
            .Select(sample => new ChartPoint(sample.ObservedAt, sample.RemainingPercent))
            .ToList();

        var firstKnown = local.FirstOrDefault() ?? current;
        var tokenBuckets = tokenHistory
            .Where(bucket => bucket.Date.AddDays(1) > window.StartsAt && bucket.Date < firstKnown.Time)
            .OrderBy(bucket => bucket.Date)
            .ToArray();
        var totalTokens = tokenBuckets.Sum(bucket => bucket.Tokens);
        var bootstrapped = new List<ChartPoint>();

        if (totalTokens > 0)
        {
            long cumulativeTokens = 0;
            foreach (var bucket in tokenBuckets)
            {
                cumulativeTokens += bucket.Tokens;
                var time = bucket.Date.AddDays(1);
                if (time < window.StartsAt) time = window.StartsAt;
                if (time > firstKnown.Time) time = firstKnown.Time;
                var used = (100 - firstKnown.RemainingPercent) * cumulativeTokens / totalTokens;
                bootstrapped.Add(new ChartPoint(time, 100 - used));
            }
        }

        return new[] { new ChartPoint(window.StartsAt, 100) }
            .Concat(bootstrapped)
            .Concat(local)
            .Append(current)
            .GroupBy(point => point.Time)
            .Select(group => group.Last())
            .OrderBy(point => point.Time)
            .ToArray();
    }

    private static IReadOnlyList<ChartPoint> Projection(
        UsageWindow window,
        DateTimeOffset now,
        double remainingNow,
        double percentPerWorkingDay,
        double remainingAtReset,
        WorkScheduleSettings schedule)
    {
        if (now >= window.ResetsAt)
        {
            return new[] { new ChartPoint(now, remainingNow) };
        }

        if (percentPerWorkingDay <= 0 || schedule.DailyActiveHours <= 0)
        {
            return new[]
            {
                new ChartPoint(now, remainingNow),
                new ChartPoint(window.ResetsAt, Math.Clamp(remainingAtReset, 0, 100))
            };
        }

        var points = new List<ChartPoint> { new(now, remainingNow) };
        var percentPerHour = percentPerWorkingDay / schedule.DailyActiveHours;
        var remaining = remainingNow;
        var lastTime = now;

        foreach (var interval in schedule.GetActiveIntervals(now, window.ResetsAt))
        {
            if (interval.Start > lastTime)
            {
                points.Add(new ChartPoint(interval.Start, remaining));
            }

            var availableHours = (interval.End - interval.Start).TotalHours;
            var consumption = availableHours * percentPerHour;
            if (consumption >= remaining)
            {
                var hoursToEmpty = remaining / percentPerHour;
                points.Add(new ChartPoint(interval.Start.AddHours(hoursToEmpty), 0));
                return points;
            }

            remaining -= consumption;
            points.Add(new ChartPoint(interval.End, remaining));
            lastTime = interval.End;
        }

        if (points[^1].Time < window.ResetsAt)
        {
            points.Add(new ChartPoint(window.ResetsAt, remaining));
        }

        points[^1] = new ChartPoint(points[^1].Time, Math.Clamp(remainingAtReset, 0, 100));
        return points;
    }

    private static IReadOnlyList<ChartPoint> ScheduledLine(
        UsageWindow window,
        DateTimeOffset start,
        double remainingAtStart,
        double remainingAtEnd,
        WorkScheduleSettings schedule)
    {
        var intervals = schedule.GetActiveIntervals(start, window.ResetsAt);
        var totalHours = intervals.Sum(interval => (interval.End - interval.Start).TotalHours);
        if (totalHours <= 0)
        {
            return new[]
            {
                new ChartPoint(start, remainingAtStart),
                new ChartPoint(window.ResetsAt, remainingAtEnd)
            };
        }

        var points = new List<ChartPoint> { new(start, remainingAtStart) };
        var remaining = remainingAtStart;
        var dropPerHour = (remainingAtStart - remainingAtEnd) / totalHours;
        var lastTime = start;

        foreach (var interval in intervals)
        {
            if (interval.Start > lastTime)
            {
                points.Add(new ChartPoint(interval.Start, remaining));
            }

            remaining -= (interval.End - interval.Start).TotalHours * dropPerHour;
            points.Add(new ChartPoint(interval.End, remaining));
            lastTime = interval.End;
        }

        if (points[^1].Time < window.ResetsAt)
        {
            points.Add(new ChartPoint(window.ResetsAt, remainingAtEnd));
        }
        else
        {
            points[^1] = new ChartPoint(points[^1].Time, remainingAtEnd);
        }

        return points;
    }
}
