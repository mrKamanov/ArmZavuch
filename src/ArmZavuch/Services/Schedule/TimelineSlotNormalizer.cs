using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Починка слотов 1 класса: урок не на номере дин. паузы; «сироты» вне timeline удаляются или переносятся.
/// Вход: шаблон недели, класс, звонки. Выход: число исправленных слотов в БД.
/// </summary>
public static class TimelineSlotNormalizer
{
    public static async Task<int> NormalizeClassSlotsAsync(
        WeekTemplateRepository templates,
        int templateId,
        SchoolClass cls,
        IReadOnlyList<BellPeriod> bells,
        string? templateName = null)
    {
        if (cls.Grade != ScheduleGridBuilder.FirstGradeTimelineGrade)
            return 0;

        templateName ??= string.IsNullOrWhiteSpace(cls.BellTemplateName) ? null : cls.BellTemplateName;
        var templatePeriods = BellScheduleResolver.FilterByTemplate(bells, templateName);
        var bellsScope = templatePeriods.Count > 0 ? templatePeriods : bells;
        var timeline = BellScheduleResolver.BuildPrimaryTimeline(
            bellsScope, cls.Grade, cls.Shift, templateName: templateName);
        if (timeline.Count == 0)
            return 0;

        var slots = (await templates.GetAllSlotsForTemplateAsync(templateId))
            .Where(s => s.ClassId == cls.Id)
            .ToList();

        var changes = 0;
        changes += await FixLessonsOnPauseStorageAsync(templates, timeline, slots);
        changes += await ReconcileOrphanSlotsAsync(templates, timeline, slots);
        return changes;
    }

    public static async Task<int> NormalizeTemplateSlotsAsync(
        WeekTemplateRepository templates,
        int templateId,
        IEnumerable<SchoolClass> classes,
        IReadOnlyList<BellPeriod> bells,
        BellTemplateAssignmentSnapshot? assignment = null)
    {
        assignment ??= BellTemplateAssignmentSnapshot.Fallback;
        var changes = 0;
        foreach (var cls in classes.Where(c => c.Grade == ScheduleGridBuilder.FirstGradeTimelineGrade))
        {
            var templateName = assignment.GetTemplateName(cls);
            changes += await NormalizeClassSlotsAsync(templates, templateId, cls, bells, templateName);
        }

        return changes;
    }

    private static async Task<int> FixLessonsOnPauseStorageAsync(
        WeekTemplateRepository templates,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        List<LessonSlot> slots)
    {
        var pauseStorages = timeline
            .Where(c => c.IsDynamicPause)
            .Select(c => ScheduleGridBuilder.ResolveDefaultStorageLessonNumber(c, timeline))
            .ToHashSet();
        if (pauseStorages.Count == 0)
            return 0;

        var changes = 0;
        foreach (var slot in slots.ToList())
        {
            if (SubjectScheduleRules.IsDynamicPause(slot.SubjectName))
                continue;

            if (!pauseStorages.Contains(slot.LessonNumber))
                continue;

            var target = slot.LessonNumber + 1;
            if (!IsOccupied(slots, slot, target))
            {
                await templates.RelocateSlotLessonNumberAsync(slot.SlotId, target);
                slot.LessonNumber = target;
                changes++;
                continue;
            }

            await templates.DeleteSlotAsync(slot.SlotId);
            slots.Remove(slot);
            changes++;
        }

        return changes;
    }

    private static async Task<int> ReconcileOrphanSlotsAsync(
        WeekTemplateRepository templates,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        List<LessonSlot> slots)
    {
        var lessonStorages = ScheduleGridBuilder.CollectTimelineLessonStorageNumbers(timeline);
        if (lessonStorages.Count == 0)
            return 0;

        var changes = 0;
        foreach (var slot in slots.ToList())
        {
            if (SubjectScheduleRules.IsDynamicPause(slot.SubjectName))
                continue;

            if (lessonStorages.Contains(slot.LessonNumber))
                continue;

            var twin = slots.FirstOrDefault(s =>
                s.SlotId != slot.SlotId
                && s.ClassId == slot.ClassId
                && s.DayOfWeek == slot.DayOfWeek
                && s.SubgroupIndex == slot.SubgroupIndex
                && lessonStorages.Contains(s.LessonNumber)
                && s.SubjectId == slot.SubjectId
                && !SubjectScheduleRules.IsDynamicPause(s.SubjectName));

            if (twin is not null)
            {
                await templates.DeleteSlotAsync(slot.SlotId);
                slots.Remove(slot);
                changes++;
                continue;
            }

            var target = FindEmptyLessonStorage(timeline, slots, slot);
            if (!target.HasValue || IsOccupied(slots, slot, target.Value))
                continue;

            await templates.RelocateSlotLessonNumberAsync(slot.SlotId, target.Value);
            slot.LessonNumber = target.Value;
            changes++;
        }

        return changes;
    }

    private static int? FindEmptyLessonStorage(
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        IReadOnlyList<LessonSlot> slots,
        LessonSlot orphan)
    {
        foreach (var column in timeline)
        {
            if (column.IsDynamicPause || column.IsBreak)
                continue;

            foreach (var storage in ScheduleGridBuilder.ResolveLessonStorageNumbers(column, timeline))
            {
                if (IsOccupied(slots, orphan, storage))
                    continue;

                return storage;
            }
        }

        return null;
    }

    private static bool IsOccupied(
        IReadOnlyList<LessonSlot> slots,
        LessonSlot candidate,
        int lessonNumber) =>
        slots.Any(s =>
            s.SlotId != candidate.SlotId
            && s.DayOfWeek == candidate.DayOfWeek
            && s.ClassId == candidate.ClassId
            && s.LessonNumber == lessonNumber
            && s.SubgroupIndex == candidate.SubgroupIndex);
}
