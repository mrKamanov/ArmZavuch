using ArmZavuch.Data;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Графики Сивкова для всех классов текущего шаблона недели.</summary>
public static class AllClassWeeklyLoadChartsBuilder
{
    public const double CardPlotWidth = 480;
    public const double CardPlotHeight = 120;

    public static IReadOnlyList<ClassWeeklyLoadChartCard> Build(
        IEnumerable<SchoolClass> classes,
        IReadOnlyList<LessonSlot> weekSlots,
        Func<int, int, IReadOnlyDictionary<int, double>> difficultyForClass)
    {
        var cards = new List<ClassWeeklyLoadChartCard>();
        foreach (var cls in classes.OrderBy(c => c.Grade).ThenBy(c => c.Letter, StringComparer.OrdinalIgnoreCase))
        {
            var difficulty = difficultyForClass(cls.Id, cls.Grade);
            var chart = WeeklyLoadChartBuilder.Build(
                weekSlots,
                difficulty,
                cls.Id,
                cls.DisplayName,
                CardPlotWidth,
                CardPlotHeight);

            var dailyLimit = SanPiNRules.MaxDailyDifficultySum(cls.Grade);
            var maxDayScore = chart.Points.Count > 0 ? chart.Points.Max(p => p.TotalScore) : 0;
            cards.Add(new ClassWeeklyLoadChartCard
            {
                ClassId = cls.Id,
                ClassName = cls.DisplayName,
                ClassGrade = cls.Grade,
                WeekTotal = chart.WeekTotal,
                MaxDayScore = maxDayScore,
                HasDailyOverload = maxDayScore > dailyLimit + 0.01,
                PolylinePoints = chart.PolylinePoints,
                Points = chart.Points
            });
        }

        return cards;
    }
}
