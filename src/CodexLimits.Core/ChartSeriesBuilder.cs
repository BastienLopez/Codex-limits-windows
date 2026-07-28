namespace CodexLimits.Core;

public static class ChartSeriesBuilder
{
    public static ChartState Build(
        UsageSnapshot snapshot,
        IReadOnlyList<UsageSample> samples,
        Forecast forecast,
        double safetyBuffer,
        AppSettings settings)
    {
        var window = snapshot.MainLimit.Window;
        var cycleIntervals = ScheduleMath.GetCurrentCycleIntervals(snapshot.FetchedAt, settings);
        var actual = BuildActual(window, samples, snapshot.TokenHistory, snapshot.FetchedAt);

        var target = cycleIntervals.Count > 0
            ? BuildTarget(cycleIntervals, safetyBuffer)
            : Array.Empty<ChartPoint>();

        var currentProjection = cycleIntervals.Count > 0
            ? Projection(
                snapshot.FetchedAt,
                window.RemainingPercent,
                forecast.CurrentPercentPerDay,
                cycleIntervals,
                settings)
            : Array.Empty<ChartPoint>();

        var historicalProjection = cycleIntervals.Count > 0
            ? Projection(
                snapshot.FetchedAt,
                window.RemainingPercent,
                forecast.HistoricalPercentPerDay,
                cycleIntervals,
                settings)
            : Array.Empty<ChartPoint>();

        return new ChartState(
            window,
            snapshot.FetchedAt,
            safetyBuffer,
            target,
            actual,
            currentProjection,
            historicalProjection,
            ScheduleMath.GetInactiveIntervals(window.StartsAt, window.ResetsAt, settings));
    }

    private static IReadOnlyList<ChartPoint> BuildTarget(
        IReadOnlyList<TimeRange> intervals,
        double safetyBuffer)
    {
        if (intervals.Count == 0)
        {
            return Array.Empty<ChartPoint>();
        }

        var dropPerDay = (100d - safetyBuffer) / intervals.Count;
        var remaining = 100d;
        var points = new List<ChartPoint> { new(intervals[0].Start, remaining) };

        foreach (var interval in intervals)
        {
            if (points[^1].Time < interval.Start)
            {
                points.Add(new ChartPoint(interval.Start, remaining));
            }

            remaining = Math.Max(remaining - dropPerDay, safetyBuffer);
            points.Add(new ChartPoint(interval.End, remaining));
        }

        return points;
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
        DateTimeOffset now,
        double remainingNow,
        double percentPerDay,
        IReadOnlyList<TimeRange> cycleIntervals,
        AppSettings settings)
    {
        if (cycleIntervals.Count == 0)
        {
            return Array.Empty<ChartPoint>();
        }

        var cycleEnd = cycleIntervals[^1].End;
        var points = new List<ChartPoint> { new(now, remainingNow) };
        if (now >= cycleEnd)
        {
            return points;
        }

        var dailyHours = ScheduleMath.GetNominalDailyHours(settings);
        var remaining = remainingNow;

        foreach (var interval in cycleIntervals)
        {
            if (interval.End <= now)
            {
                continue;
            }

            var effectiveStart = interval.Start > now ? interval.Start : now;
            if (points[^1].Time < effectiveStart)
            {
                points.Add(new ChartPoint(effectiveStart, remaining));
            }

            var activeFraction = (interval.End - effectiveStart).TotalHours / dailyHours;
            remaining = Math.Max(remaining - percentPerDay * activeFraction, 0);
            points.Add(new ChartPoint(interval.End, remaining));
        }

        return points
            .GroupBy(point => point.Time)
            .Select(group => group.Last())
            .OrderBy(point => point.Time)
            .ToArray();
    }
}
