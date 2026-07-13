namespace ArmZavuch.Models;

/// <summary>Заголовок столбца/строки урока в сетке Конструктора.</summary>
public sealed class LessonNumberHeader
{
    public int LessonNumber { get; init; }
    public string Title => $"Урок {LessonNumber}";
    public string BellTimeDisplay { get; init; } = "";
    public bool HasBellTime => !string.IsNullOrWhiteSpace(BellTimeDisplay);
}
