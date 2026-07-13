namespace ArmZavuch.Models;

/// <summary>Уровень подсказки при перетаскивании карточки в ячейку конструктора.</summary>
public enum DragHintLevel
{
    None = 0,
    Recommended = 1,
    Caution = 2,
    Blocked = 3
}

/// <summary>Результат оценки ячейки для текущего drag.</summary>
public sealed record DragHintResult(DragHintLevel Level, string Message)
{
    public static DragHintResult None { get; } = new(DragHintLevel.None, "");

    public static DragHintResult Merge(IEnumerable<DragHintResult> hints)
    {
        var best = hints.Where(h => h.Level != DragHintLevel.None)
            .OrderByDescending(h => h.Level)
            .FirstOrDefault();
        return best ?? None;
    }
}
