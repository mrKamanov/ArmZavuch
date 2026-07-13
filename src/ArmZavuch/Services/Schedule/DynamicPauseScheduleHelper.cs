using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Предмет «Динамическая пауза» в нагрузке класса и ячейках сетки.</summary>
public static class DynamicPauseScheduleHelper
{
    public static Subject? FindSubjectForClass(
        int classId,
        IEnumerable<CurriculumItem> curriculum,
        IEnumerable<Subject> subjects)
    {
        var item = curriculum.FirstOrDefault(c =>
            c.ClassId == classId && SubjectScheduleRules.IsDynamicPause(c.SubjectName));
        if (item is not null)
        {
            var byId = subjects.FirstOrDefault(s => s.Id == item.SubjectId);
            if (byId is not null)
                return byId;
        }

        return subjects.FirstOrDefault(s => SubjectScheduleRules.IsDynamicPause(s.Name));
    }

    public static bool TeacherLeadsPauseForClass(Teacher teacher, int classId, Subject pauseSubject) =>
        TeacherRecommendation.IsBoundToClassSubject(teacher, classId, pauseSubject.Id);
}
