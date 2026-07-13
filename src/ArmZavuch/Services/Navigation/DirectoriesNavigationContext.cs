namespace ArmZavuch.Services.Navigation;

/// <summary>Контекст перехода в модуль «Справочники» (вкладка и выделение строки).</summary>
public sealed class DirectoriesNavigationContext
{
    public required int TabIndex { get; init; }
    public int? ClassId { get; init; }
    public int? TeacherId { get; init; }
    public string? SubjectName { get; init; }
}
