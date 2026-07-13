using ArmZavuch.Models;

namespace ArmZavuch.Data;

/// <summary>
/// Эталонная недельная нагрузка ФОП (Menobr / приказ Минпросвещения №370, №704).
/// НОО: вариант 7.1, 5-дневка. ООО/СОО: вариант 1, 5-дневка (базовый профиль).
/// </summary>
public static class FopWorkloadReference
{
    public static IReadOnlyList<FopHoursEntry> All { get; } = Build();

    public static IEnumerable<FopHoursEntry> ForGrade(int grade) =>
        All.Where(e => e.Grade == grade);

    private static List<FopHoursEntry> Build()
    {
        var list = new List<FopHoursEntry>();
        list.AddRange(BuildNoo());
        list.AddRange(BuildOoo());
        list.AddRange(BuildSoo());
        return list;
    }

    /// <summary>ФОП НОО, вариант 7.1 (5-дневная неделя).</summary>
    private static IEnumerable<FopHoursEntry> BuildNoo()
    {
        const string plan = "ФОП НОО вар.7.1";
        foreach (var e in NooGrade(1, plan,
            ("Русский язык", 5, 1.1),
            ("Литературное чтение", 4, 1.0),
            ("Математика", 4, 1.2),
            ("Окружающий мир", 2, 1.0),
            ("Изобразительное искусство", 1, 0.8),
            ("Музыка", 1, 0.8),
            ("Технология", 1, 0.9),
            ("Физическая культура", 2, 0.7)))
            yield return e;

        foreach (var e in NooGrade(2, plan,
            ("Русский язык", 5, 1.1),
            ("Литературное чтение", 4, 1.0),
            ("Иностранный язык", 2, 1.1),
            ("Математика", 4, 1.2),
            ("Окружающий мир", 2, 1.0),
            ("Изобразительное искусство", 1, 0.8),
            ("Музыка", 1, 0.8),
            ("Технология", 1, 0.9),
            ("Физическая культура", 2, 0.7)))
            yield return e;

        foreach (var e in NooGrade(3, plan,
            ("Русский язык", 5, 1.1),
            ("Литературное чтение", 4, 1.0),
            ("Иностранный язык", 2, 1.1),
            ("Математика", 4, 1.2),
            ("Окружающий мир", 2, 1.0),
            ("Изобразительное искусство", 1, 0.8),
            ("Музыка", 1, 0.8),
            ("Технология", 1, 0.9),
            ("Физическая культура", 2, 0.7)))
            yield return e;

        foreach (var e in NooGrade(4, plan,
            ("Русский язык", 5, 1.1),
            ("Литература", 4, 1.1),
            ("Иностранный язык", 2, 1.1),
            ("Математика", 4, 1.2),
            ("Окружающий мир", 2, 1.0),
            ("Основы религиозных культур и светской этики", 1, 0.8),
            ("Изобразительное искусство", 1, 0.8),
            ("Музыка", 1, 0.8),
            ("Технология", 1, 0.9),
            ("Физическая культура", 2, 0.7)))
            yield return e;
    }

    private static IEnumerable<FopHoursEntry> NooGrade(int grade, string plan,
        params (string Name, double Hours, double Diff)[] items)
    {
        foreach (var (name, hours, diff) in items)
        {
            yield return new FopHoursEntry
            {
                SubjectName = name,
                Grade = grade,
                HoursPerWeek = hours,
                Level = EducationLevel.Noo,
                PlanVariant = plan,
                DifficultyScore = diff
            };
        }
    }

