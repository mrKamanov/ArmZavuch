namespace ArmZavuch.Models;

/// <summary>Вариант класса-назначения для копирования нагрузки внутри параллели.</summary>
public sealed class CurriculumCopyClassOption : SelectableEntity
{
    public int ClassId { get; set; }
    public int Grade { get; set; }
    public string DisplayName { get; set; } = "";
}
