namespace ArmZavuch.Services.Validation;

/// <summary>
/// Advisory — ручная сборка: только предупреждения.
/// Strict — те же правила, но нарушение считается ошибкой.
/// </summary>
public enum ScheduleRuleMode
{
    Advisory,
    Strict
}
