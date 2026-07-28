using System.Globalization;
using CodexLimits.Core;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FlowDirection = System.Windows.FlowDirection;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace CodexLimits.App.Controls;

public sealed class BurnDownChartControl : System.Windows.FrameworkElement
{
    private static readonly Pen GridPen = CreatePen(Color.FromRgb(43, 49, 57), 1, new System.Windows.Media.DoubleCollection { 2, 3 });
    private static readonly Pen DayBoundaryPen = CreatePen(Color.FromRgb(69, 78, 91), 1);
    private static readonly Pen TargetPen = CreatePen(Color.FromRgb(52, 199, 89), 1.8, new System.Windows.Media.DoubleCollection { 5, 3 });
    private static readonly Pen ActualPen = CreatePen(Color.FromRgb(10, 132, 255), 2.4);
    private static readonly Pen HistoricalPen = CreatePen(Color.FromRgb(145, 145, 150), 1.4, new System.Windows.Media.DoubleCollection { 2, 3 });
    private static readonly Pen ProjectionPen = CreatePen(Color.FromRgb(255, 103, 95), 2.2, new System.Windows.Media.DoubleCollection { 6, 3 });
    private static readonly Pen NowPen = CreatePen(Color.FromRgb(214, 220, 230), 1, new System.Windows.Media.DoubleCollection { 2, 3 });
    private static readonly Pen WorkTickPen = CreatePen(Color.FromRgb(150, 158, 170), 1.1);
    private static readonly Brush AxisBrush = new System.Windows.Media.SolidColorBrush(Color.FromRgb(151, 160, 172));
    private static readonly Brush CurrentDayBrush = Brushes.White;

    public ChartState? Data { get; set; }
    public AppSettings Settings { get; set; } = new();
    public string UiLanguage { get; set; } = "fr";

    protected override void OnRender(System.Windows.Media.DrawingContext dc)
    {
        base.OnRender(dc);

        dc.DrawRoundedRectangle(
            new System.Windows.Media.SolidColorBrush(Color.FromRgb(12, 15, 19)),
            null,
            new Rect(RenderSize),
            10,
            10);

        if (Data is null)
        {
            DrawText(dc, UiText.Get(UiLanguage, "Aucune donnée", "No data"), 12, new Point(14, 14), AxisBrush);
            return;
        }

        if (ActualWidth < 120 || ActualHeight < 80)
        {
            return;
        }

        var settings = Settings.Normalize();
        var intervals = ScheduleMath.GetCurrentCycleIntervals(Data.Now, settings);
        if (intervals.Count == 0)
        {
            return;
        }

        const double left = 54;
        const double top = 20;
        const double right = 12;
        const double bottom = 34;
        var plot = new Rect(left, top, Math.Max(ActualWidth - left - right, 1), Math.Max(ActualHeight - top - bottom, 1));

        DrawHorizontalGrid(dc, plot);
        DrawDayColumns(dc, plot, intervals, settings, Data.Now);

        var graphStart = intervals[0].Start;
        var graphEnd = intervals[^1].End;
        var actual = ClipActual(Data.Actual, graphStart, Data.Now);
        var projection = ClipFuture(Data.CurrentProjection, Data.Now, graphEnd);
        var historical = ClipFuture(Data.HistoricalProjection, Data.Now, graphEnd);

        DrawSeries(dc, Data.Target, intervals, plot, TargetPen, step: true);
        DrawSeries(dc, actual, intervals, plot, ActualPen, step: true);
        DrawSeries(dc, projection, intervals, plot, ProjectionPen, step: true);
        DrawSeries(dc, historical, intervals, plot, HistoricalPen, step: true);

        var nowTime = ClampToGraph(Data.Now, graphStart, graphEnd);
        var nowX = MapX(nowTime, intervals, plot);
        dc.DrawLine(NowPen, new Point(nowX, plot.Top), new Point(nowX, plot.Bottom));

        var nowLabel = CreateText(UiText.Get(UiLanguage, "maintenant", "now"), 10, CurrentDayBrush);
        dc.DrawText(nowLabel, new Point(Math.Clamp(nowX - nowLabel.Width / 2, plot.Left, plot.Right - nowLabel.Width), plot.Top - 16));

        var currentRemaining = Data.Actual.LastOrDefault()?.RemainingPercent ?? 0;
        DrawPoint(dc, new ChartPoint(nowTime, currentRemaining), intervals, plot, new System.Windows.Media.SolidColorBrush(Color.FromRgb(255, 103, 95)));
    }

