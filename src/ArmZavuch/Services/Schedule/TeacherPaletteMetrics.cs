using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Нагрузка и счётчик ячеек педагога для палитры конструктора.</summary>
public static class TeacherPaletteMetrics
{
    public const string UnassignedSubjectGroup = "Без основного предмета";

    public static string ResolveSubjectGroup(Teacher teacher) =>
        string.IsNullOrWhiteSpace(teacher.PrimarySubject)
            ? UnassignedSubjectGroup
            : teacher.PrimarySubject.Trim();

    public static (int Hours, bool FromCurriculum) ResolvePlannedHours(
        Teacher teacher,
        IReadOnlyDictionary<int, double> plannedByTeacherId)
    {
        if (plannedByTeacherId.TryGetValue(teacher.Id, out var fromLinks) && fromLinks > 0.01)
            return ((int)Math.Round(fromLinks), true);

        return (0, false);
    }

    public static int CountScheduledSlots(IReadOnlyList<LessonSlot> slots, int teacherId) =>
        slots.Count(s => s.TeacherId == teacherId);

    public static bool IsUnassigned(Teacher teacher) =>
        string.IsNullOrWhiteSpace(teacher.PrimarySubject);

    public static bool MatchesTypeFilter(
        Teacher teacher,
        bool showPrimary,
        bool showSubject,
        bool showAuxiliary,
        bool showUnassigned)
    {
        if (!showPrimary && !showSubject && !showAuxiliary && !showUnassigned)
            return true;

        if (IsUnassigned(teacher))
            return showUnassigned;

        return teacher.TeacherType switch
        {
            TeacherTypes.Primary => showPrimary,
            TeacherTypes.Auxiliary => showAuxiliary,
            _ => showSubject
        };
    }

    public static string ResolveScheduledCountBrush(int scheduled, int planned) =>
        planned <= 0
            ? "#64748B"
            : scheduled > planned
                ? "#DC2626"
                : scheduled == planned
                    ? "#16A34A"
                    : "#CA8A04";
}
