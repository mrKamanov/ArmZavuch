using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Сборка сеток расписания для Конструктора и сводной простыни.</summary>
public static class ScheduleGridBuilder
{
    public const int DefaultMaxLessons = 8;
    public const int DefaultMaxDays = 6;
    public const int FirstGradeTimelineGrade = 1;

    public static List<ConstructorDayGridSection> BuildDayGridSections(
        IEnumerable<SchoolClass> classes,
        IReadOnlyList<LessonSlot> daySlots,
        IReadOnlyList<BellPeriod> bells,
        int maxLessons = DefaultMaxLessons,
        IReadOnlyDictionary<int, Room>? roomsById = null)
    {
        var classList = classes.OrderBy(c => c.Grade).ThenBy(c => c.Letter).ToList();
        var slotGroups = daySlots
            .GroupBy(s => (s.ClassId, s.LessonNumber))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubgroupIndex).ToList());

        var sections = new List<ConstructorDayGridSection>();
        var firstGradeClasses = classList.Where(c => c.Grade == FirstGradeTimelineGrade).ToList();
        if (firstGradeClasses.Count > 0)
        {
            var shift = firstGradeClasses[0].Shift;
            var templateName = firstGradeClasses
                .Select(c => c.BellTemplateName)
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
            var timeline = BellScheduleResolver.BuildPrimaryTimeline(
                bells, FirstGradeTimelineGrade, shift, templateName: templateName);
            sections.Add(BuildTimelineSection(
                "1 класс · свои звонки и дин. пауза между уроками",
                isFirstGrade: true,
                timeline,
                firstGradeClasses,
                slotGroups,
                roomsById));
        }

        var otherClasses = classList.Where(c => c.Grade != FirstGradeTimelineGrade).ToList();
        if (otherClasses.Count > 0)
        {
            sections.Add(BuildStandardDaySection(
                otherClasses.Count > 0 && firstGradeClasses.Count > 0
                    ? "2–11 классы · стандартная сетка уроков"
                    : "Классы",
                otherClasses,
                slotGroups,
                maxLessons,
                roomsById));
        }

        return sections;
    }

    public static List<ClassGridRow> BuildDayGrid(
        IEnumerable<SchoolClass> classes,
        IReadOnlyList<LessonSlot> daySlots,
        int maxLessons = DefaultMaxLessons,
        IReadOnlyDictionary<int, Room>? roomsById = null)
    {
        var slotGroups = daySlots
            .GroupBy(s => (s.ClassId, s.LessonNumber))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubgroupIndex).ToList());

        var rows = new List<ClassGridRow>();
        foreach (var cls in classes.OrderBy(c => c.Grade).ThenBy(c => c.Letter))
        {
            var row = new ClassGridRow { ClassId = cls.Id, ClassName = cls.DisplayName };
            for (var lesson = 1; lesson <= maxLessons; lesson++)
            {
                row.Lessons.Add(CreateLessonCell(cls, lesson, isDynamicPauseColumn: false, slotGroups, roomsById));
            }

            rows.Add(row);
        }

        return rows;
    }

    public static List<LessonWeekRow> BuildClassWeekGrid(
        SchoolClass cls,
        IReadOnlyList<LessonSlot> weekSlots,
        IReadOnlyList<BellPeriod> bells,
        int maxLessons = DefaultMaxLessons,
        IReadOnlyDictionary<int, Room>? roomsById = null,
        BellTemplateAssignmentSnapshot? assignment = null)
    {
        if (cls.Grade == FirstGradeTimelineGrade)
            return BuildClassWeekTimelineGrid(cls, weekSlots, bells, roomsById, assignment);

        var slotGroups = weekSlots
            .GroupBy(s => (s.DayOfWeek, s.LessonNumber))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubgroupIndex).ToList());

        var rows = new List<LessonWeekRow>();
        for (var lesson = 1; lesson <= maxLessons; lesson++)
        {
            var row = new LessonWeekRow
            {
                LessonNumber = lesson,
                RowTitle = $"Урок {lesson}"
            };
            for (var day = 1; day <= DefaultMaxDays; day++)
            {
                row.Days.Add(CreateWeekCell(cls, day, lesson, isDynamicPauseColumn: false, slotGroups, roomsById));
            }

            rows.Add(row);
        }

        return rows;
    }

    public static List<LessonWeekRow> BuildClassWeekTimelineGrid(
        SchoolClass cls,
        IReadOnlyList<LessonSlot> weekSlots,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyDictionary<int, Room>? roomsById = null,
        BellTemplateAssignmentSnapshot? assignment = null)
    {
        assignment ??= BellTemplateAssignmentSnapshot.Fallback;
        var templateName = assignment.GetTemplateName(cls);
        var templatePeriods = BellScheduleResolver.FilterByTemplate(bells, templateName);
        var bellsScope = templatePeriods.Count > 0 ? templatePeriods : bells;
        var timeline = BellScheduleResolver.BuildPrimaryTimeline(
            bellsScope, cls.Grade, cls.Shift, templateName: templateName);
        var slotGroups = weekSlots
            .GroupBy(s => (s.DayOfWeek, s.LessonNumber))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubgroupIndex).ToList());

        var rows = new List<LessonWeekRow>(timeline.Count);
        foreach (var column in timeline)
        {
            var row = new LessonWeekRow
            {
                LessonNumber = column.StorageLessonNumber,
                IsDynamicPauseColumn = column.IsDynamicPause,
                IsBreakColumn = column.IsBreak,
                RowTitle = column.Title,
                BellTimeDisplay = column.BellTimeDisplay
            };

            for (var day = 1; day <= DefaultMaxDays; day++)
            {
                row.Days.Add(CreateTimelineWeekCell(cls, day, column, timeline, slotGroups, roomsById));
            }

            rows.Add(row);
        }

        return rows;
    }

    private static ConstructorDayGridSection BuildTimelineSection(
        string title,
        bool isFirstGrade,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        IEnumerable<SchoolClass> classes,
        Dictionary<(int ClassId, int LessonNumber), List<LessonSlot>> slotGroups,
        IReadOnlyDictionary<int, Room>? roomsById)
    {
        var section = new ConstructorDayGridSection { Title = title, IsFirstGradeSection = isFirstGrade };
        foreach (var column in timeline)
            section.Columns.Add(column);

        foreach (var cls in classes)
        {
            var row = new ClassGridRow { ClassId = cls.Id, ClassName = cls.DisplayName };
            foreach (var column in timeline)
            {
                row.Lessons.Add(CreateTimelineDayCell(cls, column, timeline, slotGroups, roomsById));
            }

            section.Rows.Add(row);
        }

        return section;
    }

    private static ConstructorDayGridSection BuildStandardDaySection(
        string title,
        IEnumerable<SchoolClass> classes,
        Dictionary<(int ClassId, int LessonNumber), List<LessonSlot>> slotGroups,
        int maxLessons,
        IReadOnlyDictionary<int, Room>? roomsById)
    {
        var section = new ConstructorDayGridSection { Title = title };
        for (var lesson = 1; lesson <= maxLessons; lesson++)
        {
            section.Columns.Add(new ConstructorTimelineColumn
            {
                LessonNumber = lesson,
                StorageLessonNumber = lesson,
                Title = $"Урок {lesson}"
            });
        }

        foreach (var cls in classes)
        {
            var row = new ClassGridRow { ClassId = cls.Id, ClassName = cls.DisplayName };
            for (var lesson = 1; lesson <= maxLessons; lesson++)
            {
                row.Lessons.Add(CreateLessonCell(cls, lesson, isDynamicPauseColumn: false, slotGroups, roomsById));
            }

            section.Rows.Add(row);
        }

        return section;
    }

    public static GridCell CreateLessonCellForClass(
        SchoolClass cls,
        int lessonNumber,
        bool isDynamicPauseColumn,
        Dictionary<(int ClassId, int LessonNumber), List<LessonSlot>> slotGroups,
        IReadOnlyDictionary<int, Room>? roomsById) =>
        CreateLessonCell(cls, lessonNumber, isDynamicPauseColumn, slotGroups, roomsById);

    public static GridCell CreateTimelineCellForClass(
        SchoolClass cls,
        ConstructorTimelineColumn column,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        Dictionary<(int ClassId, int LessonNumber), List<LessonSlot>> slotGroups,
        IReadOnlyDictionary<int, Room>? roomsById,
        string? templateName = null) =>
        CreateTimelineDayCell(cls, column, timeline, slotGroups, roomsById);

    private static GridCell CreateTimelineDayCell(
        SchoolClass cls,
        ConstructorTimelineColumn column,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        Dictionary<(int ClassId, int LessonNumber), List<LessonSlot>> slotGroups,
        IReadOnlyDictionary<int, Room>? roomsById)
    {
        var cell = new GridCell
        {
            ClassId = cls.Id,
            ClassName = cls.DisplayName,
            ClassGrade = cls.Grade,
            LessonNumber = ResolveDefaultStorageLessonNumber(column, timeline),
            IsDynamicPauseColumn = column.IsDynamicPause,
            IsBreakColumn = column.IsBreak
        };

        if (column.IsBreak)
            return cell;

        if (column.IsDynamicPause)
        {
            TryFillPauseCell(cell, cls.Id, column, slotGroups, roomsById);
            return cell;
        }

        TryFillLessonCell(cell, cls.Id, column, timeline, slotGroups, roomsById);
        return cell;
    }

    private static GridCell CreateTimelineWeekCell(
        SchoolClass cls,
        int day,
        ConstructorTimelineColumn column,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        Dictionary<(int DayOfWeek, int LessonNumber), List<LessonSlot>> slotGroups,
        IReadOnlyDictionary<int, Room>? roomsById)
    {
        var cell = new GridCell
        {
            ClassId = cls.Id,
            ClassName = cls.DisplayName,
            ClassGrade = cls.Grade,
            LessonNumber = ResolveDefaultStorageLessonNumber(column, timeline),
            DayOfWeek = day,
            IsDynamicPauseColumn = column.IsDynamicPause,
            IsBreakColumn = column.IsBreak
        };

        if (column.IsBreak)
            return cell;

        if (column.IsDynamicPause)
        {
            TryFillPauseWeekCell(cell, cls.Id, day, column, slotGroups, roomsById);
            return cell;
        }

        TryFillLessonWeekCell(cell, cls.Id, day, column, timeline, slotGroups, roomsById);
        return cell;
    }

    private static void TryFillLessonCell(
        GridCell cell,
        int classId,
        ConstructorTimelineColumn column,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        Dictionary<(int ClassId, int LessonNumber), List<LessonSlot>> slotGroups,
        IReadOnlyDictionary<int, Room>? roomsById)
    {
        foreach (var lessonNumber in ResolveLessonStorageNumbers(column, timeline))
        {
            if (!slotGroups.TryGetValue((classId, lessonNumber), out var group))
                continue;

            var regular = group.Where(s => !SubjectScheduleRules.IsDynamicPause(s.SubjectName)).ToList();
            if (regular.Count == 0)
                continue;

            cell.LessonNumber = lessonNumber;
            AddPartsFromSlots(cell, regular, roomsById);
            return;
        }
    }

    private static void TryFillLessonWeekCell(
        GridCell cell,
        int classId,
        int day,
        ConstructorTimelineColumn column,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        Dictionary<(int DayOfWeek, int LessonNumber), List<LessonSlot>> slotGroups,
        IReadOnlyDictionary<int, Room>? roomsById)
    {
        foreach (var lessonNumber in ResolveLessonStorageNumbers(column, timeline))
        {
            if (!slotGroups.TryGetValue((day, lessonNumber), out var group))
                continue;

            var regular = group
                .Where(s => s.ClassId == classId && !SubjectScheduleRules.IsDynamicPause(s.SubjectName))
                .ToList();
            if (regular.Count == 0)
                continue;

            cell.LessonNumber = lessonNumber;
            AddPartsFromSlots(cell, regular, roomsById);
            return;
        }
    }

    public static HashSet<int> CollectTimelineLessonStorageNumbers(
        IReadOnlyList<ConstructorTimelineColumn> timeline) =>
        timeline
            .Where(c => !c.IsDynamicPause && !c.IsBreak)
            .SelectMany(c => ResolveLessonStorageNumbers(c, timeline))
            .ToHashSet();

    public static IEnumerable<int> ResolveLessonStorageNumbers(
        ConstructorTimelineColumn column,
        IReadOnlyList<ConstructorTimelineColumn> timeline)
    {
        if (column.IsDynamicPause)
        {
            yield return column.AfterLessonNumber + 1;
            yield break;
        }

        if (column.IsBreak)
            yield break;

        if (column.StorageLessonNumber > 0 && column.StorageLessonNumber != column.LessonNumber)
        {
            yield return column.StorageLessonNumber;
            yield break;
        }

        var storage = ResolveTimelineStorageLessonNumber(column, timeline);
        yield return storage;

        // До исправления урок после паузы мог сохраниться под номером паузы.
        if (IsLessonAfterDynamicPause(timeline, column))
        {
            var legacy = column.LessonNumber;
            if (legacy != storage)
                yield return legacy;
        }
    }

    /// <summary>Номер слота в БД для ячейки timeline с учётом всех дин. пауз выше.</summary>
    public static int ResolveTimelineStorageLessonNumber(
        ConstructorTimelineColumn column,
        IReadOnlyList<ConstructorTimelineColumn> timeline)
    {
        if (column.IsDynamicPause)
            return column.StorageLessonNumber > 0
                ? column.StorageLessonNumber
                : column.AfterLessonNumber + 1;

        if (column.IsBreak)
            return column.LessonNumber;

        if (column.StorageLessonNumber > 0)
            return column.StorageLessonNumber;

        var pauseCount = 0;
        foreach (var c in timeline)
        {
            if (c.IsBreak)
                continue;

            if (!c.IsDynamicPause && c.LessonNumber == column.LessonNumber)
                return column.LessonNumber + pauseCount;

            if (c.IsDynamicPause)
                pauseCount++;
        }

        return column.LessonNumber;
    }

    public static BellPeriod? ResolvePeriodForSlot(
        IReadOnlyList<BellPeriod> bells,
        LessonSlot slot,
        string? overrideTemplateName = null)
    {
        var lookupGrade = slot.ClassGrade == FirstGradeTimelineGrade
            ? FirstGradeTimelineGrade
            : BellScheduleResolver.StandardBellLookupGrade;
        IReadOnlyList<BellPeriod> bellsScope;
        if (slot.ClassGrade == FirstGradeTimelineGrade)
        {
            var filtered = BellScheduleResolver.FilterByTemplate(bells, overrideTemplateName);
            bellsScope = filtered.Count > 0
                ? filtered
                : bells.Where(p => p.Shift == slot.ClassShift && p.MatchesGrade(lookupGrade)).ToList();
        }
        else
        {
            bellsScope = BellScheduleResolver.ResolveShiftBellPeriods(bells, slot.ClassShift, overrideTemplateName);
        }

        if (SubjectScheduleRules.IsDynamicPause(slot.SubjectName))
        {
            var afterLesson = ResolveDynamicPauseAfterLessonNumber(bellsScope, slot);
            return BellScheduleResolver.FindDynamicPauseAfterLesson(
                bellsScope, lookupGrade, slot.ClassShift, afterLesson);
        }

        var bellLessonNumber = ResolveBellLessonNumberForSlot(bellsScope, slot);
        return BellScheduleResolver.FindLessonPeriod(
            bellsScope, lookupGrade, slot.ClassShift, bellLessonNumber, overrideTemplateName);
    }

    /// <summary>Номер урока в шаблоне звонков (слот БД), не путать с отображаемым «Урок N» после дин. паузы.</summary>
    public static int ResolveBellLessonNumberForSlot(IReadOnlyList<BellPeriod> bells, LessonSlot slot)
    {
        if (SubjectScheduleRules.IsDynamicPause(slot.SubjectName))
            return slot.LessonNumber;

        var templateName = slot.BellTemplateName;
        var timeline = BellScheduleResolver.BuildPrimaryTimeline(
            bells, slot.ClassGrade, slot.ClassShift, templateName: templateName);
        if (!timeline.Any(c => c.IsDynamicPause))
            return slot.LessonNumber;

        foreach (var column in timeline)
        {
            if (column.IsDynamicPause || column.IsBreak)
                continue;

            if (ResolveTimelineStorageLessonNumber(column, timeline) == slot.LessonNumber)
                return ResolveColumnBellLessonNumber(column);
        }

        return slot.LessonNumber;
    }

    public static int ResolveColumnBellLessonNumber(ConstructorTimelineColumn column) =>
        column.BellLessonNumber > 0 ? column.BellLessonNumber : column.LessonNumber;

    public static int ResolveLogicalLessonNumber(IReadOnlyList<BellPeriod> bells, LessonSlot slot)
        => ResolveLogicalLessonNumber(bells, slot, slot.BellTemplateName);

    public static int ResolveLogicalLessonNumber(
        IReadOnlyList<BellPeriod> bells,
        LessonSlot slot,
        string? templateName)
    {
        var timeline = BellScheduleResolver.BuildPrimaryTimeline(
            bells, slot.ClassGrade, slot.ClassShift, templateName: templateName);
        if (!timeline.Any(c => c.IsDynamicPause))
            return slot.LessonNumber;

        foreach (var column in timeline)
        {
            if (column.IsDynamicPause || column.IsBreak)
                continue;

            foreach (var storage in ResolveLessonStorageNumbers(column, timeline))
            {
                if (storage == slot.LessonNumber)
                    return column.LessonNumber;
            }
        }

        return slot.LessonNumber;
    }

    private static int ResolveDynamicPauseAfterLessonNumber(IReadOnlyList<BellPeriod> bells, LessonSlot slot)
    {
        var templateName = slot.BellTemplateName;
        var timeline = BellScheduleResolver.BuildPrimaryTimeline(
            bells, slot.ClassGrade, slot.ClassShift, templateName: templateName);
        foreach (var column in timeline.Where(c => c.IsDynamicPause))
        {
            if (ScheduleGridBuilder.ResolveTimelineStorageLessonNumber(column, timeline) == slot.LessonNumber)
                return column.AfterLessonNumber;
        }

        return Math.Max(1, slot.LessonNumber - 1);
    }

    /// <summary>Номер слота в БД для пустой ячейки timeline.</summary>
    public static int ResolveDefaultStorageLessonNumber(
        ConstructorTimelineColumn column,
        IReadOnlyList<ConstructorTimelineColumn> timeline)
        => ResolveTimelineStorageLessonNumber(column, timeline);

    private static bool IsLessonAfterDynamicPause(
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        ConstructorTimelineColumn column)
    {
        if (column.IsDynamicPause)
            return false;

        var index = -1;
        for (var i = 0; i < timeline.Count; i++)
        {
            if (timeline[i].IsDynamicPause || timeline[i].LessonNumber != column.LessonNumber)
                continue;
            index = i;
            break;
        }

        return index > 0 && timeline[index - 1].IsDynamicPause;
    }

    private static GridCell CreateLessonCell(
        SchoolClass cls,
        int lessonNumber,
        bool isDynamicPauseColumn,
        Dictionary<(int ClassId, int LessonNumber), List<LessonSlot>> slotGroups,
        IReadOnlyDictionary<int, Room>? roomsById)
    {
        var cell = new GridCell
        {
            ClassId = cls.Id,
            ClassName = cls.DisplayName,
            ClassGrade = cls.Grade,
            LessonNumber = lessonNumber,
            IsDynamicPauseColumn = isDynamicPauseColumn
        };

        if (slotGroups.TryGetValue((cls.Id, lessonNumber), out var group))
            AddPartsFromSlots(cell, group, roomsById);

        return cell;
    }

    private static GridCell CreateWeekCell(
        SchoolClass cls,
        int day,
        int lessonNumber,
        bool isDynamicPauseColumn,
        Dictionary<(int DayOfWeek, int LessonNumber), List<LessonSlot>> slotGroups,
        IReadOnlyDictionary<int, Room>? roomsById)
    {
        var cell = new GridCell
        {
            ClassId = cls.Id,
            ClassName = cls.DisplayName,
            ClassGrade = cls.Grade,
            LessonNumber = lessonNumber,
            DayOfWeek = day,
            IsDynamicPauseColumn = isDynamicPauseColumn
        };

        if (slotGroups.TryGetValue((day, lessonNumber), out var group))
            AddPartsFromSlots(cell, group, roomsById);

        return cell;
    }

    private static void TryFillPauseCell(
        GridCell cell,
        int classId,
        ConstructorTimelineColumn column,
        Dictionary<(int ClassId, int LessonNumber), List<LessonSlot>> slotGroups,
        IReadOnlyDictionary<int, Room>? roomsById)
    {
        if (!slotGroups.TryGetValue((classId, column.AfterLessonNumber + 1), out var group))
            return;

        var pauseSlots = group.Where(s => SubjectScheduleRules.IsDynamicPause(s.SubjectName)).ToList();
        if (pauseSlots.Count == 0)
            return;

        cell.LessonNumber = column.AfterLessonNumber + 1;
        AddPartsFromSlots(cell, pauseSlots, roomsById);
    }

    private static void TryFillPauseWeekCell(
        GridCell cell,
        int classId,
        int day,
        ConstructorTimelineColumn column,
        Dictionary<(int DayOfWeek, int LessonNumber), List<LessonSlot>> slotGroups,
        IReadOnlyDictionary<int, Room>? roomsById)
    {
        if (!slotGroups.TryGetValue((day, column.AfterLessonNumber + 1), out var group))
            return;

        var pauseSlots = group.Where(s => s.ClassId == classId && SubjectScheduleRules.IsDynamicPause(s.SubjectName)).ToList();
        if (pauseSlots.Count == 0)
            return;

        cell.LessonNumber = column.AfterLessonNumber + 1;
        AddPartsFromSlots(cell, pauseSlots, roomsById);
    }

    private static void AddPartsFromSlots(
        GridCell cell,
        IEnumerable<LessonSlot> slots,
        IReadOnlyDictionary<int, Room>? roomsById)
    {
        foreach (var slot in slots)
        {
            cell.Parts.Add(new SubgroupPart
            {
                SubgroupIndex = slot.SubgroupIndex,
                SlotId = slot.SlotId,
                SubjectId = slot.SubjectId,
                TeacherId = slot.TeacherId,
                RoomId = slot.RoomId > 0 ? slot.RoomId : null,
                Line = FormatSlotLine(slot),
                SubjectName = slot.SubjectName,
                TeacherName = slot.TeacherName,
                RoomLine = FormatRoomLine(slot),
                RoomBuildingColorHex = ResolveRoomColor(slot.RoomId, roomsById),
                IsAnchored = slot.IsAnchored
            });
        }

        if (cell.Parts.Count > 1 || cell.Parts.Any(p => p.SubgroupIndex > 0))
        {
            foreach (var part in cell.Parts)
                part.SubgroupLabel = $"[{part.SubgroupIndex + 1}]";
        }
    }

    private static string FormatRoomLine(LessonSlot slot) =>
        slot.RoomId > 0 && !string.IsNullOrWhiteSpace(slot.RoomNumber)
            ? $"каб.{slot.RoomNumber}"
            : SubjectScheduleRules.IsDynamicPause(slot.SubjectName) ? "прогулка" : "без каб.";

    private static string ResolveRoomColor(int roomId, IReadOnlyDictionary<int, Room>? roomsById)
    {
        if (roomId > 0 && roomsById is not null && roomsById.TryGetValue(roomId, out var room))
            return room.BuildingColorHex;
        return "#94A3B8";
    }

    private static string FormatSlotLine(LessonSlot slot)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(slot.SubjectName))
            lines.Add(slot.SubjectName);
        if (!string.IsNullOrWhiteSpace(slot.TeacherName))
            lines.Add(slot.TeacherName);
        var roomLine = FormatRoomLine(slot);
        if (!string.IsNullOrWhiteSpace(roomLine) && roomLine != "без каб.")
            lines.Add(roomLine);
        return lines.Count == 0 ? "" : string.Join("\n", lines);
    }

    public static List<LessonSlot>? ResolveClassTimelineSlots(
        int classId,
        int dayOfWeek,
        ConstructorTimelineColumn column,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        IReadOnlyDictionary<(int ClassId, int DayOfWeek, int LessonNumber), List<LessonSlot>> lookup)
    {
        if (column.IsDynamicPause)
        {
            if (!lookup.TryGetValue((classId, dayOfWeek, column.AfterLessonNumber + 1), out var group))
                return null;

            var pauseSlots = group.Where(s => SubjectScheduleRules.IsDynamicPause(s.SubjectName)).ToList();
            return pauseSlots.Count > 0 ? pauseSlots : null;
        }

        foreach (var lessonNumber in ResolveLessonStorageNumbers(column, timeline))
        {
            if (!lookup.TryGetValue((classId, dayOfWeek, lessonNumber), out var group))
                continue;

            var regular = group.Where(s => !SubjectScheduleRules.IsDynamicPause(s.SubjectName)).ToList();
            if (regular.Count > 0)
                return regular;
        }

        return null;
    }

    public static List<LessonSlot>? ResolveTeacherTimelineSlots(
        int teacherId,
        int dayOfWeek,
        ConstructorTimelineColumn column,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        IReadOnlyDictionary<(int TeacherId, int DayOfWeek, int LessonNumber), List<LessonSlot>> lookup)
    {
        if (column.IsDynamicPause)
        {
            if (!lookup.TryGetValue((teacherId, dayOfWeek, column.AfterLessonNumber + 1), out var group))
                return null;

            var pauseSlots = group.Where(s => SubjectScheduleRules.IsDynamicPause(s.SubjectName)).ToList();
            return pauseSlots.Count > 0 ? pauseSlots : null;
        }

        foreach (var lessonNumber in ResolveLessonStorageNumbers(column, timeline))
        {
            if (!lookup.TryGetValue((teacherId, dayOfWeek, lessonNumber), out var group))
                continue;

            var regular = group.Where(s => !SubjectScheduleRules.IsDynamicPause(s.SubjectName)).ToList();
            if (regular.Count > 0)
                return regular;
        }

        return null;
    }

    public static List<LessonSlot>? ResolveRoomTimelineSlots(
        int roomId,
        int dayOfWeek,
        int classShift,
        ConstructorTimelineColumn column,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        IReadOnlyDictionary<(int RoomId, int DayOfWeek, int LessonNumber, int ClassShift), List<LessonSlot>> lookup)
    {
        if (column.IsDynamicPause)
        {
            if (!lookup.TryGetValue((roomId, dayOfWeek, column.AfterLessonNumber + 1, classShift), out var group))
                return null;

            var pauseSlots = group.Where(s => SubjectScheduleRules.IsDynamicPause(s.SubjectName)).ToList();
            return pauseSlots.Count > 0 ? pauseSlots : null;
        }

        foreach (var lessonNumber in ResolveLessonStorageNumbers(column, timeline))
        {
            if (!lookup.TryGetValue((roomId, dayOfWeek, lessonNumber, classShift), out var group))
                continue;

            var regular = group.Where(s => !SubjectScheduleRules.IsDynamicPause(s.SubjectName)).ToList();
            if (regular.Count > 0)
                return regular;
        }

        return null;
    }

    public static string FormatOverviewRoomLine(LessonSlot slot) => FormatRoomLine(slot);

    public static string ResolveOverviewRoomColor(int roomId, IReadOnlyDictionary<int, Room>? roomsById) =>
        ResolveRoomColor(roomId, roomsById);
}
