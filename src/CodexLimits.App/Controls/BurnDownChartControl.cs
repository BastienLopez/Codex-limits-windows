using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FlowDirection = System.Windows.FlowDirection;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using CodexLimits.Core;

namespace CodexLimits.App.Controls;

public sealed class BurnDownChartControl : FrameworkElement
{
    private static readonly Pen GridPen = CreatePen(Color.FromRgb(40, 42, 45), 1, new DoubleCollection { 2, 3 });
    private static readonly Pen TargetPen = CreatePen(Color.FromRgb(52, 199, 89), 1.5, new DoubleCollection { 3, 3 });
    private static readonly Pen ActualPen = CreatePen(Color.FromRgb(10, 132, 255), 2.2);
    private static readonly Pen HistoricalPen = CreatePen(Color.FromRgb(145, 145, 150), 1.5, new DoubleCollection { 2, 3 });
    private static readonly Pen NowPen = CreatePen(Color.FromRgb(100, 100, 105), 1, new DoubleCollection { 2, 3 });
    private static readonly Brush AxisBrush = new SolidColorBrush(Color.FromRgb(140, 142, 147));

    public ChartState? Data { get; set; }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(14, 15, 17)), null, new Rect(RenderSize));

        if (Data is null || ActualWidth < 120 || ActualHeight < 100)
        {
            DrawText(drawingContext, "Aucune donnée", 12, new Point(12, 12), AxisBrush);
            return;
        }

        const double left = 42;
        const double top = 10;
        const double right = 8;
        const double bottom = 28;
        var plot = new Rect(left, top, Math.Max(ActualWidth - left - right, 1), Math.Max(ActualHeight - top - bottom, 1));

        for (var percent = 0; percent <= 100; percent += 25)
        {
            var y = MapY(percent, plot);
            drawingContext.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(drawingContext, $"{percent}%", 10, new Point(2, y - 7), AxisBrush);
        }

        var labels = BuildTimeLabels(Data.Window);
        foreach (var (time, label) in labels)
        {
            var x = MapX(time, Data.Window, plot);
            drawingContext.DrawLine(GridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var text = CreateText(label, 10, AxisBrush);
            drawingContext.DrawText(text, new Point(Math.Clamp(x - text.Width / 2, plot.Left, plot.Right - text.Width), plot.Bottom + 6));
        }

        DrawSeries(drawingContext, Data.Target, Data.Window, plot, TargetPen, step: false);
        DrawSeries(drawingContext, Data.Actual, Data.Window, plot, ActualPen, step: true);

        var currentColor = Data.CurrentProjection.LastOrDefault()?.RemainingPercent <= 0
            ? Color.FromRgb(255, 69, 58)
            : Color.FromRgb(10, 132, 255);
        var currentPen = CreatePen(currentColor, 2.4, new DoubleCollection { 7, 3 });
        DrawSeries(drawingContext, Data.CurrentProjection, Data.Window, plot, currentPen, step: false);
        DrawSeries(drawingContext, Data.HistoricalProjection, Data.Window, plot, HistoricalPen, step: false);

        var nowX = MapX(Data.Now, Data.Window, plot);
        drawingContext.DrawLine(NowPen, new Point(nowX, plot.Top), new Point(nowX, plot.Bottom));
        DrawPoint(drawingContext, new ChartPoint(Data.Now, Data.Actual.LastOrDefault()?.RemainingPercent ?? 0), Data.Window, plot, new SolidColorBrush(currentColor));
    }

    private static IReadOnlyList<(DateTimeOffset Time, string Label)> BuildTimeLabels(UsageWindow window)
    {
        var result = new List<(DateTimeOffset, string)>();
        var duration = window.ResetsAt - window.StartsAt;
        var count = duration.TotalDays >= 2 ? 7 : 5;
        for (var index = 0; index <= count; index++)
        {
            var fraction = (double)index / count;
            var time = window.StartsAt + TimeSpan.FromTicks((long)(duration.Ticks * fraction));
            var label = duration.TotalDays >= 2
                ? time.ToLocalTime().ToString("ddd", CultureInfo.GetCultureInfo("fr-FR"))
                : time.ToLocalTime().ToString("HH:mm", CultureInfo.GetCultureInfo("fr-FR"));
            result.Add((time, label));
        }
        return result;
    }

    private static void DrawSeries(
        DrawingContext context,
        IReadOnlyList<ChartPoint> points,
        UsageWindow window,
        Rect plot,
        Pen pen,
        bool step)
    {
        if (points.Count < 2) return;
        var geometry = new StreamGeometry();
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

    private static void DrawPoint(DrawingContext context, ChartPoint point, UsageWindow window, Rect plot, Brush brush)
    {
        context.DrawEllipse(
            brush,
            new Pen(Brushes.White, 1),
            new Point(MapX(point.Time, window, plot), MapY(point.RemainingPercent, plot)),
            4,
            4);
    }

    private static double MapX(DateTimeOffset time, UsageWindow window, Rect plot)
    {
        var duration = Math.Max((window.ResetsAt - window.StartsAt).TotalSeconds, 1);
        var fraction = Math.Clamp((time - window.StartsAt).TotalSeconds / duration, 0, 1);
        return plot.Left + fraction * plot.Width;
    }

    private static double MapY(double percent, Rect plot) =>
        plot.Bottom - Math.Clamp(percent, 0, 100) / 100d * plot.Height;

    private static Pen CreatePen(Color color, double thickness, DoubleCollection? dash = null)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness);
        if (dash is not null) pen.DashStyle = new DashStyle(dash, 0);
        pen.Freeze();
        return pen;
    }

    private static void DrawText(DrawingContext context, string text, double size, Point point, Brush brush) =>
        context.DrawText(CreateText(text, size, brush), point);

    private static FormattedText CreateText(string text, double size, Brush brush) =>
        new(
            text,
            CultureInfo.GetCultureInfo("fr-FR"),
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            size,
            brush,
            1.0);
}
