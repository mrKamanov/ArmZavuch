using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Schedule;

namespace ArmZavuch.Services.Validation;

/// <summary>Сверка нагрузки из справочника и уроков в сетке выбранного шаблона недели (ТЗ §4).</summary>
public sealed class LoadBalanceChecker
{
    private readonly CurriculumRepository _curriculum;
    private readonly WeekTemplateRepository _templates;
    private readonly TeacherRepository _teachers;

    public LoadBalanceChecker(
        CurriculumRepository curriculum,
        WeekTemplateRepository templates,
        TeacherRepository teachers)
    {
        _curriculum = curriculum;
        _templates = templates;
        _teachers = teachers;
    }

    public async Task<List<LoadBalanceRow>> CheckAsync(int templateId)
    {
        var template = await _templates.GetByIdAsync(templateId);
        var templateParity = template?.WeekParity ?? WeekTemplateParity.Any;

        var allPlan = (await _curriculum.GetAllAsync()).ToList();
        var plan = allPlan
            .Where(item => CurriculumWeekParity.MatchesForTemplate(item.WeekParity, templateParity))
            .Where(item => SubjectScheduleRules.CountsForLoadBalance(item.SubjectName))
            .ToList();
        var allSlots = await _templates.GetAllSlotsForTemplateAsync(templateId);
        var slots = allSlots
            .Where(s => SubjectScheduleRules.CountsForLoadBalance(s.SubjectName))
            .ToList();
        var assigneesByCurriculum = await _teachers.GetExplicitAssigneesByCurriculumAsync();

        var rows = new List<LoadBalanceRow>();
        foreach (var item in plan)
        {
            assigneesByCurriculum.TryGetValue(item.Id, out var teacherIds);
            var scheduled = CurriculumScheduledHours.CountForClassSubject(
                slots, item.ClassId, item.SubjectId, item.HasSubgroups, teacherIds);
            var paritySuffix = item.WeekParity == CurriculumWeekParity.EveryWeek
                ? ""
                : $" ({item.WeekParityDisplay})";
            rows.Add(new LoadBalanceRow
            {
                Key = $"{item.ClassName}/{item.SubjectName}/{item.WeekParity}",
                Label = $"{item.ClassName} · {item.SubjectName}{paritySuffix}",
                SubjectName = item.SubjectName,
                ClassId = item.ClassId,
                SubjectId = item.SubjectId,
                PlannedHours = item.HoursPerWeek,
                ScheduledHours = scheduled
            });
        }

        foreach (var extra in slots
                     .GroupBy(s => (s.ClassId, s.SubjectId))
                     .Select(g => g.Key)
                     .Except(plan.Select(p => (p.ClassId, p.SubjectId))))
        {
            var slot = slots.First(s => s.ClassId == extra.ClassId && s.SubjectId == extra.SubjectId);
            var curItem = allPlan
                .FirstOrDefault(p => p.ClassId == extra.ClassId && p.SubjectId == extra.SubjectId);
            var hasSubgroups = curItem?.HasSubgroups ?? false;
            List<int>? teacherIds = null;
            if (curItem is not null && assigneesByCurriculum.TryGetValue(curItem.Id, out var ids))
                teacherIds = ids;
            rows.Add(new LoadBalanceRow
            {
                Key = $"{slot.ClassName}/{slot.SubjectName}/extra",
                Label = $"{slot.ClassName} · {slot.SubjectName}",
                SubjectName = slot.SubjectName,
                ClassId = slot.ClassId,
                SubjectId = slot.SubjectId,
                PlannedHours = 0,
                ScheduledHours = CurriculumScheduledHours.CountForClassSubject(
                    slots, extra.ClassId, extra.SubjectId, hasSubgroups, teacherIds)
            });
        }

        return rows.OrderBy(r => r.Label).ToList();
    }
}
