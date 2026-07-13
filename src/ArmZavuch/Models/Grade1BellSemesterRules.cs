namespace ArmZavuch.Models;

/// <summary>
/// Выбор шаблона звонков 1 класса: I или II полугодие по дате.
/// Вход: дата. Выход: нужен ли шаблон II полугодия.
/// </summary>
public static class Grade1BellSemesterRules
{
    public static bool UseSecondHalfTemplate(DateOnly date) =>
        date.Month is >= 1 and <= 6;

    /// <summary>Дата для выбора шаблона 1 класса в недельном конструкторе (вне контекста конкретного дня).</summary>
    public static DateOnly ReferenceDateForGrid(DateOnly? context = null)
    {
        var date = context ?? DateOnly.FromDateTime(DateTime.Today);
        return UseSecondHalfTemplate(date)
            ? new DateOnly(date.Year, 4, 1)
            : new DateOnly(date.Year, 10, 1);
    }
}
