using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Поиск строк нагрузки по классу, предмету и чётности шаблона.</summary>
public static class TeacherCurriculumMatcher
{
    public static IEnumerable<CurriculumItem> MatchingItems(
        IEnumerable<CurriculumItem> curriculum,
        int classId,
        int subjectId,
        string templateWeekParity) =>
        curriculum.Where(item =>
            item.ClassId == classId
            && item.SubjectId == subjectId
            && CurriculumWeekParity.MatchesForTemplate(item.WeekParity, templateWeekParity));

    public static bool TeacherHasAssignment(
        Teacher teacher,
        int classId,
        int subjectId,
        string? templateWeekParity = null) =>
        teacher.CurriculumAssignments?.Any(a =>
            a.ClassId == classId
            && a.SubjectId == subjectId
            && (templateWeekParity is null
                || CurriculumWeekParity.MatchesForTemplate(a.WeekParity, templateWeekParity))) == true;
}
