using ArmZavuch.Models;
using ArmZavuch.Services.Navigation;

namespace ArmZavuch.ViewModels;

/// <summary>Переходы в справочники по контексту из других модулей.</summary>
public partial class DirectoriesViewModel
{
    private readonly IModuleNavigationService _navigation;

    public void ApplyPendingNavigationContext()
    {
        var context = _navigation.ConsumePendingDirectoriesContext();
        if (context is null)
            return;

        ActiveTabIndex = context.TabIndex;

        if (context.ClassId is int classId)
            SelectedClass = ClassList.FirstOrDefault(c => c.Id == classId);

        if (context.TeacherId is int teacherId)
            SelectedTeacher = TeacherList.FirstOrDefault(t => t.Id == teacherId);

        if (context.TabIndex == DirectoriesTabIndices.Curriculum && context.ClassId is int curriculumClassId)
            ApplyCurriculumNavigation(curriculumClassId, context.SubjectName);
    }

    private void ApplyCurriculumNavigation(int classId, string? subjectName)
    {
        var group = CurriculumGroups.FirstOrDefault(g => g.ClassId == classId);
        if (group is not null)
            group.IsExpanded = true;

        if (string.IsNullOrWhiteSpace(subjectName))
            return;

        foreach (var row in CurriculumGroups.SelectMany(g => g.Rows))
        {
            if (row.Item.ClassId != classId)
                continue;
            if (!row.Item.SubjectName.Equals(subjectName, StringComparison.OrdinalIgnoreCase))
                continue;

            SelectedCurriculumGridRow = row;
            SelectedCurriculumItem = row.Item;
            StatusMessage = $"Нагрузка: {row.Item.ClassName} · {row.Item.SubjectName}";
            return;
        }

        StatusMessage =
            $"Нагрузка: класс {ClassList.FirstOrDefault(c => c.Id == classId)?.DisplayName ?? classId.ToString()}, " +
            $"предмет «{subjectName}» не найден";
    }
}
