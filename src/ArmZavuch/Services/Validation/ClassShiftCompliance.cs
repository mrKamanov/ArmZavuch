using ArmZavuch.Data;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Validation;

/// <summary>
/// Требования к смене: СП 2.4.3648-20, п. 3.4.15 (при двухсменке).
/// Обучение 1, 5, 9–11 классов и классов для обучающихся с ОВЗ (в т.ч. коррекционных) — только в 1-ю смену.
/// </summary>
public static class ClassShiftCompliance
{
    public const string RuleCitation =
        "СП 2.4.3648-20, п. 3.4.15: при двухсменке 1, 5, 9–11 классы и коррекционные классы — только 1-я смена";

    private static readonly int[] FirstShiftOnlyGrades = [1, 5, 9, 10, 11];

    public static bool MustStudyFirstShiftOnly(SchoolClass cls) =>
        cls.IsCorrectional || FirstShiftOnlyGrades.Contains(cls.Grade);

    public static bool ViolatesSecondShiftRule(SchoolClass cls) =>
        cls.Shift == 2 && MustStudyFirstShiftOnly(cls);

    public static string DescribeClass(SchoolClass cls)
    {
        if (cls.IsCorrectional)
            return $"коррекционный класс {cls.DisplayName}";
        return $"{cls.Grade} класс ({cls.DisplayName})";
    }

    public static string FormatShiftViolation(SchoolClass cls) =>
        $"{DescribeClass(cls)} указан во 2-й смене. {RuleCitation}.";

    public static string? GetShiftWarning(SchoolClass cls) =>
        ViolatesSecondShiftRule(cls) ? FormatShiftViolation(cls) : null;

    public static ComplianceIssue CreateComplianceIssue(SchoolClass cls, int? dayOfWeek)
    {
        var dayName = dayOfWeek is int dayNum ? SanPiNRules.DayName(dayNum) : null;
        var message = dayOfWeek is null
            ? $"2-я смена: {DescribeClass(cls)} — по СанПиН только 1-я смена"
            : $"2-я смена: {DescribeClass(cls)} — по СанПиН только 1-я смена (есть уроки в этот день)";

        return dayOfWeek is int dow
            ? ComplianceIssueBuilder.Grid(
                ComplianceSeverity.Warning,
                "SANPIN_FIRST_SHIFT",
                message,
                cls.DisplayName,
                cls.Id,
                dow,
                dayName)
            : ComplianceIssueBuilder.Directory(
                ComplianceNavigationTarget.DirectoriesClasses,
                ComplianceSeverity.Warning,
                "SANPIN_FIRST_SHIFT",
                message,
                cls.DisplayName,
                cls.Id);
    }
}
