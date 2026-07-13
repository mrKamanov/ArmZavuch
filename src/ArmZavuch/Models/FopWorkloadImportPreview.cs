namespace ArmZavuch.Models;

/// <summary>Сводка перед импортом ФОП: сколько строк добавится или изменится.</summary>
public sealed record FopWorkloadImportPreview(
    int MissingSubjects,
    int ExistingWithDifferentHours,
    int AlreadyMatching);
