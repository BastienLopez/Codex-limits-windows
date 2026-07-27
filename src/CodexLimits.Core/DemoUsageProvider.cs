namespace CodexLimits.Core;

public sealed class DemoUsageProvider : IUsageProvider
{
    private readonly DateTimeOffset _start = DateTimeOffset.Now;

    public Task<UsageSnapshot> FetchAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.Now;
        var reset = _start.AddDays(5.5);
        if (reset <= now) reset = reset.AddDays(7);
        var durationMinutes = 7 * 24 * 60;
        var elapsedHours = Math.Max((now - _start).TotalHours, 0);
        var remaining = Math.Clamp(63 - elapsedHours * 0.3, 0, 100);

        var snapshot = new UsageSnapshot(
            new LimitReading("codex", "Codex", new UsageWindow(remaining, reset, durationMinutes)),
            new[]
            {
                new LimitReading(
                    "gpt-5.3-codex-spark",
                    "GPT-5.3-Codex-Spark",
                    new UsageWindow(100, now.AddDays(1), 24 * 60))
            },
            Array.Empty<TokenDay>(),
            now);
        return Task.FromResult(snapshot);
    }

    public static IReadOnlyList<UsageSample> CreateSeedSamples(UsageWindow window, DateTimeOffset now)
    {
        var points = new[]
        {
            (window.StartsAt, 100d),
            (now.AddHours(-18), 88d),
            (now.AddHours(-12), 79d),
            (now.AddHours(-6), 71d),
            (now, window.RemainingPercent)
        };

        return points
            .Where(point => point.Item1 >= window.StartsAt && point.Item1 <= now)
            .Select(point => new UsageSample(point.Item1, point.Item2, window.ResetsAt))
            .ToArray();
    }
}
