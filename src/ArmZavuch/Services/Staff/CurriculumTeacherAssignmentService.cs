using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Staff;

/// <summary>
/// Единая точка записи явных назначений (таблица нагрузки, галочки в анкете).
/// Защита: случайная массовая очистка строки отклоняется.
/// </summary>
public sealed class CurriculumTeacherAssignmentService
{
    private readonly TeacherRepository _teachers;

    public CurriculumTeacherAssignmentService(TeacherRepository teachers) => _teachers = teachers;

    public async Task<CurriculumAssigneeWriteResult> TrySetAssigneesAsync(
        int curriculumId,
        bool hasSubgroups,
        IReadOnlyList<int> teacherIds,
        bool allowClearExisting)
    {
        var normalized = CurriculumTeacherAssignmentRules.Normalize(hasSubgroups, teacherIds);
        var existing = await _teachers.GetExplicitAssigneesForCurriculumAsync(curriculumId);

        if (normalized.Count == 0 && existing.Count > 0 && !allowClearExisting)
            return CurriculumAssigneeWriteResult.RejectedWouldClearExisting;

        if (normalized.SequenceEqual(existing.OrderBy(id => id)))
            return CurriculumAssigneeWriteResult.Success;

        await _teachers.SetCurriculumAssigneesAsync(curriculumId, normalized);
        return CurriculumAssigneeWriteResult.Success;
    }

    public async Task RefreshTeachersAsync(IEnumerable<Teacher> teachers, IEnumerable<int> teacherIds)
    {
        var ids = teacherIds.ToHashSet();
        foreach (var teacher in teachers.Where(t => ids.Contains(t.Id)))
            await _teachers.RefreshCurriculumAssignmentsAsync(teacher);
    }
}
