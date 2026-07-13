using System.Globalization;

namespace ArmZavuch.Data;

/// <summary>
/// Баллы трудности (Сивкова) по вариантам расписаний Минпросвещения РФ.
/// Для каждой параллели — своё значение на урок.
/// </summary>
public static partial class OfficialSubjectDifficultyReference
{
    /// <summary>Подпись источника в интерфейсе (письмо Минпросвещения, варианты расписаний).</summary>
    public const string SourceLabel = "варианты расписаний Минпросвещения";

    /// <summary>Кратко: «по шаблону…» в подсказках.</summary>
    public const string SourceLabelDative = "шаблону Минпросвещения";

    /// <summary>После предлога «по»: вариантам расписаний…</summary>
    public const string SourceLabelDativeVariants = "вариантам расписаний Минпросвещения";

    public const double DefaultFallback = 5;

    private static readonly Dictionary<int, Dictionary<string, double>> ByGrade = new();
    private static readonly Dictionary<string, double> FlatScores = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lazy<IReadOnlyList<string>> LookupNamesLazy = new(BuildLookupNames);

    public static IReadOnlyList<string> AllLookupNames => LookupNamesLazy.Value;

    static OfficialSubjectDifficultyReference()
    {
        RegisterGradeScores();
        RegisterExtras();
    }

    private static partial void RegisterGradeScores();

    private static void RegisterExtras()
    {
        SetFlat("Разговоры о важном", 1);
        SetFlat("Россия — мои горизонты", 1);
        SetFlat("Классный час", 1);
        SetFlat("Внеурочная деятельность", 1);
        SetFlat("Воспитательная работа", 1);
        SetFlat("Профориентация", 3);
        SetFlat("Профориентационный минимум", 3);
        SetFlat("Дополнительное образование", 2);
        SetFlat("Кружок", 2);
        SetFlat("Секция", 2);
        SetFlat("Группа продлённого дня", 1);
        SetFlat("Портфолио", 1);
        SetFlat("Самоопределение", 2);
        SetFlat("Динамическая пауза", 1);
        SetFlat("ДКП", 5);

        SetFlat("Факультатив", 6);
        SetFlat("Коррекционный час", 3);
        SetFlat("Логопедический час", 3);
        SetFlat("Индивидуальное консультирование", 5);
        SetFlat("Адаптированная программа", 3);
        SetFlat("Занятия с тьютором", 3);

        Alias("Изобразительное искусство", "ИЗО");
        Alias("Технология", "Труд (технология)");
        Alias("Труд", "Труд (технология)");
        Alias("Трудовое обучение", "Труд (технология)");
        Alias("Домоводство", "Труд (технология)");
        Alias("Основы религиозных культур и светской этики", "ОРКСЭ");
        Alias("Основы безопасности жизнедеятельности", "ОБЗР");
        Alias("Основы безопасности и защиты Родины", "ОБЗР");
        Alias("Литература родного языка", "Родная литература");
        Alias("Информационные технологии", "Информатика");
        Alias("Естествознание", "Окружающий мир");
        Alias("История России", "История");
        Alias("Всеобщая история", "История");
        Alias("География моего края", "География");
        Alias("Граждановедение", "Обществознание");
        Alias("Русский как иностранный", "Русский язык");
        Alias("Математический практикум", "Математика");
        Alias("Математическая грамотность", "Математика");
        Alias("Читательская грамотность", "Литературное чтение");
        Alias("Естественнонаучная грамотность", "Биология");
        Alias("Физика и астрономия", "Физика");
        Alias("Химия и экология", "Химия");
        Alias("Биология и экология", "Биология");
        Alias("Астрономия", "Физика");
        Alias("Компьютерная графика", "Информатика");
        Alias("Английский язык", "Иностранный язык");
        Alias("Немецкий язык", "Иностранный язык");
        Alias("Французский язык", "Иностранный язык");
        Alias("Испанский язык", "Иностранный язык");
        Alias("Китайский язык", "Иностранный язык");
        Alias("Иностранный", "Иностранный язык");
        Alias("Английский", "Иностранный язык");
        Alias("Физическая культур", "Физическая культура");
    }

    private static void SetGrade(int grade, string name, double score)
    {
        if (!ByGrade.TryGetValue(grade, out var map))
        {
            map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            ByGrade[grade] = map;
        }
        map[name] = score;
    }

    private static void SetFlat(string name, double score) => FlatScores[name] = score;

    private static void Alias(string alias, string canonical) => Aliases[alias] = canonical;

