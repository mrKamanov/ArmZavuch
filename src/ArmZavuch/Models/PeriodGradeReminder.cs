namespace ArmZavuch.Models;

/// <summary>
/// Напоминание о выставлении оценок перед концом учебного периода (ТЗ §3, диспетчерская).
/// Вход: период и дата. Выход: подписи для компактного чипа.
/// </summary>
public sealed class PeriodGradeReminder
{
    public string PeriodName { get; init; } = "";
    public int SchoolDaysUntilEnd { get; init; }

    public string ChipLabel => "Оценки";

    public string ChipValue => SchoolDaysUntilEnd switch
    {
        1 => "завтра",
        2 => "через 2 дня",
        _ => $"через {SchoolDaysUntilEnd} дн."
    };

    public string ChipHint => string.IsNullOrWhiteSpace(PeriodName)
        ? "выставьте за 2 учебных дня"
        : $"выставьте · {PeriodName.Trim()}";

    public string AccentBackground => "#FFF1F2";
    public string AccentForeground => "#BE123C";
}
