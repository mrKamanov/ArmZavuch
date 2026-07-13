namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Правила заполнения ячейки по типу предмета: дин. пауза, РоВ и т.п.
/// Вход: название предмета. Выход: флаги для сетки и валидации.
/// </summary>
public static class SubjectScheduleRules
{
    public const string ImportantTalksCanonicalName = "Разговоры о важном";
    public const int ImportantTalksPreferredDayOfWeek = 1;
    public const int ImportantTalksPreferredLessonNumber = 1;

    public static bool IsDynamicPause(string? subjectName)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return false;

        var name = subjectName.Trim();
        return name.Contains("динам", StringComparison.OrdinalIgnoreCase)
               && name.Contains("пауз", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// РоВ — формально внеурочка (СП 3.4.16), но в школах ставят в сетку как урок.
    /// </summary>
    public static bool IsImportantTalksSubject(string? subjectName)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return false;

        var name = subjectName.Trim();
        if (name.Equals(ImportantTalksCanonicalName, StringComparison.OrdinalIgnoreCase))
            return true;

        return name.Contains("разговор", StringComparison.OrdinalIgnoreCase)
               && name.Contains("важн", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPhysicalEducationSubject(string? subjectName)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return false;

        return subjectName.Contains("физическ", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsForeignLanguageSubject(string? subjectName)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return false;

        var name = subjectName.Trim();
        return name.Contains("иностран", StringComparison.OrdinalIgnoreCase)
               || name.Contains("англий", StringComparison.OrdinalIgnoreCase)
               || name.Contains("немец", StringComparison.OrdinalIgnoreCase)
               || name.Contains("франц", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTechnologySubject(string? subjectName)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return false;

        var name = subjectName.Trim();
        return name.Contains("труд", StringComparison.OrdinalIgnoreCase)
               || name.Contains("технолог", StringComparison.OrdinalIgnoreCase);
    }

    public static bool RequiresRoom(string? subjectName) => !IsDynamicPause(subjectName);

    public static bool CountsAsRegularLesson(string? subjectName) =>
        !IsDynamicPause(subjectName);

    /// <summary>Строки нагрузки, которые не сравнивают с сеткой (дин. пауза — служебная строка 1 класса).</summary>
    public static bool CountsForLoadBalance(string? subjectName) =>
        !IsDynamicPause(subjectName);
}
