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

var availableBalance = DailyQuotaMath.Evaluate(78, now, cycle, settings.SafetyBuffer);
Assert(Math.Abs(availableBalance.AvailablePercent - 16.8) < 0.01,
    "Le résumé doit indiquer le quota encore utilisable aujourd'hui avant de dépasser la cible.");
Assert(availableBalance.ExceededPercent < 0.01,
    "Aucun dépassement ne doit être annoncé lorsque le quota reste au-dessus de la cible du jour.");

var exceededBalance = DailyQuotaMath.Evaluate(59, now, cycle, settings.SafetyBuffer);
Assert(Math.Abs(exceededBalance.ExceededPercent - 2.2) < 0.01,
    "Le résumé doit calculer le dépassement par rapport à la cible de fin de journée.");
Assert(exceededBalance.AvailablePercent < 0.01,
    "Le quota encore utilisable aujourd'hui doit être nul lorsque la cible journalière est dépassée.");

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
Assert(forecast.EstimatedExhaustionAt is null,
    "Un rythme modéré ne doit pas annoncer un épuisement avant le reset.");

var exhaustionWindow = new UsageWindow(55, reset, (int)(reset - windowStart).TotalMinutes);
var exhaustionForecast = ForecastEngine.Evaluate(
    exhaustionWindow,
    new[]
    {
        new UsageSample(windowStart, 100, reset),
        new UsageSample(now, 55, reset)
    },
    Array.Empty<TokenDay>(),
    settings.SafetyBuffer,
    now,
    null,
    settings);

var expectedExhaustion = LocalOffset(new DateTime(2026, 7, 29, 13, 0, 0, DateTimeKind.Unspecified));
Assert(exhaustionForecast.Status == PaceStatus.SlowDown,
    "Une consommation de 45 % au début du cycle doit déclencher le statut Risque.");
Assert(exhaustionForecast.EstimatedExhaustionAt == expectedExhaustion,
    "L'épuisement doit être calculé sur les créneaux actifs et tomber mercredi à 13:00.");

var nextWeekWindow = new UsageWindow(80, reset, (int)(reset - windowStart).TotalMinutes);
var nextWeekForecast = ForecastEngine.Evaluate(
    nextWeekWindow,
    new[]
    {
        new UsageSample(windowStart, 100, reset),
        new UsageSample(now, 80, reset)
    },
    Array.Empty<TokenDay>(),
    settings.SafetyBuffer,
    now,
    null,
    settings);

var expectedNextWeekExhaustion = LocalOffset(new DateTime(2026, 8, 3, 11, 0, 0, DateTimeKind.Unspecified));
Assert(nextWeekForecast.EstimatedExhaustionAt == expectedNextWeekExhaustion,
    "La recherche d'épuisement doit continuer après vendredi jusqu'au reset Codex.");
Assert(nextWeekForecast.Status == PaceStatus.SlowDown,
    "Un épuisement prévu avant le reset doit toujours déclencher le statut Risque.");

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

var historyDirectory = Path.Combine(Path.GetTempPath(), "CodexLimitsSmokeTests", Guid.NewGuid().ToString("N"));
try
{
    var historyStore = new UsageHistoryStore(historyDirectory);
    var historyNow = DateTimeOffset.UtcNow;
    var recentSample = new UsageSample(historyNow.AddDays(-1), 75, historyNow.AddDays(6));
    var expiredSample = new UsageSample(historyNow.AddDays(-100), 90, historyNow.AddDays(-93));

    await historyStore.RecordAsync(recentSample);
    await historyStore.RecordAsync(expiredSample);

    var retained = await historyStore.LoadAsync(retentionDays: 90);
    Assert(retained.Any(sample => sample.ObservedAt == recentSample.ObservedAt),
        "Un échantillon récent doit être conservé.");
    Assert(retained.All(sample => sample.ObservedAt != expiredSample.ObservedAt),
        "Un échantillon de plus de 90 jours ne doit pas être chargé.");

    var expiredFile = Path.Combine(historyDirectory, $"{expiredSample.ObservedAt.UtcDateTime:yyyy-MM-dd}.jsonl");
    Assert(!File.Exists(expiredFile),
        "Le fichier quotidien expiré doit être supprimé au chargement.");
}
finally
{
    if (Directory.Exists(historyDirectory))
    {
        Directory.Delete(historyDirectory, recursive: true);
    }
}

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
