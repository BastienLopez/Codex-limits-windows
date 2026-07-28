using CodexLimits.Core;

var settings = new AppSettings
{
    StartDay = DayOfWeek.Monday,
    EndDay = DayOfWeek.Friday,
    StartTime = new TimeSpan(9, 0, 0),
    EndTime = new TimeSpan(18, 0, 0),
    SafetyBuffer = 3
};

var monday = LocalOffset(new DateTime(2026, 7, 27, 9, 0, 0, DateTimeKind.Unspecified));
var fridayEnd = LocalOffset(new DateTime(2026, 7, 31, 18, 0, 0, DateTimeKind.Unspecified));

var cycle = ScheduleMath.GetCurrentCycleIntervals(
    LocalOffset(new DateTime(2026, 7, 28, 11, 0, 0, DateTimeKind.Unspecified)),
    settings);

Assert(cycle.Count == 5, "Le graphique doit contenir exactement cinq jours travaillés.");
Assert(cycle[0].Start == monday, "Le cycle doit commencer lundi à 09:00.");
Assert(cycle[^1].End == fridayEnd, "Le cycle doit se terminer vendredi à 18:00.");
Assert(Math.Abs(ScheduleMath.GetActiveHours(monday, fridayEnd, settings) - 45) < 0.01,
    "Le planning doit contenir 45 heures actives.");

// Cas réel typique : le quota Codex redémarre mardi matin, au milieu du planning local.
var now = LocalOffset(new DateTime(2026, 7, 28, 11, 0, 0, DateTimeKind.Unspecified));
var windowStart = LocalOffset(new DateTime(2026, 7, 28, 9, 58, 0, DateTimeKind.Unspecified));
var reset = LocalOffset(new DateTime(2026, 8, 4, 9, 58, 0, DateTimeKind.Unspecified));
var window = new UsageWindow(88, reset, (int)(reset - windowStart).TotalMinutes);
var samples = new[]
{
    new UsageSample(windowStart, 100, reset),
    new UsageSample(now, 88, reset)
};

var forecast = ForecastEngine.Evaluate(
    window,
    samples,
    Array.Empty<TokenDay>(),
    settings.SafetyBuffer,
    now,
    null,
    settings);

Assert(forecast.Status == PaceStatus.RoomToUseMore,
    "Douze pour cent utilisés en début de cycle ne doivent pas déclencher un faux statut Risque.");
Assert(forecast.CurrentPercentPerDay is >= 11.9 and <= 12.1,
    "Le rythme initial doit être stabilisé sur au moins un jour travaillé.");
Assert(forecast.ExpectedRemainingAtReset > settings.SafetyBuffer,
    "La projection doit conserver une marge avant vendredi 18:00.");
Assert(forecast.ActiveHoursLeft > 0,
    "Il doit rester des heures actives dans le planning.");

var snapshot = new UsageSnapshot(
    new LimitReading("codex", "Codex", window),
    Array.Empty<LimitReading>(),
    Array.Empty<TokenDay>(),
    now);

var chart = ChartSeriesBuilder.Build(
    snapshot,
    samples,
    forecast,
    settings.SafetyBuffer,
    settings);

Assert(chart.Target.Count >= 10,
    "La cible doit contenir les paliers et les chutes des cinq jours.");
Assert(chart.Actual.Count >= 2,
    "La courbe réelle doit contenir le début du quota et le point actuel.");
Assert(chart.CurrentProjection.Count >= 2,
    "La projection doit aller du point actuel jusqu'à vendredi.");

AssertDropsOnlyAtWorkdayEnds(chart.Target, cycle, "cible");
AssertDropsOnlyAtWorkdayEnds(chart.CurrentProjection, cycle, "projection");

var projectionEnd = chart.CurrentProjection[^1];
Assert(projectionEnd.Time == fridayEnd,
    "La projection doit se terminer exactement vendredi à 18:00.");
Assert(Math.Abs(projectionEnd.RemainingPercent - forecast.ExpectedRemainingAtReset) < 0.01,
    "La fin de la courbe rouge doit correspondre au pourcentage annoncé dans le message.");

Console.WriteLine("Smoke tests OK");
return;

static void AssertDropsOnlyAtWorkdayEnds(
    IReadOnlyList<ChartPoint> points,
    IReadOnlyList<TimeRange> intervals,
    string name)
{
    for (var index = 1; index < points.Count; index++)
    {
        if (points[index].RemainingPercent >= points[index - 1].RemainingPercent - 0.001)
        {
            continue;
        }

        Assert(intervals.Any(interval => interval.End == points[index].Time),
            $"La {name} ne doit descendre qu'à la fin d'une journée travaillée.");
    }
}

static DateTimeOffset LocalOffset(DateTime value) =>
    new(value, TimeZoneInfo.Local.GetUtcOffset(value));

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
