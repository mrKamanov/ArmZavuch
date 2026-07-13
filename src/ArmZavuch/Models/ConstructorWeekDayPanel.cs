namespace ArmZavuch.Models;

/// <summary>Блок «все классы · неделя»: одна колонка дней с транспонированной сеткой.</summary>
public sealed class ConstructorWeekDayPanel
{
    public int DayOfWeek { get; init; }
    public string DayTitle { get; init; } = "";
    public IList<ConstructorDayGridSection> Sections { get; init; } = [];
}
