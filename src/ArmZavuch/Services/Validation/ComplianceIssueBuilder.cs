using ArmZavuch.Data;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Validation;

/// <summary>Фабрика замечаний проверки сетки с полями навигации.</summary>
internal static class ComplianceIssueBuilder
{
    public static ComplianceIssue Grid(
        ComplianceSeverity severity,
        string code,
        string message,
        string? className = null,
        int? classId = null,
        int? dayOfWeek = null,
        string? dayName = null,
        int? lessonNumber = null,
        int? teacherId = null)
    {
        var target = classId is not null || dayOfWeek is not null || teacherId is not null
            ? ComplianceNavigationTarget.ScheduleGrid
            : ComplianceNavigationTarget.None;

        return new ComplianceIssue
        {
            Severity = severity,
            Code = code,
            Message = message,
            ClassName = className,
            ClassId = classId,
            DayOfWeek = dayOfWeek,
            DayName = dayName ?? (dayOfWeek is int day ? SanPiNRules.DayName(day) : null),
            LessonNumber = lessonNumber,
            TeacherId = teacherId,
            NavigationTarget = target
        };
    }

    public static ComplianceIssue Directory(
        ComplianceNavigationTarget target,
        ComplianceSeverity severity,
        string code,
        string message,
        string? className = null,
        int? classId = null,
        int? teacherId = null,
        int? dayOfWeek = null)
    {
        return new ComplianceIssue
        {
            Severity = severity,
            Code = code,
            Message = message,
            ClassName = className,
            ClassId = classId,
            TeacherId = teacherId,
            DayOfWeek = dayOfWeek,
            DayName = dayOfWeek is int day ? SanPiNRules.DayName(day) : null,
            NavigationTarget = target
        };
    }
}