    public static bool TryGet(string? rawName, int grade, out double score) =>
        TryGetInternal(rawName, grade, out score);

    public static bool TryGet(string? rawName, out double score) =>
        TryGetInternal(rawName, null, out score);

    public static double GetOrDefault(string? rawName, int grade, double fallback = DefaultFallback) =>
        TryGet(rawName, grade, out var score) ? score : fallback;

    public static double GetOrDefault(string? rawName, double fallback = DefaultFallback) =>
        TryGet(rawName, out var score) ? score : fallback;

    /// <summary>Официальный балл для параллели класса; иначе сохранённый в справочнике.</summary>
    public static double ResolveForClass(string? rawName, int grade, double storedScore)
    {
        if (TryGet(rawName, grade, out var official))
            return official;
        return storedScore > 0 ? storedScore : DefaultFallback;
    }

    public static IEnumerable<string> GetLookupNamesForGrade(int grade)
    {
        if (ByGrade.TryGetValue(grade, out var map))
        {
            foreach (var name in map.Keys)
                yield return name;
        }
        foreach (var name in FlatScores.Keys)
            yield return name;
    }

    public static string? FormatScoreRange(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return null;

        var canonical = ResolveCanonical(rawName.Trim()) ?? rawName.Trim();

        var values = new List<double>();
        for (var g = 1; g <= 11; g++)
        {
            if (ByGrade.TryGetValue(g, out var map)
                && map.TryGetValue(canonical, out var score))
                values.Add(score);
        }

        if (values.Count == 0)
            return TryGet(rawName, out var flat) ? FormatScore(flat) : null;

        var min = values.Min();
        var max = values.Max();
        return min.Equals(max) ? FormatScore(min) : $"{FormatScore(min)}–{FormatScore(max)}";
    }

    public static string FormatScore(double score) =>
        score.ToString("0.##", CultureInfo.InvariantCulture);

    private static bool TryGetInternal(string? rawName, int? grade, out double score)
    {
        score = 0;
        var name = Normalize(rawName);
        if (name.Length == 0)
            return false;

        var canonical = ResolveCanonical(name) ?? name;

        if (grade is int g && ByGrade.TryGetValue(g, out var gradeMap))
        {
            if (gradeMap.TryGetValue(canonical, out score))
                return true;
            if (gradeMap.TryGetValue(name, out score))
                return true;

            if (g >= 7 && IsLegacyMathName(canonical))
            {
                score = ResolveLegacyMathScore(gradeMap);
                if (score > 0)
                    return true;
            }

            var best = gradeMap.Keys
                .Select(key => (Key: key, Score: gradeMap[key]))
                .Where(x => name.Contains(x.Key, StringComparison.OrdinalIgnoreCase)
                            || x.Key.Contains(name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Key.Length)
                .FirstOrDefault();
            if (best.Key is not null)
            {
                score = best.Score;
                return true;
            }
        }

        if (FlatScores.TryGetValue(canonical, out score) || FlatScores.TryGetValue(name, out score))
            return true;

        if (grade is null)
        {
            for (g = 1; g <= 11; g++)
            {
                if (TryGetInternal(name, g, out score))
                    return true;
            }
        }

        return false;
    }

    private static string? ResolveCanonical(string name)
    {
        if (Aliases.TryGetValue(name, out var canonical))
            return canonical;
        return null;
    }

    private static bool IsLegacyMathName(string name) =>
        name.Equals("Математика", StringComparison.OrdinalIgnoreCase);

    /// <summary>В 7–11 кл. в шаблоне Минпросвещения вместо «Математики» — алгебра, геометрия и т.д.</summary>
    private static double ResolveLegacyMathScore(Dictionary<string, double> gradeMap)
    {
        double max = 0;
        foreach (var part in new[] { "Алгебра", "Геометрия", "Вероятность и статистика" })
        {
            if (gradeMap.TryGetValue(part, out var partScore))
                max = Math.Max(max, partScore);
        }
        return max;
    }

    private static List<string> BuildLookupNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var map in ByGrade.Values)
        {
            foreach (var key in map.Keys)
                names.Add(key);
        }
        foreach (var key in FlatScores.Keys)
            names.Add(key);
        foreach (var key in Aliases.Keys)
            names.Add(key);
        return names.OrderBy(n => n).ToList();
    }

    private static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        return raw.Trim().TrimEnd('*').Replace("  ", " ", StringComparison.Ordinal);
    }
}
