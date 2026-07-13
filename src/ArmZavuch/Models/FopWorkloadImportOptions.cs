namespace ArmZavuch.Models;

/// <summary>Параметры импорта типовой нагрузки ФОП в справочник школы.</summary>
public sealed class FopWorkloadImportOptions
{
    public bool OverwriteExistingHours { get; init; }

    public static FopWorkloadImportOptions FillGapsOnly => new() { OverwriteExistingHours = false };

    public static FopWorkloadImportOptions OverwriteHours => new() { OverwriteExistingHours = true };
}
