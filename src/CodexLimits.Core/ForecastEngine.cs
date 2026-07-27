namespace CodexLimits.Core;

/// <summary>
/// Forecasting model adapted from the MIT-licensed thrr87/codex-limits project.
/// Rates are calculated only across the configured active weekly schedule.
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
        var activeHoursLeft = ScheduleMath.GetActiveHours(now, window.ResetsAt, normalized);
        var activeDaysLeft = ScheduleMath.GetActiveWorkDays(now, window.ResetsAt, normalized);
        var currentSamples = samples
            .Where(sample => sample.ResetsAt == window.ResetsAt && sample.ObservedAt <= now)
            .OrderBy(sample => sample.ObservedAt)
            .ToArray();

        var elapsedActiveDays = ScheduleMath.GetActiveWorkDays(window.StartsAt, now, normalized);
        var windowRate = elapsedActiveDays > 1d / 96d
            ? Math.Max((100 - window.RemainingPercent) / elapsedActiveDays, 0)
            : 0;

        double recentRate;
        if (currentSamples.Length > 1 && currentSamples[^1].ObservedAt > currentSamples[0].ObservedAt)
        {
            var activeDays = ScheduleMath.GetActiveHours(
                currentSamples[0].ObservedAt,
                currentSamples[^1].ObservedAt,
                normalized) / 24d;
            recentRate = activeDays > 1d / 96d
                ? Math.Max((currentSamples[0].RemainingPercent - currentSamples[^1].RemainingPercent) / activeDays, 0)
                : windowRate;
        }
        else
        {
            recentRate = windowRate;
        }

        var currentRate = currentSamples.Length > 1
            ? 0.7 * recentRate + 0.3 * windowRate
            : windowRate;

        var historicalRates = samples
            .Where(sample => sample.ResetsAt != window.ResetsAt)
            .GroupBy(sample => sample.ResetsAt)
            .Select(group =>
            {
                var ordered = group.OrderBy(sample => sample.ObservedAt).ToArray();
                if (ordered.Length < 2 || ordered[^1].ObservedAt <= ordered[0].ObservedAt)
                {
                    return (double?)null;
                }

                var activeDays = ScheduleMath.GetActiveWorkDays(
                    ordered[0].ObservedAt,
                    ordered[^1].ObservedAt,
                    normalized);
                return activeDays > 1d / 96d
                    ? Math.Max((ordered[0].RemainingPercent - ordered[^1].RemainingPercent) / activeDays, 0)
                    : null;
            })
            .Where(rate => rate.HasValue)
            .Select(rate => rate!.Value)
            .ToArray();

        double historicalRate;
        if (historicalRates.Length == 0)
        {
            historicalRate = TokenBootstrapRate(window, windowRate, tokenHistory, now) ?? currentRate;
        }
        else
        {
            historicalRate = historicalRates.Average();
        }

        var expectedRate = 0.75 * currentRate + 0.25 * historicalRate;
        var safetyRate = Math.Max(currentRate, historicalRate) * 1.2;
        var expected = Math.Max(window.RemainingPercent - expectedRate * activeDaysLeft, 0);
        var safety = Math.Max(window.RemainingPercent - safetyRate * activeDaysLeft, 0);
        var historical = Math.Max(window.RemainingPercent - historicalRate * activeDaysLeft, 0);
        var recommended = activeDaysLeft > 0
            ? Math.Max(window.RemainingPercent - safetyBuffer, 0) / activeDaysLeft
            : 0;

        PaceStatus status;
        if (safety < safetyBuffer || (previousStatus == PaceStatus.SlowDown && safety < safetyBuffer + 1))
        {
            status = PaceStatus.SlowDown;
        }
        else if (expected > 8 || (previousStatus == PaceStatus.RoomToUseMore && expected > 7))
        {
            status = PaceStatus.RoomToUseMore;
        }
        else
        {
            status = PaceStatus.OnTrack;
        }

        DateTimeOffset? exhaustionAt = null;
        if (expectedRate > 0)
        {
            var hoursPerWorkDay = ScheduleMath.GetNominalDailyHours(normalized);
            var hoursToEmpty = window.RemainingPercent / expectedRate * hoursPerWorkDay;
            exhaustionAt = ScheduleMath.AddActiveHours(now, hoursToEmpty, window.ResetsAt, normalized);
        }

        return new Forecast(
            status,
            expected,
            safety,
            historical,
            recommended,
            expectedRate,
            historicalRate,
            safetyRate,
            activeHoursLeft,
            exhaustionAt);
    }

    private static double? TokenBootstrapRate(
        UsageWindow window,
        double windowRate,
        IReadOnlyList<TokenDay> tokenHistory,
        DateTimeOffset now)
    {
        static int DayNumber(DateTimeOffset value) =>
            (int)Math.Floor(value.ToUnixTimeSeconds() / 86_400d);

        var start = DayNumber(window.StartsAt);
        var today = DayNumber(now);
        var buckets = tokenHistory
            .GroupBy(day => DayNumber(day.Date))
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Tokens));

        if (buckets.Count == 0 || windowRate <= 0)
        {
            return null;
        }

        var first = buckets.Keys.Min();
        var latestCandidates = buckets.Keys.Where(day => day < today).ToArray();
        if (latestCandidates.Length == 0)
        {
            return null;
        }

        var latest = latestCandidates.Max();
        if (latest < start)
        {
            return null;
        }

        var currentCount = latest - start + 1;
        long currentTokens = 0;
        for (var day = start; day <= latest; day++)
        {
            currentTokens += buckets.GetValueOrDefault(day);
        }

        var historyEnd = start - 1;
        var historyStart = Math.Max(first, historyEnd - 27);
        if (historyStart > historyEnd || currentTokens <= 0)
        {
            return null;
        }

        var historyCount = historyEnd - historyStart + 1;
        long historyTokens = 0;
        for (var day = historyStart; day <= historyEnd; day++)
        {
            historyTokens += buckets.GetValueOrDefault(day);
        }

        var currentAverage = (double)currentTokens / currentCount;
        var historicalAverage = (double)historyTokens / historyCount;
        if (currentAverage <= 0 || historicalAverage <= 0)
        {
            return null;
        }

        var relativePace = Math.Clamp(historicalAverage / currentAverage, 0.25, 4);
        return windowRate * relativePace;
    }
}
