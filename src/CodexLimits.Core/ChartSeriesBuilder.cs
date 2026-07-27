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
        var actual = BuildActual(window, samples, snapshot.TokenHistory, snapshot.FetchedAt);
        var target = BuildTarget(window, safetyBuffer, settings);
        var currentProjection = Projection(
            window,
            snapshot.FetchedAt,
            window.RemainingPercent,
            forecast.CurrentPercentPerDay,
            forecast.ExpectedRemainingAtReset,
            settings);
        var historicalProjection = Projection(
            window,
            snapshot.FetchedAt,
            window.RemainingPercent,
            forecast.HistoricalPercentPerDay,
            forecast.HistoricalRemainingAtReset,
            settings);

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
        UsageWindow window,
        double safetyBuffer,
        AppSettings settings)
    {
        var intervals = ScheduleMath.GetIntervals(window.StartsAt, window.ResetsAt, settings);
        var totalHours = intervals.Sum(interval => (interval.End - interval.Start).TotalHours);
        var points = new List<ChartPoint> { new(window.StartsAt, 100) };

        if (totalHours <= 0)
        {
            points.Add(new ChartPoint(window.ResetsAt, 100));
            return points;
        }

        var consumedHours = 0d;
        var targetRemaining = 100d;
        foreach (var interval in intervals)
        {
            if (points[^1].Time < interval.Start)
            {
                points.Add(new ChartPoint(interval.Start, targetRemaining));
            }

            consumedHours += (interval.End - interval.Start).TotalHours;
            targetRemaining = 100 - (100 - safetyBuffer) * consumedHours / totalHours;
            points.Add(new ChartPoint(interval.End, Math.Clamp(targetRemaining, safetyBuffer, 100)));
        }

        if (points[^1].Time < window.ResetsAt)
        {
            points.Add(new ChartPoint(window.ResetsAt, Math.Clamp(targetRemaining, safetyBuffer, 100)));
        }

        return points
            .GroupBy(point => point.Time)
            .Select(group => group.Last())
            .OrderBy(point => point.Time)
            .ToArray();
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
        double percentPerDay,
        double remainingAtReset,
        AppSettings settings)
    {
        var points = new List<ChartPoint> { new(now, remainingNow) };
        if (now >= window.ResetsAt)
        {
            return points;
        }

        var remaining = remainingNow;
        var percentPerHour = Math.Max(percentPerDay, 0) / ScheduleMath.GetNominalDailyHours(settings);
        foreach (var interval in ScheduleMath.GetIntervals(now, window.ResetsAt, settings))
        {
            if (points[^1].Time < interval.Start)
            {
                points.Add(new ChartPoint(interval.Start, remaining));
            }

            var availableHours = (interval.End - interval.Start).TotalHours;
            if (percentPerHour > 0 && remaining - percentPerHour * availableHours <= 0)
            {
                points.Add(new ChartPoint(interval.Start.AddHours(remaining / percentPerHour), 0));
                return points;
            }

            remaining = Math.Max(remaining - percentPerHour * availableHours, 0);
            points.Add(new ChartPoint(interval.End, remaining));
        }

        if (points[^1].Time < window.ResetsAt)
        {
            points.Add(new ChartPoint(window.ResetsAt, Math.Clamp(remainingAtReset, 0, 100)));
        }

        return points
            .GroupBy(point => point.Time)
            .Select(group => group.Last())
            .OrderBy(point => point.Time)
            .ToArray();
    }
}
