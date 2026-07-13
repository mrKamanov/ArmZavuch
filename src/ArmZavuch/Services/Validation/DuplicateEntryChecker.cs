using ArmZavuch.Models;

namespace ArmZavuch.Services.Validation;

/// <summary>Поиск дублей в справочниках (до сохранения в БД).</summary>
public static class DuplicateEntryChecker
{
    public static bool EqualsIgnoreCase(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public static Building? FindBuilding(IEnumerable<Building> items, string name, int? excludeId = null) =>
        items.FirstOrDefault(b => EqualsIgnoreCase(b.Name, name) && (excludeId is null || b.Id != excludeId));

    public static Subject? FindSubject(IEnumerable<Subject> items, string name, int? excludeId = null) =>
        items.FirstOrDefault(s => EqualsIgnoreCase(s.Name, name) && (excludeId is null || s.Id != excludeId));

    public static SchoolClass? FindClass(IEnumerable<SchoolClass> items, int grade, string letter, int? excludeId = null)
    {
        var l = letter.Trim();
        return items.FirstOrDefault(c =>
            c.Grade == grade && EqualsIgnoreCase(c.Letter, l) && (excludeId is null || c.Id != excludeId));
    }

    public static Room? FindRoom(IEnumerable<Room> items, string number, int buildingId, int? excludeId = null) =>
        items.FirstOrDefault(r =>
            EqualsIgnoreCase(r.Number, number) && r.BuildingId == buildingId &&
            (excludeId is null || r.Id != excludeId));

    public static Teacher? FindTeacher(IEnumerable<Teacher> items, string fullName, int? excludeId = null) =>
        items.FirstOrDefault(t =>
            EqualsIgnoreCase(t.FullName, fullName) && (excludeId is null || t.Id != excludeId));

    public static CurriculumItem? FindCurriculum(
        IEnumerable<CurriculumItem> items,
        int classId,
        int subjectId,
        string weekParity,
        int? excludeId = null) =>
        items.FirstOrDefault(c =>
            c.ClassId == classId &&
            c.SubjectId == subjectId &&
            string.Equals(c.WeekParity, weekParity, StringComparison.Ordinal) &&
            (excludeId is null || c.Id != excludeId));

    public static BellPeriod? FindBell(
        IEnumerable<BellPeriod> items,
        string templateName,
        int lessonNumber,
        int shift,
        string periodKind,
        int? excludeId = null) =>
        items.FirstOrDefault(b =>
            EqualsIgnoreCase(b.TemplateName, templateName) &&
            b.LessonNumber == lessonNumber &&
            b.Shift == shift &&
            string.Equals(b.PeriodKind, periodKind, StringComparison.Ordinal) &&
            (excludeId is null || b.Id != excludeId));
}
