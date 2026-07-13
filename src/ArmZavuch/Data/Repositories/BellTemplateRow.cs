namespace ArmZavuch.Data.Repositories;

/// <summary>Строка справочника шаблонов звонков.</summary>
public sealed record BellTemplateRow(int Id, string Name, int GradeFrom, int GradeTo);