    private void DrawHorizontalGrid(System.Windows.Media.DrawingContext dc, Rect plot)
    {
        for (var percent = 0; percent <= 100; percent += 25)
        {
            var y = MapY(percent, plot);
            dc.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(dc, $"{percent}%", 10, new Point(4, y - 7), AxisBrush);
        }
    }

    private void DrawDayColumns(
        System.Windows.Media.DrawingContext dc,
        Rect plot,
        IReadOnlyList<TimeRange> intervals,
        AppSettings settings,
        DateTimeOffset now)
    {
        var startTextValue = settings.StartTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        var endTextValue = settings.EndTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        var currentDate = now.ToLocalTime().Date;
        var columnWidth = plot.Width / intervals.Count;

        for (var i = 0; i < intervals.Count; i++)
        {
            var startX = plot.Left + i * columnWidth;
            var endX = startX + columnWidth;
            var interval = intervals[i];

            dc.DrawLine(DayBoundaryPen, new Point(startX, plot.Top), new Point(startX, plot.Bottom));
            if (i == intervals.Count - 1)
            {
                dc.DrawLine(DayBoundaryPen, new Point(endX, plot.Top), new Point(endX, plot.Bottom));
            }

            DrawBoundaryTick(dc, startX, plot);
            DrawBoundaryTick(dc, endX, plot);

            var startText = CreateText(startTextValue, 8.4, AxisBrush);
            var endText = CreateText(endTextValue, 8.4, AxisBrush);
            dc.DrawText(startText, new Point(startX + 3, plot.Top + 4));
            dc.DrawText(endText, new Point(endX - endText.Width - 3, plot.Top + 4));

            var labelBrush = interval.Start.ToLocalTime().Date == currentDate ? CurrentDayBrush : AxisBrush;
            var label = CreateText(FormatDayLabel(interval.Start.ToLocalTime().Date), 10, labelBrush);
            dc.DrawText(label, new Point(startX + (columnWidth - label.Width) / 2, plot.Bottom + 8));
        }
    }

    private static IReadOnlyList<ChartPoint> ClipActual(
        IReadOnlyList<ChartPoint> source,
        DateTimeOffset graphStart,
        DateTimeOffset now)
    {
        var ordered = source.OrderBy(point => point.Time).ToArray();
        var result = new List<ChartPoint>();
        var beforeStart = ordered.LastOrDefault(point => point.Time <= graphStart);
        if (beforeStart is not null)
        {
            result.Add(new ChartPoint(graphStart, beforeStart.RemainingPercent));
        }

        result.AddRange(ordered.Where(point => point.Time > graphStart && point.Time <= now));
        return result
            .GroupBy(point => point.Time)
            .Select(group => group.Last())
            .OrderBy(point => point.Time)
            .ToArray();
    }

    private static IReadOnlyList<ChartPoint> ClipFuture(
        IReadOnlyList<ChartPoint> source,
        DateTimeOffset now,
        DateTimeOffset graphEnd)
    {
        return source
            .Where(point => point.Time >= now && point.Time <= graphEnd)
            .GroupBy(point => point.Time)
            .Select(group => group.Last())
            .OrderBy(point => point.Time)
            .ToArray();
    }

