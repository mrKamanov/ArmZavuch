namespace ArmZavuch.Models;

/// <summary>Результат импорта типовой нагрузки ФОП.</summary>
public sealed record FopWorkloadImportResult(int Added, int HoursUpdated, int Skipped);
