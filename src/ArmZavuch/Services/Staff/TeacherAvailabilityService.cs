using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Staff;

/// <summary>Проверка доступности сотрудника: статусы, нерабочее время.</summary>
public sealed class TeacherAvailabilityService
{
    private readonly TeacherStatusRepository _statuses;
    private readonly TeacherUnavailabilityRepository _unavailability;

    public TeacherAvailabilityService(
        TeacherStatusRepository statuses,
        TeacherUnavailabilityRepository unavailability)
    {
        _statuses = statuses;
        _unavailability = unavailability;
    }

    public async Task<TeacherStatusPeriod?> GetBlockingStatusAsync(int teacherId, DateOnly date)
    {
        var active = await _statuses.GetActiveForDateAsync(date);
        return active.FirstOrDefault(s =>
            s.TeacherId == teacherId && StaffStatusTypes.BlocksWork(s.StatusType));
    }

    public async Task<bool> IsAvailableForLessonAsync(int teacherId, DateOnly date, int lessonNumber)
    {
        if (await GetBlockingStatusAsync(teacherId, date) is not null)
            return false;

        var blocks = await _unavailability.GetForTeacherAsync(teacherId);

        foreach (var block in blocks)
        {
            if (!TeacherUnavailabilityResolver.MatchesDate(block, date))
                continue;
            if (block.AllDay || block.LessonFrom is null)
                return false;
            if (lessonNumber >= block.LessonFrom && lessonNumber <= (block.LessonTo ?? block.LessonFrom))
                return false;
        }

        return true;
    }

    /// <summary>Замечания для недельного шаблона (повторяющееся нерабочее время).</summary>
    public async Task<IReadOnlyList<string>> GetTemplateLessonWarningsAsync(
        int teacherId, int dayOfWeek, int lessonNumber, string? teacherName = null)
    {
        var blocks = await _unavailability.GetForTeacherAsync(teacherId);
        return TeacherUnavailabilityCompliance
            .GetTemplateWarnings(blocks, dayOfWeek, lessonNumber, teacherName)
            .ToList();
    }

    public async Task ApplyStaffAbsencesAsync(List<LessonSlot> lessons, DateOnly date)
    {
        var activeStatuses = await _statuses.GetActiveForDateAsync(date);
        foreach (var status in activeStatuses.Where(s => StaffStatusTypes.BlocksWork(s.StatusType)))
        {
            foreach (var lesson in lessons.Where(l => l.TeacherId == status.TeacherId && !l.IsCancelled))
            {
                if (lesson.ReplacementTeacherName is null or "" or "— нужна замена —")
                    lesson.ReplacementTeacherName = "— нужна замена —";
                lesson.AbsenceNote ??= status.AbsenceNoteText;
            }
        }
    }
}
