namespace ArmZavuch.Models;

/// <summary>Карточка недельного графика баллов Сивкова для одного класса (вкладка Конструктора).</summary>
public sealed class ClassWeeklyLoadChartCard
{
    public int ClassId { get; init; }
    public string ClassName { get; init; } = "";
    public int ClassGrade { get; init; }
    public double WeekTotal { get; init; }
    public double MaxDayScore { get; init; }
    public bool HasDailyOverload { get; init; }
    public string PolylinePoints { get; init; } = "";
    public IReadOnlyList<WeeklyLoadChartPoint> Points { get; init; } = [];

    public string SummaryLine => WeekTotal > 0
        ? $"за неделю {WeekTotal:0.##} · макс. за день {MaxDayScore:0.##}"
        : "уроков в шаблоне пока нет";
}
