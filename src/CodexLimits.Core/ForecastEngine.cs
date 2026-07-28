namespace CodexLimits.Core;

/// <summary>
/// Forecasting model adapted from the MIT-licensed thrr87/codex-limits project.
/// Rates are calculated only across the configured active work cycle.
/// </summary>
public static class ForecastEngine
{
    public static Forecast Evaluate(
        UsageWindow window,
        IReadOnlyList<UsageSample> samples,
        IReadOnlyList<TokenDay> tokenHistory,
        double safetyBuffer,
        DateTimeOffset now,
        PaceStatus? previousStatus,
        AppSettings settings)
    {
        var normalized = settings.Normalize();
        var cycleIntervals = ScheduleMath.GetCurrentCycleIntervals(now, normalized);
        if (cycleIntervals.Count == 0)
        {
            return new Forecast(
                PaceStatus.OnTrack,
                window.RemainingPercent,
                window.RemainingPercent,
                window.RemainingPercent,
                0,
                0,
                0,
                0,
                0,
                null);
        }

        var cycleStart = cycleIntervals[0].Start;
        var cycleEnd = cycleIntervals[^1].End;
        var effectiveCycleStart = window.StartsAt > cycleStart ? window.StartsAt : cycleStart;
        var dailyHours = ScheduleMath.GetNominalDailyHours(normalized);

        var activeHoursLeft = cycleIntervals
            .Sum(interval => ActiveOverlapHours(interval, now, cycleEnd));
        var activeDaysLeft = activeHoursLeft / dailyHours;

        var elapsedActiveHours = cycleIntervals
            .Sum(interval => ActiveOverlapHours(interval, effectiveCycleStart, now));
        var elapsedActiveDays = elapsedActiveHours / dailyHours;

        var currentSamples = samples
            .Where(sample =>
                sample.ResetsAt == window.ResetsAt &&
                sample.ObservedAt >= effectiveCycleStart &&
                sample.ObservedAt <= now)
            .OrderBy(sample => sample.ObservedAt)
            .ToArray();

        var usedPercent = Math.Clamp(100 - window.RemainingPercent, 0, 100);
        var stableElapsedDays = Math.Max(elapsedActiveDays, 1d);
        var cycleRate = usedPercent / stableElapsedDays;

        var currentRate = cycleRate;
        if (currentSamples.Length > 1)
        {
            var first = currentSamples[0];
            var last = currentSamples[^1];
            var recentDays = ScheduleMath.GetActiveWorkDays(first.ObservedAt, last.ObservedAt, normalized);

            // A few minutes of activity must not be extrapolated as a full-day burn rate.
            if (recentDays >= 1d)
            {
                var recentRate = Math.Max(
                    (first.RemainingPercent - last.RemainingPercent) / recentDays,
                    0);
                currentRate = 0.65 * recentRate + 0.35 * cycleRate;
            }
        }

        var historicalRates = samples
            .Where(sample => sample.ResetsAt != window.ResetsAt)
            .GroupBy(sample => sample.ResetsAt)
            .Select(group =>
            {
                var ordered = group.OrderBy(sample => sample.ObservedAt).ToArray();
                if (ordered.Length < 2)
                {
                    return (double?)null;
                }

                var activeDays = ScheduleMath.GetActiveWorkDays(
                    ordered[0].ObservedAt,
                    ordered[^1].ObservedAt,
                    normalized);
                if (activeDays < 1d)
                {
                    return null;
                }

                return Math.Max(
                    (ordered[0].RemainingPercent - ordered[^1].RemainingPercent) / activeDays,
                    0);
            })
            .Where(rate => rate.HasValue)
            .Select(rate => rate!.Value)
            .ToArray();

        var historicalRate = historicalRates.Length > 0
            ? historicalRates.Average()
            : currentRate;

        // The red projection and the status are based on the current cycle only.
        // Historical data remains a separate grey comparison curve.
        var expectedRate = currentRate;
        var safetyRate = currentRate * 1.15;
        var expected = Math.Max(window.RemainingPercent - expectedRate * activeDaysLeft, 0);
        var safety = Math.Max(window.RemainingPercent - safetyRate * activeDaysLeft, 0);
        var historical = Math.Max(window.RemainingPercent - historicalRate * activeDaysLeft, 0);
        var recommended = activeDaysLeft > 0
            ? Math.Max(window.RemainingPercent - safetyBuffer, 0) / activeDaysLeft
            : 0;

        PaceStatus status;
        if (activeDaysLeft <= 0)
        {
            status = PaceStatus.OnTrack;
        }
        else if (
            currentRate > recommended * 1.05 ||
            expected < safetyBuffer ||
            (previousStatus == PaceStatus.SlowDown && currentRate > recommended))
        {
            status = PaceStatus.SlowDown;
        }
        else if (
            currentRate < recommended * 0.80 &&
            expected > safetyBuffer + 8)
        {
            status = PaceStatus.RoomToUseMore;
        }
        else
        {
            status = PaceStatus.OnTrack;
        }

        DateTimeOffset? exhaustionAt = null;
        if (currentRate > 0)
        {
            var hoursToEmpty = window.RemainingPercent / currentRate * dailyHours;
            exhaustionAt = ScheduleMath.AddActiveHours(now, hoursToEmpty, cycleEnd, normalized);
        }

        return new Forecast(
            status,
            expected,
            safety,
            historical,
            recommended,
            currentRate,
            historicalRate,
            safetyRate,
            activeHoursLeft,
            exhaustionAt);
    }

    private static double ActiveOverlapHours(
        TimeRange interval,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd)
    {
        var start = interval.Start > rangeStart ? interval.Start : rangeStart;
        var end = interval.End < rangeEnd ? interval.End : rangeEnd;
        return end > start ? (end - start).TotalHours : 0;
    }
}
