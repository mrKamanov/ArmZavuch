using ArmZavuch.Data;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Validation;

/// <summary>
/// Педагогические правила из практики завуча (поверх СанПиН и шаблона Минпросвещения).
/// </summary>
public static class SchedulePedagogyRules
{
    public static bool SameSubjectName(string? a, string? b) =>
        NormalizeSubject(a) == NormalizeSubject(b);

    /// <summary>Можно ли ставить два урока одного предмета подряд.</summary>
    public static bool AllowsConsecutiveSameSubject(int grade, string? subjectName, double difficultyScore)
    {
        if (IsPairExemptSubject(subjectName))
            return true;

        if (grade is >= 1 and <= 4)
            return false;

        if (grade is >= 5 and <= 8)
            return false;

        if (grade >= 9)
            return difficultyScore < SanPiNRules.HardSubjectThreshold
                   || IsProfilePairSubject(subjectName, grade);

        return false;
    }

    public static bool IsPairExemptSubject(string? subjectName)
    {
        var name = NormalizeSubject(subjectName);
        if (name.Length == 0)
            return false;

        if (name.Contains("физическ", StringComparison.OrdinalIgnoreCase))
            return true;

        return name.Contains("труд", StringComparison.OrdinalIgnoreCase)
               || name.Contains("технолог", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Профильные предметы 10–11 кл. — пары без порога &lt; 7 (проверьте профиль класса).</summary>
    public static bool IsProfilePairSubject(string? subjectName, int grade)
    {
        if (grade < 10)
            return false;

        var name = NormalizeSubject(subjectName);
        foreach (var profile in ProfilePairSubjectNames)
        {
            if (name.Equals(profile, StringComparison.OrdinalIgnoreCase)
                || name.Contains(profile, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static ComplianceSeverity PairViolationSeverity(ScheduleRuleMode mode) =>
        mode == ScheduleRuleMode.Strict ? ComplianceSeverity.Error : ComplianceSeverity.Warning;

    public static ComplianceSeverity NooPairViolationSeverity(ScheduleRuleMode mode) =>
        mode == ScheduleRuleMode.Strict ? ComplianceSeverity.Error : ComplianceSeverity.Warning;

    public static string FormatPairViolationMessage(int grade, string subjectName, double difficultyScore)
    {
        if (grade is >= 5 and <= 8)
            return $"Подряд два урока «{subjectName}» (в {grade} кл. не рекомендуется; исключения — физкультура, труд)";

        if (grade >= 9 && difficultyScore >= SanPiNRules.HardSubjectThreshold)
            return $"Подряд два урока «{subjectName}» (балл {FormatScore(difficultyScore)} ≥ 7; в старших допустимо только при балле < 7 или для профильных)";

        return $"Подряд два урока «{subjectName}» (не рекомендуется)";
    }

    /// <summary>Сумма баллов за день: каждый слот (в т.ч. подгруппа) считается отдельно.</summary>
    public static double SumDailyDifficulty(
        IEnumerable<LessonSlot> daySlots,
        SchoolClass cls,
        IReadOnlyDictionary<int, Subject> subjectMap,
        Func<LessonSlot, SchoolClass, Dictionary<int, Subject>, double> resolveDifficulty)
    {
        var map = subjectMap as Dictionary<int, Subject>
                  ?? subjectMap.ToDictionary(kv => kv.Key, kv => kv.Value);
        return daySlots.Sum(s => resolveDifficulty(s, cls, map));
    }

    public static IEnumerable<TeacherWindowIssue> EvaluateTeacherWindows(
        IReadOnlyList<int> taughtLessons,
        int maxLoadHours,
        ScheduleRuleMode mode)
    {
        if (taughtLessons.Count < 2)
            yield break;

        var ordered = taughtLessons.Distinct().OrderBy(n => n).ToList();
        var blocks = new List<(int From, int To)>();
        for (var i = 1; i < ordered.Count; i++)
        {
            var gapFrom = ordered[i - 1] + 1;
            var gapTo = ordered[i] - 1;
            if (gapTo < gapFrom)
                continue;
            blocks.Add((gapFrom, gapTo));
        }

        if (blocks.Count == 0)
            yield break;

        var severity = mode == ScheduleRuleMode.Strict
            ? ComplianceSeverity.Error
            : ComplianceSeverity.Warning;

        if (blocks.Count > SanPiNRules.MaxTeacherWindowsPerDay)
        {
            yield return new TeacherWindowIssue(
                severity,
                "TEACHER_WINDOWS_COUNT",
                $"Окон {blocks.Count} при норме не более {SanPiNRules.MaxTeacherWindowsPerDay}");
        }

        foreach (var (from, to) in blocks.Where(b => b.To > b.From))
        {
            yield return new TeacherWindowIssue(
                severity,
                "TEACHER_WINDOWS_ADJACENT",
                $"Смежные окна (уроки {from}–{to} без занятий) — не рекомендуется");
        }

        if (maxLoadHours >= SanPiNRules.FullLoadHoursThreshold && blocks.Count > 0)
        {
            yield return new TeacherWindowIssue(
                mode == ScheduleRuleMode.Strict ? ComplianceSeverity.Error : ComplianceSeverity.Info,
                "TEACHER_WINDOWS_FULLLOAD",
                $"Ставка {maxLoadHours} ч — окна по возможности минимизировать");
        }
    }

    public static ComplianceSeverity BuildingDaySeverity(ScheduleRuleMode mode) =>
        mode == ScheduleRuleMode.Strict ? ComplianceSeverity.Error : ComplianceSeverity.Warning;

    private static string NormalizeSubject(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? "" : raw.Trim();

    private static string FormatScore(double score) =>
        score.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly string[] ProfilePairSubjectNames =
    [
        "Алгебра",
        "Геометрия",
        "Физика",
        "Химия",
        "Биология",
        "Информатика",
        "Вероятность и статистика"
    ];
}

public readonly record struct TeacherWindowIssue(
    ComplianceSeverity Severity,
    string Code,
    string Message);
