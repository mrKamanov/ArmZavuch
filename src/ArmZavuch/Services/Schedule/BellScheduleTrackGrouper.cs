using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Группировка классов по дорожке звонков для сеток конструктора, сводки и диспетчерской.</summary>
public static class BellScheduleTrackGrouper
{
    public sealed record BellScheduleTrack(
        string TemplateName,
        int Shift,
        bool IsCustom,
        IReadOnlyList<SchoolClass> Classes);

    public static IEnumerable<BellScheduleTrack> GroupTracks(
        IEnumerable<SchoolClass> classes,
        BellTemplateAssignmentSnapshot assignment)
    {
        return classes
            .GroupBy(c => (c.Shift, assignment.GetTemplateName(c)))
            .OrderBy(g => g.Key.Shift)
            .ThenBy(g => g.Any(c => assignment.IsCustomClass(c)) ? 1 : 0)
            .ThenBy(g => g.Key.Item2, StringComparer.OrdinalIgnoreCase)
            .Select(g => new BellScheduleTrack(
                g.Key.Item2,
                g.Key.Shift,
                g.Any(c => assignment.IsCustomClass(c)),
                g.OrderBy(c => c.Grade).ThenBy(c => c.Letter, StringComparer.OrdinalIgnoreCase).ToList()));
    }

    public static string BuildTitle(BellScheduleTrack track)
    {
        var template = BellTemplateNaming.ToDisplay(track.TemplateName);
        if (track.IsCustom)
        {
            var names = string.Join(", ", track.Classes.Select(c => c.DisplayName));
            return $"Отдельный режим · {names} · «{template}»";
        }

        if (track.Classes.All(c => c.Grade == 1))
            return $"1 класс · «{template}»";

        return track.Shift == 2
            ? $"2–11 классы · 2 смена · «{template}»"
            : $"2–11 классы · 1 смена · «{template}»";
    }

    public static int ResolveProfileGrade(BellScheduleTrack track) =>
        track.Classes.All(c => c.Grade == 1)
            ? ScheduleGridBuilder.FirstGradeTimelineGrade
            : track.Classes.Min(c => c.Grade);

    public static bool UsePrimaryTimeline(IReadOnlyList<BellPeriod> templatePeriods, BellScheduleTrack track)
    {
        if (track.Classes.All(c => c.Grade == 1))
            return true;

        var grade = ResolveProfileGrade(track);
        return BellScheduleResolver.GetDynamicPausesForGrade(templatePeriods, grade, track.Shift).Count > 0;
    }

    public sealed record LessonBellTrack(
        string TemplateName,
        int Shift,
        bool IsCustom,
        IReadOnlyList<LessonSlot> Lessons);

    public static IEnumerable<LessonBellTrack> GroupLessonTracks(
        IEnumerable<LessonSlot> lessons,
        BellTemplateAssignmentSnapshot assignment)
    {
        return lessons
            .GroupBy(l => (l.ClassShift, assignment.GetTemplateName(l)))
            .OrderBy(g => g.Key.ClassShift)
            .ThenBy(g => g.Any(l => assignment.CustomClassIds.Contains(l.ClassId)) ? 1 : 0)
            .ThenBy(g => g.Key.Item2, StringComparer.OrdinalIgnoreCase)
            .Select(g => new LessonBellTrack(
                g.Key.Item2,
                g.Key.ClassShift,
                g.Any(l => assignment.CustomClassIds.Contains(l.ClassId)),
                g.OrderBy(l => l.ClassGrade).ThenBy(l => l.ClassName, StringComparer.OrdinalIgnoreCase).ToList()));
    }

    public static string BuildLessonTrackTitle(LessonBellTrack track)
    {
        var template = BellTemplateNaming.ToDisplay(track.TemplateName);
        if (track.IsCustom)
        {
            var names = string.Join(", ", track.Lessons.Select(l => l.ClassName).Distinct());
            return $"Отдельный режим · {names} · «{template}»";
        }

        if (track.Lessons.All(l => l.ClassGrade == 1))
            return $"1 класс · «{template}»";

        return track.Shift == 2
            ? $"2–11 классы · 2 смена · «{template}»"
            : $"2–11 классы · 1 смена · «{template}»";
    }
}
