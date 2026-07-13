using System.Collections.ObjectModel;

namespace ArmZavuch.Models;

/// <summary>Блок нагрузки в карточке сотрудника: предметы, классы или «Остальные».</summary>
public sealed class CurriculumAssignmentSection
{
    public string Title { get; init; } = "";
    public ObservableCollection<CurriculumPreferenceItem> Items { get; } = [];
}
