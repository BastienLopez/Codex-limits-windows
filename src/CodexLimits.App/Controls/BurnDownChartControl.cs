using System.Globalization;
using CodexLimits.Core;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FlowDirection = System.Windows.FlowDirection;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CodexLimits.App.Controls;

public sealed class BurnDownChartControl : System.Windows.FrameworkElement
{
    private static readonly Pen GridPen = CreatePen(Color.FromRgb(43, 49, 57), 1, new System.Windows.Media.DoubleCollection { 2, 3 });
    private static readonly Pen TargetPen = CreatePen(Color.FromRgb(52, 199, 89), 1.7, new System.Windows.Media.DoubleCollection { 4, 3 });
    private static readonly Pen ActualPen = CreatePen(Color.FromRgb(10, 132, 255), 2.4);
    private static readonly Pen HistoricalPen = CreatePen(Color.FromRgb(145, 145, 150), 1.6, new System.Windows.Media.DoubleCollection { 2, 3 });
    private static readonly Pen NowPen = CreatePen(Color.FromRgb(108, 116, 128), 1, new System.Windows.Media.DoubleCollection { 2, 3 });
    private static readonly Brush AxisBrush = new System.Windows.Media.SolidColorBrush(Color.FromRgb(151, 160, 172));
    private static readonly Brush InactiveBrush = new System.Windows.Media.SolidColorBrush(Color.FromArgb(65, 83, 91, 103));

    public ChartState? Data { get; set; }
    public string UiLanguage { get; set; } = "fr";