    /// <summary>ФОП ООО, вариант 1 (5-дневная неделя).</summary>
    private static IEnumerable<FopHoursEntry> BuildOoo()
    {
        const string plan = "ФОП ООО вар.1";
        yield return Ooo(5, plan, "Русский язык", 5, 1.2);
        yield return Ooo(5, plan, "Литература", 3, 1.1);
        yield return Ooo(5, plan, "Иностранный язык", 3, 1.1);
        yield return Ooo(5, plan, "Математика", 5, 1.3);
        yield return Ooo(5, plan, "История", 2, 1.1);
        yield return Ooo(5, plan, "География", 1, 1.1);
        yield return Ooo(5, plan, "Биология", 1, 1.2);
        yield return Ooo(5, plan, "Информатика", 1, 1.2);
        yield return Ooo(5, plan, "Изобразительное искусство", 1, 0.8);
        yield return Ooo(5, plan, "Музыка", 1, 0.8);
        yield return Ooo(5, plan, "Технология", 2, 0.9);
        yield return Ooo(5, plan, "Физическая культура", 3, 0.7);

        foreach (var g in new[] { 6, 7, 8, 9 })
        {
            yield return Ooo(g, plan, "Русский язык", 5, 1.2);
            yield return Ooo(g, plan, "Литература", 3, 1.1);
            yield return Ooo(g, plan, "Иностранный язык", 3, 1.1);
            yield return Ooo(g, plan, "Математика", 5, 1.3);
            yield return Ooo(g, plan, "История", 2, 1.1);
            yield return Ooo(g, plan, "Обществознание", g >= 6 ? 1 : 0, 1.1);
            yield return Ooo(g, plan, "География", g >= 6 ? 1 : 0, 1.1);
            yield return Ooo(g, plan, "Биология", 1, 1.2);
            yield return Ooo(g, plan, "Информатика", 1, 1.2);
            yield return Ooo(g, plan, "Изобразительное искусство", 1, 0.8);
            yield return Ooo(g, plan, "Музыка", 1, 0.8);
            yield return Ooo(g, plan, "Технология", g <= 7 ? 2 : 1, 0.9);
            yield return Ooo(g, plan, "Физическая культура", 3, 0.7);
            if (g >= 7)
            {
                yield return Ooo(g, plan, "Физика", 2, 1.4);
                yield return Ooo(g, plan, "Основы безопасности жизнедеятельности", 1, 1.0);
            }
            if (g >= 8)
                yield return Ooo(g, plan, "Химия", 2, 1.4);
        }
    }

    /// <summary>ФОП СОО, базовый профиль (5-дневка).</summary>
    private static IEnumerable<FopHoursEntry> BuildSoo()
    {
        const string plan = "ФОП СОО базовый";
        foreach (var g in new[] { 10, 11 })
        {
            yield return Soo(g, plan, "Русский язык", 3, 1.2);
            yield return Soo(g, plan, "Литература", 3, 1.1);
            yield return Soo(g, plan, "Иностранный язык", 3, 1.1);
            yield return Soo(g, plan, "Алгебра", 3, 1.4);
            yield return Soo(g, plan, "Геометрия", 2, 1.4);
            yield return Soo(g, plan, "Информатика", 1, 1.2);
            yield return Soo(g, plan, "История", 2, 1.1);
            yield return Soo(g, plan, "Обществознание", 2, 1.1);
            yield return Soo(g, plan, "География", 1, 1.1);
            yield return Soo(g, plan, "Биология", 1, 1.2);
            yield return Soo(g, plan, "Физика", 2, 1.4);
            yield return Soo(g, plan, "Химия", 2, 1.4);
            yield return Soo(g, plan, "Физическая культура", 3, 0.7);
            yield return Soo(g, plan, "Основы безопасности жизнедеятельности", 1, 1.0);
        }
    }

    private static FopHoursEntry Ooo(int grade, string plan, string name, double hours, double diff) => new()
    {
        SubjectName = name,
        Grade = grade,
        HoursPerWeek = hours,
        Level = EducationLevel.Ooo,
        PlanVariant = plan,
        DifficultyScore = diff
    };

    private static FopHoursEntry Soo(int grade, string plan, string name, double hours, double diff) => new()
    {
        SubjectName = name,
        Grade = grade,
        HoursPerWeek = hours,
        Level = EducationLevel.Soo,
        PlanVariant = plan,
        DifficultyScore = diff
    };
}
