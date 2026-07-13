namespace ArmZavuch.Models;

/// <summary>Замечание при проверке расписания (мягкий алерт).</summary>
public sealed class ComplianceIssue
{
    public ComplianceSeverity Severity { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public string? ClassName { get; init; }
    public string? DayName { get; init; }
    public int? ClassId { get; init; }
    public int? DayOfWeek { get; init; }
    public int? LessonNumber { get; init; }
    public int? TeacherId { get; init; }
    public ComplianceNavigationTarget NavigationTarget { get; init; }

    public string Icon => Severity switch
    {
        ComplianceSeverity.Error => "●",
        ComplianceSeverity.Warning => "●",
        _ => "●"
    };

    public string SeverityLabel => Severity switch
    {
        ComplianceSeverity.Error => "Накладка",
        ComplianceSeverity.Warning => "Замечание",
        _ => "Справка"
    };

    public bool HasContext => !string.IsNullOrWhiteSpace(ClassName) || !string.IsNullOrWhiteSpace(DayName);

    public bool CanNavigate => NavigationTarget != ComplianceNavigationTarget.None;

    public string NavigationHint => NavigationTarget switch
    {
        ComplianceNavigationTarget.ScheduleGrid => "Перейти в сетку",
        ComplianceNavigationTarget.DirectoriesClasses => "Перейти в классы",
        ComplianceNavigationTarget.DirectoriesTeachers => "Перейти к сотруднику",
        ComplianceNavigationTarget.DirectoriesCurriculum => "Перейти в нагрузку",
        _ => ""
    };

    public string StableKey =>
        $"{Severity}|{Code}|{ClassId}|{DayOfWeek}|{LessonNumber}|{TeacherId}|{ClassName}|{DayName}|{Message}";

    public string ContextLine
    {
        get
        {
            var hasClass = !string.IsNullOrWhiteSpace(ClassName);
            var hasDay = !string.IsNullOrWhiteSpace(DayName);

            return (hasClass, hasDay) switch
            {
                (true, true) => $"{ClassName} · {DayName}",
                (true, false) => ClassName!,
                (false, true) => DayName!,
                _ => ""
            };
        }
    }

    public string DisplayLine
    {
        get
        {
            var prefix = BuildDisplayPrefix();
            return string.IsNullOrEmpty(prefix)
                ? $"{Icon} {Message}"
                : $"{Icon} {prefix}: {Message}";
        }
    }

    private string BuildDisplayPrefix()
    {
        var hasClass = !string.IsNullOrWhiteSpace(ClassName);
        var hasDay = !string.IsNullOrWhiteSpace(DayName);

        return (hasClass, hasDay) switch
        {
            (true, true) => $"{ClassName}, {DayName}",
            (true, false) => ClassName!,
            (false, true) => DayName!,
            _ => ""
        };
    }
}

public enum ComplianceSeverity
{
    Info,
    Warning,
    Error
}
