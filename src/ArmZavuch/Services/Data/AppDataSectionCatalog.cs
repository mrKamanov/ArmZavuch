using ArmZavuch.Models;

namespace ArmZavuch.Services.Data;

/// <summary>Описание разделов переноса: подписи, порядок импорта, зависимости.</summary>
public static class AppDataSectionCatalog
{
    public static IReadOnlyList<AppDataTransferSection> All { get; } =
        Enum.GetValues<AppDataTransferSection>().Cast<AppDataTransferSection>().ToList();

    public static IReadOnlyList<AppDataTransferSection> ImportOrder { get; } =
    [
        AppDataTransferSection.Buildings,
        AppDataTransferSection.Subjects,
        AppDataTransferSection.Bells,
        AppDataTransferSection.Teachers,
        AppDataTransferSection.Rooms,
        AppDataTransferSection.Classes,
        AppDataTransferSection.Curriculum,
        AppDataTransferSection.Schedule,
        AppDataTransferSection.Calendar,
        AppDataTransferSection.DayOperations
    ];

    public static IReadOnlyList<AppDataTransferSectionItem> CreateUiItems(bool availableOnly = false)
    {
        var items = All.Select(section => new AppDataTransferSectionItem
        {
            Section = section,
            Title = Title(section),
            Hint = Hint(section),
            IsSelected = false,
            IsAvailable = true
        }).ToList();

        return availableOnly ? items.Where(i => i.IsAvailable).ToList() : items;
    }

    public static string Title(AppDataTransferSection section) => section switch
    {
        AppDataTransferSection.Buildings => "Здания",
        AppDataTransferSection.Subjects => "Предметы",
        AppDataTransferSection.Classes => "Классы",
        AppDataTransferSection.Teachers => "Сотрудники",
        AppDataTransferSection.Rooms => "Кабинеты",
        AppDataTransferSection.Curriculum => "Нагрузка",
        AppDataTransferSection.Bells => "Звонки",
        AppDataTransferSection.Schedule => "Расписание (шаблоны недели)",
        AppDataTransferSection.Calendar => "Календарь и периоды",
        AppDataTransferSection.DayOperations => "Оперативка и журнал замен",
        _ => section.ToString()
    };

    public static string Hint(AppDataTransferSection section) => section switch
    {
        AppDataTransferSection.Buildings => "Здания и переходы между ними",
        AppDataTransferSection.Subjects => "Справочник предметов",
        AppDataTransferSection.Classes => "Классы, классное руководство, привязка к зданиям",
        AppDataTransferSection.Teachers => "Сотрудники, профили, отсутствия, предпочтения",
        AppDataTransferSection.Rooms => "Кабинеты и закреплённые учителя",
        AppDataTransferSection.Curriculum => "Нагрузка классов и шаблоны нагрузки",
        AppDataTransferSection.Bells => "Шаблоны звонков и расписание перемен",
        AppDataTransferSection.Schedule => "Недельные шаблоны и уроки в сетке",
        AppDataTransferSection.Calendar => "Каникулы, праздники, учебные периоды",
        AppDataTransferSection.DayOperations => "Правки дня и записи журнала замен",
        _ => ""
    };

    public static string SuggestPartialFileName(string schoolName, IReadOnlyList<AppDataTransferSection> sections)
    {
        var safe = string.Concat((schoolName ?? "Школа").Where(ch => !Path.GetInvalidFileNameChars().Contains(ch))).Trim();
        if (string.IsNullOrWhiteSpace(safe))
            safe = "Школа";

        var tag = sections.Count == All.Count
            ? "полная"
            : string.Join("-", sections.Select(s => ShortTag(s)));
        return $"Расписание_Про_{safe}_{tag}_{DateTime.Now:yyyy-MM-dd}{AppDataTransferService.FileExtension}";
    }

    private static string ShortTag(AppDataTransferSection section) => section switch
    {
        AppDataTransferSection.Buildings => "здания",
        AppDataTransferSection.Subjects => "предметы",
        AppDataTransferSection.Classes => "классы",
        AppDataTransferSection.Teachers => "сотрудники",
        AppDataTransferSection.Rooms => "кабинеты",
        AppDataTransferSection.Curriculum => "нагрузка",
        AppDataTransferSection.Bells => "звонки",
        AppDataTransferSection.Schedule => "расписание",
        AppDataTransferSection.Calendar => "календарь",
        AppDataTransferSection.DayOperations => "оперативка",
        _ => "данные"
    };
}