    protected override void OnRender(System.Windows.Media.DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRoundedRectangle(
            new System.Windows.Media.SolidColorBrush(Color.FromRgb(12, 15, 19)),
            null,
            new System.Windows.Rect(RenderSize),
            10,
            10);

        if (Data is null || ActualWidth < 120 || ActualHeight < 100)
        {
            DrawText(drawingContext, UiText.Get(UiLanguage, "Aucune donnée", "No data"), 12, new Point(14, 14), AxisBrush);
            return;
        }

        const double left = 44;
        const double top = 14;
        const double right = 10;
        const double bottom = 32;
        var plot = new System.Windows.Rect(
            left,
            top,
            Math.Max(ActualWidth - left - right, 1),
            Math.Max(ActualHeight - top - bottom, 1));

        foreach (var inactive in Data.InactivePeriods)
        {
            var inactiveLeft = MapX(inactive.Start, Data.Window, plot);
            var inactiveRight = MapX(inactive.End, Data.Window, plot);
            if (inactiveRight > inactiveLeft)
            {
                drawingContext.DrawRectangle(
                    InactiveBrush,
                    null,
                    new System.Windows.Rect(inactiveLeft, plot.Top, inactiveRight - inactiveLeft, plot.Height));
            }
        }

        for (var percent = 0; percent <= 100; percent += 25)
        {
            var y = MapY(percent, plot);
            drawingContext.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(drawingContext, $"{percent}%", 10, new Point(3, y - 7), AxisBrush);
        }

        var labels = BuildTimeLabels(Data.Window);
        foreach (var (time, label) in labels)
        {
            var x = MapX(time, Data.Window, plot);
            drawingContext.DrawLine(GridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var text = CreateText(label, 10, AxisBrush);
            drawingContext.DrawText(
                text,
                new Point(Math.Clamp(x - text.Width / 2, plot.Left, plot.Right - text.Width), plot.Bottom + 8));
        }

        DrawSeries(drawingContext, Data.Target, Data.Window, plot, TargetPen, step: false);
        DrawSeries(drawingContext, Data.Actual, Data.Window, plot, ActualPen, step: true);

        var currentColor = Data.CurrentProjection.LastOrDefault()?.RemainingPercent <= 0
            ? Color.FromRgb(255, 103, 95)
            : Color.FromRgb(103, 166, 255);
        var currentPen = CreatePen(currentColor, 2.5, new System.Windows.Media.DoubleCollection { 7, 3 });
        DrawSeries(drawingContext, Data.CurrentProjection, Data.Window, plot, currentPen, step: false);
        DrawSeries(drawingContext, Data.HistoricalProjection, Data.Window, plot, HistoricalPen, step: false);

        var nowX = MapX(Data.Now, Data.Window, plot);
        drawingContext.DrawLine(NowPen, new Point(nowX, plot.Top), new Point(nowX, plot.Bottom));
        DrawPoint(
            drawingContext,
            new ChartPoint(Data.Now, Data.Actual.LastOrDefault()?.RemainingPercent ?? 0),
            Data.Window,
            plot,
            new System.Windows.Media.SolidColorBrush(currentColor));
    }

    private IReadOnlyList<(DateTimeOffset Time, string Label)> BuildTimeLabels(UsageWindow window)
    {
        var result = new List<(DateTimeOffset, string)>();
        var duration = window.ResetsAt - window.StartsAt;
        var count = duration.TotalDays >= 2 ? 7 : 5;
        var culture = UiText.Culture(UiLanguage);

        for (var index = 0; index <= count; index++)
        {
            var fraction = (double)index / count;
            var time = window.StartsAt + TimeSpan.FromTicks((long)(duration.Ticks * fraction));
            var label = duration.TotalDays >= 2
                ? time.ToLocalTime().ToString("ddd", culture)
                : time.ToLocalTime().ToString("HH:mm", culture);
            result.Add((time, label));
        }
        return result;
    }

    private static void DrawSeries(
        System.Windows.Media.DrawingContext context,
        IReadOnlyList<ChartPoint> points,
        UsageWindow window,
        System.Windows.Rect plot,
        Pen pen,
        bool step)
    {
        if (points.Count < 2) return;
        var geometry = new System.Windows.Media.StreamGeometry();
        using (var writer = geometry.Open())
        {
            var first = points[0];
            writer.BeginFigure(new Point(MapX(first.Time, window, plot), MapY(first.RemainingPercent, plot)), false, false);
            for (var index = 1; index < points.Count; index++)
            {
                var previous = points[index - 1];
                var current = points[index];
                if (step)
                {
                    writer.LineTo(new Point(MapX(current.Time, window, plot), MapY(previous.RemainingPercent, plot)), true, false);
                }
                writer.LineTo(new Point(MapX(current.Time, window, plot), MapY(current.RemainingPercent, plot)), true, false);
            }
        }
        geometry.Freeze();
        context.DrawGeometry(null, pen, geometry);
    }

    private static void DrawPoint(
        System.Windows.Media.DrawingContext context,
        ChartPoint point,
        UsageWindow window,
        System.Windows.Rect plot,
        Brush brush)
    {
        context.DrawEllipse(
            brush,
            new Pen(Brushes.White, 1),
            new Point(MapX(point.Time, window, plot), MapY(point.RemainingPercent, plot)),
            4.5,
            4.5);
    }

    private static double MapX(DateTimeOffset time, UsageWindow window, System.Windows.Rect plot)
    {
        var duration = Math.Max((window.ResetsAt - window.StartsAt).TotalSeconds, 1);
        var fraction = Math.Clamp((time - window.StartsAt).TotalSeconds / duration, 0, 1);
        return plot.Left + fraction * plot.Width;
    }

    private static double MapY(double percent, System.Windows.Rect plot) =>
        plot.Bottom - Math.Clamp(percent, 0, 100) / 100d * plot.Height;

    private static Pen CreatePen(Color color, double thickness, System.Windows.Media.DoubleCollection? dash = null)
    {
        var pen = new Pen(new System.Windows.Media.SolidColorBrush(color), thickness);
        if (dash is not null) pen.DashStyle = new System.Windows.Media.DashStyle(dash, 0);
        pen.Freeze();
        return pen;
    }

    private void DrawText(
        System.Windows.Media.DrawingContext context,
        string text,
        double size,
        Point point,
        Brush brush) =>
        context.DrawText(CreateText(text, size, brush), point);

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

