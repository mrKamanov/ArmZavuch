namespace ArmZavuch.Data;

/// <summary>Нормы СанПиН 1.2.3685-21 для проверки сетки (мягкие алерты).</summary>
public static class SanPiNRules
{
    public static int MaxLessonsPerDay(int grade) => grade switch
    {
        1 => 4,
        <= 4 => 5,
        <= 6 => 6,
        _ => 7
    };

    /// <summary>Рекомендуемая сумма баллов трудности (Сивков) на класс в день — по вариантам расписаний Минпросвещения.</summary>
    public static double MaxDailyDifficultySum(int grade) => grade switch
    {
        1 => 27,
        <= 4 => 32,
        <= 6 => 48,
        <= 9 => 50,
        _ => 55
    };

    /// <summary>Предмет считается «тяжёлым», если балл одного урока ≥ 7 (правило парных уроков).</summary>
    public static double HardSubjectThreshold => 7.0;

    public static int MaxSameSubjectPerDay(int grade) => grade <= 4 ? 1 : 2;

    public static int MaxTeacherLessonsPerDayWarning => 6;

    public static int MaxTeacherLessonsPerDay => 7;

    /// <summary>Не более стольких «окон» (разрывов между уроками) в день у педагога.</summary>
    public static int MaxTeacherWindowsPerDay => 2;

    /// <summary>От этой недельной нагрузки окна минимизируют (кроме надомников — через нерабочее время).</summary>
    public static int FullLoadHoursThreshold => 23;

    public static string DayName(int dayOfWeek) => dayOfWeek switch
    {
        1 => "Пн",
        2 => "Вт",
        3 => "Ср",
        4 => "Чт",
        5 => "Пт",
        6 => "Сб",
        _ => "?"
    };

    public static bool TryParseDayName(string? dayName, out int dayOfWeek)
    {
        dayOfWeek = dayName?.Trim() switch
        {
            "Пн" => 1,
            "Вт" => 2,
            "Ср" => 3,
            "Чт" => 4,
            "Пт" => 5,
            "Сб" => 6,
            _ => 0
        };
        return dayOfWeek > 0;
    }
}
