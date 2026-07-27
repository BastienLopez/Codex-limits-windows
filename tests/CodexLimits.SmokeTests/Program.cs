using CodexLimits.Core;

var settings = new AppSettings();
var localMonday = LocalOffset(new DateTime(2026, 7, 27, 9, 0, 0, DateTimeKind.Unspecified));
var localFriday = LocalOffset(new DateTime(2026, 7, 31, 18, 0, 0, DateTimeKind.Unspecified));
var activeHours = ScheduleMath.GetActiveHours(localMonday, localFriday, settings);
Assert(Math.Abs(activeHours - 45) < 0.01, "Le planning lundi-vendredi 09:00-18:00 doit contenir 45 heures actives.");
Assert(Math.Abs(ScheduleMath.GetActiveWorkDays(localMonday, localFriday, settings) - 5) < 0.01, "Le planning doit contenir cinq jours travaillés.");
Assert(ScheduleMath.IsActive(localMonday.AddHours(1), settings), "Le lundi matin doit être actif.");
Assert(!ScheduleMath.IsActive(localMonday.AddHours(11), settings), "Le lundi soir doit être inactif.");
Assert(ScheduleMath.IsActive(localMonday.AddDays(1).AddHours(1), settings), "Le mardi matin doit être actif.");
Assert(!ScheduleMath.IsActive(localFriday.AddHours(2), settings), "Le vendredi soir doit être hors créneau.");
Assert(!ScheduleMath.IsActive(localFriday.AddDays(1), settings), "Le samedi doit être hors créneau.");

var now = localMonday.AddDays(2).AddHours(4);
var window = new UsageWindow(63, localMonday.AddDays(7), 7 * 24 * 60);
var samples = new[]
{
    new UsageSample(localMonday, 100, window.ResetsAt),
    new UsageSample(localMonday.AddDays(1).AddHours(4), 82, window.ResetsAt),
    new UsageSample(now, 63, window.ResetsAt)
};

var forecast = ForecastEngine.Evaluate(window, samples, Array.Empty<TokenDay>(), 3, now, null, settings);
Assert(forecast.CurrentPercentPerDay > 0, "Le rythme courant doit être positif.");
Assert(forecast.RecommendedPercentPerDay > 0, "Le rythme conseillé doit être positif.");
Assert(forecast.ExpectedRemainingAtReset >= 0, "La projection ne doit pas être négative.");
Assert(forecast.ActiveHoursLeft > 0, "Il doit rester des heures actives.");

var chart = ChartSeriesBuilder.Build(
    new UsageSnapshot(new LimitReading("codex", "Codex", window), Array.Empty<LimitReading>(), Array.Empty<TokenDay>(), now),
    samples,
    forecast,
    3,
    settings);
Assert(chart.Target.Count >= 10, "La cible doit contenir les pentes et paliers des jours travaillés.");
Assert(chart.Actual.Count >= 2, "La courbe réelle doit contenir plusieurs points.");
Assert(chart.CurrentProjection.Count >= 2, "La projection courante doit contenir au moins deux points.");
Assert(chart.InactivePeriods.Count >= 5, "Les nuits et le week-end doivent apparaître comme périodes inactives.");

var targetPoints = chart.Target.OrderBy(point => point.Time).ToArray();
var targetIntervals = ScheduleMath.GetIntervals(window.StartsAt, window.ResetsAt, settings);
Assert(targetIntervals.Count == 5, "La semaine doit produire exactement cinq créneaux actifs.");
foreach (var interval in targetIntervals)
{
    var startPoint = targetPoints.Last(point => point.Time <= interval.Start);
    var endPoint = targetPoints.First(point => point.Time >= interval.End);
    Assert(endPoint.RemainingPercent < startPoint.RemainingPercent,
        "La cible doit descendre pendant chaque journée travaillée.");
}

for (var index = 0; index < targetIntervals.Count - 1; index++)
{
    var dayEnd = targetPoints.First(point => point.Time == targetIntervals[index].End);
    var nextDayStart = targetPoints.First(point => point.Time == targetIntervals[index + 1].Start);
    Assert(Math.Abs(dayEnd.RemainingPercent - nextDayStart.RemainingPercent) < 0.001,
        "La cible doit rester horizontale pendant la nuit.");
}

var fridayEnd = targetPoints.First(point => point.Time == targetIntervals[^1].End);
var resetPoint = targetPoints[^1];
Assert(Math.Abs(fridayEnd.RemainingPercent - resetPoint.RemainingPercent) < 0.001,
    "La cible doit rester horizontale pendant tout le week-end.");

var projectionPoints = chart.CurrentProjection.OrderBy(point => point.Time).ToArray();
var hasProjectionPlateau = projectionPoints
    .Zip(projectionPoints.Skip(1), (left, right) => new { Left = left, Right = right })
    .Any(pair => pair.Right.Time > pair.Left.Time &&
                 Math.Abs(pair.Right.RemainingPercent - pair.Left.RemainingPercent) < 0.001);
Assert(hasProjectionPlateau, "La projection courante doit contenir au moins un palier hors horaires actifs.");

Console.WriteLine("Smoke tests OK");
return;

static DateTimeOffset LocalOffset(DateTime value) =>
    new(value, TimeZoneInfo.Local.GetUtcOffset(value));

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