    private string FormatDayLabel(DateTime day)
    {
        var abbreviation = UiText.ShortDay(UiLanguage, day.DayOfWeek).ToLowerInvariant();
        if (!UiText.IsEnglish(UiLanguage))
        {
            abbreviation += ".";
        }

        return $"{abbreviation} {day.Day}";
    }

    private static void DrawSeries(
        System.Windows.Media.DrawingContext dc,
        IReadOnlyList<ChartPoint> points,
        IReadOnlyList<TimeRange> intervals,
        Rect plot,
        Pen pen,
        bool step)
    {
        if (points.Count < 2)
        {
            return;
        }

        var geometry = new System.Windows.Media.StreamGeometry();
        using var writer = geometry.Open();
        var first = points[0];
        writer.BeginFigure(new Point(MapX(first.Time, intervals, plot), MapY(first.RemainingPercent, plot)), false, false);

        for (var i = 1; i < points.Count; i++)
        {
            var previous = points[i - 1];
            var current = points[i];
            var currentX = MapX(current.Time, intervals, plot);

            if (step)
            {
                writer.LineTo(new Point(currentX, MapY(previous.RemainingPercent, plot)), true, false);
            }

            writer.LineTo(new Point(currentX, MapY(current.RemainingPercent, plot)), true, false);
        }

        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private static void DrawPoint(
        System.Windows.Media.DrawingContext dc,
        ChartPoint point,
        IReadOnlyList<TimeRange> intervals,
        Rect plot,
        Brush brush)
    {
        dc.DrawEllipse(
            brush,
            new Pen(Brushes.White, 1),
            new Point(MapX(point.Time, intervals, plot), MapY(point.RemainingPercent, plot)),
            4.8,
            4.8);
    }

    private static void DrawBoundaryTick(System.Windows.Media.DrawingContext dc, double x, Rect plot)
    {
        dc.DrawLine(WorkTickPen, new Point(x, plot.Top), new Point(x, plot.Top + 9));
        dc.DrawLine(WorkTickPen, new Point(x, plot.Bottom - 9), new Point(x, plot.Bottom));
    }

    private static double MapX(DateTimeOffset time, IReadOnlyList<TimeRange> intervals, Rect plot)
    {
        if (intervals.Count == 0)
        {
            return plot.Left;
        }

        for (var i = 0; i < intervals.Count; i++)
        {
            var interval = intervals[i];
            var startX = plot.Left + plot.Width * i / intervals.Count;
            var endX = plot.Left + plot.Width * (i + 1) / intervals.Count;

            if (time <= interval.Start)
            {
                return startX;
            }

            if (time < interval.End)
            {
                var fraction = (time - interval.Start).TotalSeconds /
                               Math.Max((interval.End - interval.Start).TotalSeconds, 1);
                return startX + fraction * (endX - startX);
            }
        }

        return plot.Right;
    }

    private static double MapY(double percent, Rect plot) =>
        plot.Bottom - Math.Clamp(percent, 0, 100) / 100d * plot.Height;

    private static DateTimeOffset ClampToGraph(DateTimeOffset time, DateTimeOffset start, DateTimeOffset end) =>
        time < start ? start : time > end ? end : time;

    private static Pen CreatePen(Color color, double thickness, System.Windows.Media.DoubleCollection? dash = null)
    {
        var pen = new Pen(new System.Windows.Media.SolidColorBrush(color), thickness);
        if (dash is not null)
        {
            pen.DashStyle = new System.Windows.Media.DashStyle(dash, 0);
        }

        pen.Freeze();
        return pen;
    }

    private void DrawText(System.Windows.Media.DrawingContext dc, string text, double size, Point point, Brush brush)
    {
        dc.DrawText(CreateText(text, size, brush), point);
    }

    private System.Windows.Media.FormattedText CreateText(string text, double size, Brush brush) =>
        new(
            text,
            UiText.Culture(UiLanguage),
            FlowDirection.LeftToRight,
            new System.Windows.Media.Typeface("Segoe UI"),
            size,
            brush,
            1.0);
}
