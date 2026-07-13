using CommunityToolkit.Mvvm.ComponentModel;

namespace ArmZavuch.Models;

/// <summary>Раздел данных для выборочной выгрузки/загрузки.</summary>
public enum AppDataTransferSection
{
    Buildings,
    Subjects,
    Classes,
    Teachers,
    Rooms,
    Curriculum,
    Bells,
    Schedule,
    Calendar,
    DayOperations
}

/// <summary>Режим загрузки раздела: заменить или дополнить/обновить.</summary>
public enum AppDataImportMode
{
    Replace,
    Merge
}

/// <summary>Пункт выбора раздела в UI переноса данных.</summary>
public sealed partial class AppDataTransferSectionItem : ObservableObject
{
    public required AppDataTransferSection Section { get; init; }
    public required string Title { get; init; }
    public required string Hint { get; init; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isAvailable = true;
}
