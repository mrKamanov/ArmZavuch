using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Schedule;

namespace ArmZavuch.Services.Staff;

/// <summary>
/// Синхронизация конструктора с нагрузкой: из сетки — только «временные» назначения,
/// явные (анкета, таблица) не снимаются при перестановке педагога.
/// </summary>
public sealed class TeacherCurriculumSyncService
{
    private readonly TeacherRepository _teachers;
    private readonly WeekTemplateRepository _templates;
    private readonly CurriculumRepository _curriculum;

    public TeacherCurriculumSyncService(
        TeacherRepository teachers,
        WeekTemplateRepository templates,
        CurriculumRepository curriculum)
    {
        _teachers = teachers;
        _templates = templates;
        _curriculum = curriculum;
    }

    public async Task SyncAfterSlotChangeAsync(
        int templateId,
        string templateWeekParity,
        int classId,
        int subjectId,
        int? previousTeacherId,
        int? newTeacherId)
    {
        var matching = TeacherCurriculumMatcher
            .MatchingItems(await _curriculum.GetAllAsync(), classId, subjectId, templateWeekParity)
            .ToList();
        if (matching.Count == 0)
            return;

        if (previousTeacherId is int oldId && oldId != newTeacherId)
        {
            foreach (var item in matching)
            {
                if (await HasSlotsForLineAsync(templateId, oldId, item))
                    continue;
                await _teachers.RemoveScheduleCurriculumAssignmentAsync(oldId, item.Id);
            }
        }

        if (newTeacherId is int newId)
        {
            foreach (var item in matching)
                await _teachers.AddScheduleCurriculumAssignmentAsync(newId, item.Id);
        }
    }

    private Task<bool> HasSlotsForLineAsync(int templateId, int teacherId, CurriculumItem item) =>
        _templates.HasTeacherClassSubjectSlotsAsync(
            templateId, teacherId, item.ClassId, item.SubjectId);
}
