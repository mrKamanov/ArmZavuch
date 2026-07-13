namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Стабильные акцентные цвета по названию предмета для палитры конструктора.
/// Вход: название предмета. Выход: hex-цвета и короткая метка для бейджа.
/// </summary>
public static class SubjectAccentColors
{
    public sealed record Accent(string BorderHex, string BackgroundHex, string BadgeText);

    private static readonly Accent Unassigned = new("#64748B", "#F8FAFC", "?");

    private static readonly Dictionary<string, Accent> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Математика"] = new("#2563EB", "#EFF6FF", "Мат"),
        ["Алгебра"] = new("#2563EB", "#EFF6FF", "Алг"),
        ["Геометрия"] = new("#2563EB", "#EFF6FF", "Геом"),
        ["Вероятность и Статистика"] = new("#2563EB", "#EFF6FF", "Вер"),
        ["Вероятность и статистика"] = new("#2563EB", "#EFF6FF", "Вер"),
        ["Русский язык"] = new("#DC2626", "#FEF2F2", "Рус"),
        ["Литературное чтение"] = new("#B45309", "#FFFBEB", "Лит"),
        ["Литература"] = new("#B45309", "#FFFBEB", "Лит"),
        ["Иностранный язык"] = new("#7C3AED", "#F5F3FF", "Яз"),
        ["Физическая культура"] = new("#059669", "#ECFDF5", "ФК"),
        ["История"] = new("#92400E", "#FEF3C7", "Ист"),
        ["Обществознание"] = new("#A16207", "#FEF9C3", "Общ"),
        ["Биология"] = new("#16A34A", "#F0FDF4", "Био"),
        ["География"] = new("#0891B2", "#ECFEFF", "Гео"),
        ["Физика"] = new("#4338CA", "#EEF2FF", "Физ"),
        ["Химия"] = new("#C026D3", "#FAF5FF", "Хим"),
        ["Информатика"] = new("#6366F1", "#EEF2FF", "Инф"),
        ["Изобразительное искусство"] = new("#DB2777", "#FDF2F8", "ИЗО"),
        ["Музыка"] = new("#D97706", "#FFFBEB", "Муз"),
        ["Технология"] = new("#78716C", "#F5F5F4", "Тех"),
        ["Труд"] = new("#78716C", "#F5F5F4", "Труд"),
        ["Трудовое обучение"] = new("#78716C", "#F5F5F4", "Труд"),
        ["Окружающий мир"] = new("#65A30D", "#F7FEE7", "ОМ"),
        ["Динамическая пауза"] = new("#475569", "#F1F5F9", "ДП"),
        ["Разговоры о важном"] = new("#64748B", "#F8FAFC", "РоВ"),
        ["Факультатив"] = new("#4F46E5", "#EEF2FF", "Фак"),
        [TeacherPaletteMetrics.UnassignedSubjectGroup] = Unassigned
    };

    private static readonly Dictionary<string, string> FamilyBySubject = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Алгебра"] = "Математика",
        ["Геометрия"] = "Математика",
        ["Вероятность и Статистика"] = "Математика",
        ["Вероятность и статистика"] = "Математика",
        ["Литература"] = "Литературное чтение",
        ["Труд"] = "Технология",
        ["Трудовое обучение"] = "Технология"
    };

    private static readonly Accent[] Palette =
    [
        new("#2563EB", "#EFF6FF", "•"),
        new("#DC2626", "#FEF2F2", "•"),
        new("#7C3AED", "#F5F3FF", "•"),
        new("#059669", "#ECFDF5", "•"),
        new("#0891B2", "#ECFEFF", "•"),
        new("#D97706", "#FFFBEB", "•"),
        new("#DB2777", "#FDF2F8", "•"),
        new("#4338CA", "#EEF2FF", "•"),
        new("#C026D3", "#FAF5FF", "•"),
        new("#65A30D", "#F7FEE7", "•"),
        new("#92400E", "#FEF3C7", "•"),
        new("#6366F1", "#EEF2FF", "•")
    ];

    public static Accent Resolve(string? subjectName)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return Unassigned;

        var trimmed = subjectName.Trim();
        if (Known.TryGetValue(trimmed, out var exact))
            return exact;

        var familyKey = ResolveFamilyKey(trimmed);
        if (Known.TryGetValue(familyKey, out var family))
            return family with { BadgeText = CreateBadge(trimmed) };

        var index = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(familyKey)) % Palette.Length;
        return Palette[index] with { BadgeText = CreateBadge(trimmed) };
    }

    private static string ResolveFamilyKey(string trimmed)
    {
        if (FamilyBySubject.TryGetValue(trimmed, out var family))
            return family;

        if (trimmed.StartsWith("Факультатив", StringComparison.OrdinalIgnoreCase))
            return "Факультатив";

        if (Contains(trimmed, "алгебр") || Contains(trimmed, "геометр") || Contains(trimmed, "вероятност"))
            return "Математика";

        if (trimmed.StartsWith("Труд", StringComparison.OrdinalIgnoreCase)
            || Contains(trimmed, "технолог")
            || Contains(trimmed, "домовод"))
            return "Технология";

        return trimmed;
    }

    private static bool Contains(string value, string part) =>
        value.Contains(part, StringComparison.OrdinalIgnoreCase);

    private static string CreateBadge(string name) =>
        name.Length <= 3 ? name : name[..3];
}
