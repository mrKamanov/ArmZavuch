using ArmZavuch.Data.Repositories;

namespace ArmZavuch.Services.Staff;

/// <summary>
/// Синхронизация конструктора с классами педагога: из сетки — только «временные»,
/// явные (анкета) не снимаются при очистке ячейки.
/// </summary>
public sealed class TeacherClassPreferenceSyncService
{
    private readonly TeacherRepository _teachers;
    private readonly WeekTemplateRepository _templates;

    public TeacherClassPreferenceSyncService(TeacherRepository teachers, WeekTemplateRepository templates)
    {
        _teachers = teachers;
        _templates = templates;
    }

    public async Task SyncAfterSlotChangeAsync(int classId, int? previousTeacherId, int? newTeacherId)
    {
        if (previousTeacherId is int oldId && oldId != newTeacherId)
            await SyncAfterUnassignAsync(oldId, classId);

        if (newTeacherId is int newId)
            await SyncAfterAssignAsync(newId, classId);
    }

    public async Task SyncAfterUnassignAsync(int teacherId, int classId)
    {
        if (await _templates.HasTeacherClassSlotsAsync(teacherId, classId))
            return;

        await _teachers.RemoveSchedulePreferredClassAsync(teacherId, classId);
    }

    public async Task SyncAfterAssignAsync(int teacherId, int classId) =>
        await _teachers.AddSchedulePreferredClassAsync(teacherId, classId);
}
