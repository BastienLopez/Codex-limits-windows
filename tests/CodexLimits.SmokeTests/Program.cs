using CodexLimits.Core;

var offset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 7, 29, 12, 0, 0));
var now = new DateTimeOffset(2026, 7, 29, 12, 0, 0, offset);
var window = new UsageWindow(63, now.AddDays(5), 7 * 24 * 60);
var schedule = WorkScheduleSettings.Default;
var samples = new[]
{
    new UsageSample(now.AddDays(-2), 100, window.ResetsAt),
    new UsageSample(now.AddDays(-1), 82, window.ResetsAt),
    new UsageSample(now, 63, window.ResetsAt)
};

Assert(schedule.IsActive(now), "Le mercredi à midi doit être dans la période active par défaut.");
Assert(!schedule.IsActive(now.Date.AddDays(3).AddHours(12)), "Le samedi doit être exclu par défaut.");
Assert(schedule.ActiveHoursBetween(now, now.AddDays(5)) > 0, "Le planning doit contenir des heures actives.");

var forecast = ForecastEngine.Evaluate(window, samples, Array.Empty<TokenDay>(), 3, schedule, now, null);
Assert(forecast.CurrentPercentPerDay > 0, "Le rythme courant doit être positif.");
Assert(forecast.RecommendedPercentPerDay > 0, "Le rythme conseillé doit être positif.");
Assert(forecast.ExpectedRemainingAtReset >= 0, "La projection ne doit pas être négative.");

var chart = ChartSeriesBuilder.Build(
    new UsageSnapshot(new LimitReading("codex", "Codex", window), Array.Empty<LimitReading>(), Array.Empty<TokenDay>(), now),
    samples,
    forecast,
    3,
    schedule);
Assert(chart.Target.Count >= 2, "La cible doit contenir plusieurs points.");
Assert(chart.Actual.Count >= 2, "La courbe réelle doit contenir plusieurs points.");
Assert(chart.CurrentProjection.Count >= 2, "La projection courante doit contenir au moins deux points.");

Console.WriteLine("Smoke tests OK");
return;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
