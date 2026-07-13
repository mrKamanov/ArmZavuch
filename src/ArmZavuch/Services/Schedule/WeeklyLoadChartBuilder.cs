using System.Globalization;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Сумма баллов Сивкова по дням недели для класса — подсказка «буква М».</summary>
public static class WeeklyLoadChartBuilder
{
    public const double CompactWidth = 260;
    public const double CompactHeight = 56;
    public const double LargeWidth = 620;
    public const double LargeHeight = 140;

    private const double MarginX = 14;
    public const double BottomMargin = 8;

    public static double GetTopLabelReserve(double plotHeight) =>
        plotHeight > CompactHeight ? 28 : 20;

    public static double GetValueLabelOffset(double plotHeight) =>
        plotHeight > CompactHeight ? 22 : 16;

    public static WeeklyLoadChartResult Build(
        IReadOnlyList<LessonSlot> weekSlots,
        IReadOnlyDictionary<int, double> subjectDifficulty,
        int classId,
        string className,
        double plotWidth = CompactWidth,
        double plotHeight = CompactHeight)
    {
        var slots = weekSlots.Where(s => s.ClassId == classId).ToList();
        var points = new List<WeeklyLoadChartPoint>();
        var max = 0.0;
        var weekTotal = 0.0;

        for (var day = 1; day <= 6; day++)
        {
            var total = slots
                .Where(s => s.DayOfWeek == day && s.SubjectId > 0)
                .GroupBy(s => (s.LessonNumber, s.SubjectId))
                .Sum(g => subjectDifficulty.TryGetValue(g.Key.SubjectId, out var score) ? score : 1.0);

            weekTotal += total;
            max = Math.Max(max, total);
            points.Add(new WeeklyLoadChartPoint
            {
                DayOfWeek = day,
                DayName = DayNames[day - 1],
                TotalScore = total,
                IsPeakDay = day is 2 or 4
            });
        }

        if (max <= 0)
            max = 1;

        var topReserve = GetTopLabelReserve(plotHeight);
        var labelOffset = GetValueLabelOffset(plotHeight);
        var labelBelowGap = plotHeight > CompactHeight ? 10 : 8;
        var plotW = plotWidth - MarginX * 2;
        var plotH = plotHeight - topReserve - BottomMargin;

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var x = MarginX + (points.Count == 1 ? plotW / 2 : i * plotW / (points.Count - 1));
            var ratio = point.TotalScore / max;
            var y = topReserve + plotH * (1 - ratio);
            point.ChartX = x;
            point.ChartY = y;
            point.ValueLabelX = x;

            var aboveY = y - labelOffset;
            if (aboveY >= 2)
            {
                point.ValueLabelY = aboveY;
                point.IsValueLabelBelow = false;
            }
            else
            {
                point.ValueLabelY = y + labelBelowGap;
                point.IsValueLabelBelow = true;
            }
        }

        var polyline = string.Join(" ",
            points.Select(p => $"{p.ChartX.ToString(CultureInfo.InvariantCulture)},{p.ChartY.ToString(CultureInfo.InvariantCulture)}"));

        return new WeeklyLoadChartResult
        {
            ClassName = className,
            MaxScore = max,
            WeekTotal = weekTotal,
            PlotWidth = plotWidth,
            PlotHeight = plotHeight,
            Points = points,
            PolylinePoints = polyline
        };
    }

    private static readonly string[] DayNames = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб"];
}

public sealed class WeeklyLoadChartResult
{
    public string ClassName { get; init; } = "";
    public double MaxScore { get; init; }
    public double WeekTotal { get; init; }
    public double PlotWidth { get; init; }
    public double PlotHeight { get; init; }
    public IReadOnlyList<WeeklyLoadChartPoint> Points { get; init; } = [];
    public string PolylinePoints { get; init; } = "";
}
