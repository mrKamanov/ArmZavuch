using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Сборка сводной простыни: дни — колонки, уроки — вертикальная шкала со временем звонков.</summary>
public static class OverviewScheduleBuilder
{
    public static (List<OverviewColumnHeader> DayHeaders, List<OverviewMatrixRow> Rows, string RowHeader) Build(
        string mode,
        IReadOnlyList<LessonSlot> slots,
        IReadOnlyList<SchoolClass> classes,
        IReadOnlyList<Teacher> teachers,
        IReadOnlyList<Room> rooms,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyList<string> dayNames,
        OverviewFilter filter,
        int maxLessons = ScheduleGridBuilder.DefaultMaxLessons,
        int maxDays = ScheduleGridBuilder.DefaultMaxDays,
        bool showBreaks = false,
        BellTemplateAssignmentSnapshot? assignment = null)
    {
        var dayHeaders = BuildDayHeaders(dayNames, maxDays);
        var filteredSlots = ApplySlotFilter(slots, filter);
        var roomsById = rooms.ToDictionary(r => r.Id);

        var rows = mode switch
        {
            OverviewViewModes.Teachers => BuildTeacherRows(
                dayHeaders, filteredSlots, bells, teachers, roomsById, filter, maxLessons, showBreaks),
            OverviewViewModes.Buildings => BuildRoomRows(
                dayHeaders, filteredSlots, bells, rooms, roomsById, filter, maxLessons, showBreaks),
            OverviewViewModes.Classes => BuildClassRows(
                dayHeaders, filteredSlots, bells, classes, roomsById, filter, maxLessons, showBreaks, assignment),
            _ => BuildClassRows(
                dayHeaders, filteredSlots, bells, classes, roomsById, filter, maxLessons, showBreaks, assignment)
        };
        ApplyUniformHeights(rows);
        EnsureLeadingDayHeaderRow(rows);

        var rowHeader = mode switch
        {
            OverviewViewModes.Teachers => "Педагог",
            OverviewViewModes.Buildings => "Кабинет",
            _ => "Класс"
        };
        return (dayHeaders, rows, rowHeader);
    }

    private static void ApplyUniformHeights(List<OverviewMatrixRow> rows)
    {
        var dataRows = rows.Where(r => !r.IsSectionHeader && !r.IsDayHeaderRow).ToList();
        if (dataRows.Count == 0)
            return;

        foreach (var rowGroup in dataRows.GroupBy(r => r.Days.FirstOrDefault()?.Lessons.Count ?? 0))
        {
            var lessonCount = rowGroup.Key;
            if (lessonCount <= 0)
                continue;

            for (var lessonIdx = 0; lessonIdx < lessonCount; lessonIdx++)
            {
                var height = rowGroup
                    .SelectMany(r => r.Days)
                    .Where(d => d.Lessons.Count > lessonIdx)
                    .Select(d => d.Lessons[lessonIdx])
                    .Select(OverviewTimelineLayoutCalculator.EstimateContentHeight)
                    .DefaultIfEmpty(OverviewTimelineCellHeights.Min)
                    .Max();

                foreach (var row in rowGroup)
                {
                    foreach (var day in row.Days.Where(d => d.Lessons.Count > lessonIdx))
                        day.Lessons[lessonIdx].CellHeight = height;
                }
            }
        }
    }

    private static List<OverviewColumnHeader> BuildDayHeaders(IReadOnlyList<string> dayNames, int maxDays)
    {
        var headers = new List<OverviewColumnHeader>();
        for (var day = 1; day <= maxDays; day++)
        {
            var dayName = day - 1 < dayNames.Count ? dayNames[day - 1] : $"Д{day}";
            headers.Add(new OverviewColumnHeader { Label = dayName, DayOfWeek = day });
        }
        return headers;
    }

    private static IReadOnlyList<LessonSlot> ApplySlotFilter(IReadOnlyList<LessonSlot> slots, OverviewFilter filter)
    {
        if (!filter.IsActive)
            return slots;

        return slots.Where(s =>
            (filter.ClassId is null || s.ClassId == filter.ClassId)
            && (filter.TeacherId is null || s.TeacherId == filter.TeacherId)
            && (filter.RoomId is null || s.RoomId == filter.RoomId)
            && (string.IsNullOrWhiteSpace(filter.BuildingName)
                || s.BuildingName.Equals(filter.BuildingName, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    private static List<OverviewMatrixRow> BuildClassRows(
        List<OverviewColumnHeader> dayHeaders,
        IReadOnlyList<LessonSlot> slots,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyList<SchoolClass> classes,
        IReadOnlyDictionary<int, Room> roomsById,
        OverviewFilter filter,
        int maxLessons,
        bool showBreaks,
        BellTemplateAssignmentSnapshot? assignment = null)
    {
        assignment ??= BellTemplateAssignmentSnapshot.Fallback;
        var lookup = slots
            .GroupBy(s => (s.ClassId, s.DayOfWeek, s.LessonNumber))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubgroupIndex).ToList());

        IEnumerable<SchoolClass> source = classes.OrderBy(c => c.Shift).ThenBy(c => c.Grade).ThenBy(c => c.Letter);
        if (filter.ClassId is int classId)
            source = source.Where(c => c.Id == classId);

        var rows = new List<OverviewMatrixRow>();
        foreach (var shiftGroup in source.GroupBy(c => c.Shift).OrderBy(g => g.Key))
        {
            if (filter.ClassId is null)
                rows.Add(CreateSectionHeader($"{shiftGroup.Key} смена", showDayHeadersBelow: true));

            foreach (var track in BellScheduleTrackGrouper.GroupTracks(shiftGroup, assignment))
            {
                if (filter.ClassId is null)
                {
                    rows.Add(CreateSectionHeader(
                        BellScheduleTrackGrouper.BuildTitle(track),
                        showDayHeadersBelow: true));
                }

                var templatePeriods = BellScheduleResolver.FilterByTemplate(bells, track.TemplateName);
                var usePrimary = BellScheduleTrackGrouper.UsePrimaryTimeline(templatePeriods, track);

                var classList = track.Classes.ToList();
                for (var classIndex = 0; classIndex < classList.Count; classIndex++)
                {
                    var cls = classList[classIndex];
                    var row = new OverviewMatrixRow
                    {
                        RowLabel = cls.DisplayName,
                        RowSubLabel = $"{shiftGroup.Key} смена",
                        RowKind = OverviewRowKinds.Class,
                        EntityId = cls.Id,
                        ClassShift = cls.Shift,
                        HasRowSeparatorBelow = true,
                        IsTrackGroupEnd = classIndex == classList.Count - 1
                    };

                    if (usePrimary)
                        FillClassTimelineRow(row, dayHeaders, lookup, cls, templatePeriods, track.TemplateName, roomsById, showBreaks);
                    else
                        FillClassStandardRow(row, dayHeaders, lookup, cls, templatePeriods, track.TemplateName, maxLessons, roomsById, showBreaks);

                    rows.Add(row);
                }
            }
        }

        return rows;
    }

    private static List<OverviewMatrixRow> BuildTeacherRows(
        List<OverviewColumnHeader> dayHeaders,
        IReadOnlyList<LessonSlot> slots,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyList<Teacher> teachers,
        IReadOnlyDictionary<int, Room> roomsById,
        OverviewFilter filter,
        int maxLessons,
        bool showBreaks)
    {
        var lookup = slots
            .GroupBy(s => (s.TeacherId, s.DayOfWeek, s.LessonNumber))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubgroupIndex).ToList());

        var activeIds = slots.Select(s => s.TeacherId).Distinct().ToHashSet();
        IEnumerable<Teacher> source = teachers
            .Where(t => activeIds.Contains(t.Id))
            .OrderBy(t => t.FullName);
        if (filter.TeacherId is int teacherId)
            source = source.Where(t => t.Id == teacherId);

        var rows = new List<OverviewMatrixRow>();
        var teacherList = source.ToList();
        var primaryTeachers = teacherList
            .Where(t => TeacherUsesPrimaryTimeline(t.Id, slots, bells))
            .ToList();
        var standardTeachers = teacherList
            .Where(t => !TeacherUsesPrimaryTimeline(t.Id, slots, bells))
            .ToList();

        if (primaryTeachers.Count > 0)
        {
            if (filter.TeacherId is null && standardTeachers.Count > 0)
            {
                rows.Add(CreateSectionHeader(
                    "Начальная школа · звонки и дин. пауза между уроками",
                    showDayHeadersBelow: true));
            }

            for (var teacherIndex = 0; teacherIndex < primaryTeachers.Count; teacherIndex++)
            {
                var teacher = primaryTeachers[teacherIndex];
                var row = new OverviewMatrixRow
                {
                    RowLabel = teacher.FullName,
                    RowSubLabel = teacher.PrimarySubject,
                    RowKind = OverviewRowKinds.Teacher,
                    EntityId = teacher.Id,
                    HasRowSeparatorBelow = true,
                    IsTrackGroupEnd = teacherIndex == primaryTeachers.Count - 1
                        && standardTeachers.Count == 0
                };
                FillTeacherTimelineRow(row, dayHeaders, lookup, teacher.Id, slots, bells, maxLessons, roomsById, showBreaks);
                rows.Add(row);
            }
        }

        if (standardTeachers.Count > 0)
        {
            if (filter.TeacherId is null && primaryTeachers.Count > 0)
            {
                rows.Add(CreateSectionHeader(
                    "Остальные педагоги · стандартная сетка уроков",
                    showDayHeadersBelow: true));
            }

            for (var teacherIndex = 0; teacherIndex < standardTeachers.Count; teacherIndex++)
            {
                var teacher = standardTeachers[teacherIndex];
                var row = new OverviewMatrixRow
                {
                    RowLabel = teacher.FullName,
                    RowSubLabel = teacher.PrimarySubject,
                    RowKind = OverviewRowKinds.Teacher,
                    EntityId = teacher.Id,
                    HasRowSeparatorBelow = true,
                    IsTrackGroupEnd = teacherIndex == standardTeachers.Count - 1
                };
                FillTeacherStandardRow(row, dayHeaders, lookup, teacher.Id, bells, maxLessons, roomsById, showBreaks);
                rows.Add(row);
            }
        }

        return rows;
    }

    private static bool TeacherUsesPrimaryTimeline(
        int teacherId,
        IReadOnlyList<LessonSlot> slots,
        IReadOnlyList<BellPeriod> bells)
    {
        var teacherSlots = slots.Where(s => s.TeacherId == teacherId).ToList();
        if (teacherSlots.Any(s => SubjectScheduleRules.IsDynamicPause(s.SubjectName)))
            return true;

        return teacherSlots.Any(s => s.ClassGrade is >= 1 and <= 4
            && BellScheduleResolver.GetDynamicPausesForGrade(bells, s.ClassGrade, s.ClassShift).Count > 0);
    }

    private static (int Grade, int Shift) ResolveTeacherTimelineProfile(IEnumerable<LessonSlot> teacherSlots)
    {
        var primary = teacherSlots.Where(s => s.ClassGrade is >= 1 and <= 4).ToList();
        if (primary.Count == 0)
            primary = teacherSlots.ToList();

        var grade = primary.Any(s => s.ClassGrade == ScheduleGridBuilder.FirstGradeTimelineGrade)
            ? ScheduleGridBuilder.FirstGradeTimelineGrade
            : primary.Where(s => s.ClassGrade is >= 1 and <= 4).Select(s => s.ClassGrade).DefaultIfEmpty(1).Min();

        var shift = primary
            .GroupBy(s => s.ClassShift)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First().Key;

        return (grade, shift);
    }

    private static List<ConstructorTimelineColumn> BuildTeacherTimelineColumns(
        int teacherId,
        IReadOnlyList<LessonSlot> allSlots,
        IReadOnlyList<BellPeriod> bells,
        int maxLessons,
        bool showBreaks)
    {
        var teacherSlots = allSlots.Where(s => s.TeacherId == teacherId).ToList();
        var (grade, shift) = ResolveTeacherTimelineProfile(teacherSlots);
        string? templateName = null;
        if (grade == ScheduleGridBuilder.FirstGradeTimelineGrade)
        {
            templateName = teacherSlots
                .Where(s => s.ClassGrade == grade && !string.IsNullOrWhiteSpace(s.BellTemplateName))
                .Select(s => s.BellTemplateName)
                .FirstOrDefault();
        }

        var columns = BellScheduleResolver.BuildPrimaryTimeline(bells, grade, shift, showBreaks, templateName).ToList();

        var seniorLessonNumbers = teacherSlots
            .Where(s => s.ClassGrade >= 5 && !SubjectScheduleRules.IsDynamicPause(s.SubjectName))
            .Select(s => s.LessonNumber)
            .Distinct()
            .OrderBy(n => n);

        foreach (var lessonNumber in seniorLessonNumbers)
        {
            if (columns.Any(c => !c.IsDynamicPause && c.LessonNumber == lessonNumber))
                continue;

            var sample = teacherSlots.First(s => s.ClassGrade >= 5 && s.LessonNumber == lessonNumber);
            var period = BellScheduleResolver.FindLessonPeriod(bells, sample.ClassGrade, sample.ClassShift, lessonNumber);
            columns.Add(new ConstructorTimelineColumn
            {
                LessonNumber = lessonNumber,
                StorageLessonNumber = lessonNumber,
                Title = $"Урок {lessonNumber}",
                BellTimeDisplay = BellScheduleResolver.FormatPeriodTime(period)
            });
        }

        return columns;
    }

    private static bool RoomUsesPrimaryTimeline(
        int roomId,
        IReadOnlyList<LessonSlot> roomSlots,
        IReadOnlyList<BellPeriod> bells)
    {
        if (roomSlots.Any(s => SubjectScheduleRules.IsDynamicPause(s.SubjectName)))
            return true;

        return roomSlots.Any(s => s.ClassGrade is >= 1 and <= 4
            && BellScheduleResolver.GetDynamicPausesForGrade(bells, s.ClassGrade, s.ClassShift).Count > 0);
    }

    private static (int Grade, int Shift) ResolveRoomTimelineProfile(IReadOnlyList<LessonSlot> roomSlots)
    {
        var primary = roomSlots.Where(s => s.ClassGrade is >= 1 and <= 4).ToList();
        if (primary.Count == 0)
            primary = roomSlots.ToList();

        var grade = primary.Any(s => s.ClassGrade == ScheduleGridBuilder.FirstGradeTimelineGrade)
            ? ScheduleGridBuilder.FirstGradeTimelineGrade
            : primary.Where(s => s.ClassGrade is >= 1 and <= 4).Select(s => s.ClassGrade).DefaultIfEmpty(5).Min();

        var shift = primary
            .GroupBy(s => s.ClassShift)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First().Key;

        return (grade, shift);
    }

    private static List<ConstructorTimelineColumn> BuildRoomTimelineColumns(
        IReadOnlyList<LessonSlot> roomSlots,
        IReadOnlyList<BellPeriod> bells,
        int maxLessons,
        bool showBreaks)
    {
        if (roomSlots.Count == 0)
            return BuildFallbackRoomTimeline(maxLessons);

        var roomId = roomSlots[0].RoomId;
        if (RoomUsesPrimaryTimeline(roomId, roomSlots, bells))
        {
            var (grade, shift) = ResolveRoomTimelineProfile(roomSlots);
            string? templateName = null;
            if (grade == ScheduleGridBuilder.FirstGradeTimelineGrade)
            {
                templateName = roomSlots
                    .Where(s => s.ClassGrade == grade && !string.IsNullOrWhiteSpace(s.BellTemplateName))
                    .Select(s => s.BellTemplateName)
                    .FirstOrDefault();
            }

            var columns = BellScheduleResolver.BuildPrimaryTimeline(bells, grade, shift, showBreaks, templateName).ToList();

            var seniorLessonNumbers = roomSlots
                .Where(s => s.ClassGrade >= 5 && !SubjectScheduleRules.IsDynamicPause(s.SubjectName))
                .Select(s => s.LessonNumber)
                .Distinct()
                .OrderBy(n => n);

            foreach (var lessonNumber in seniorLessonNumbers)
            {
                if (columns.Any(c => !c.IsDynamicPause && !c.IsBreak && c.LessonNumber == lessonNumber))
                    continue;

                var sample = roomSlots.First(s => s.ClassGrade >= 5 && s.LessonNumber == lessonNumber);
                var period = BellScheduleResolver.FindLessonPeriod(bells, sample.ClassGrade, sample.ClassShift, lessonNumber);
                columns.Add(new ConstructorTimelineColumn
                {
                    LessonNumber = lessonNumber,
                    StorageLessonNumber = lessonNumber,
                    Title = $"Урок {lessonNumber}",
                    BellTimeDisplay = BellScheduleResolver.FormatPeriodTime(period)
                });
            }

            return columns;
        }

        if (showBreaks)
        {
            var (grade, shift) = ResolveRoomTimelineProfile(roomSlots);
            return BellScheduleResolver.BuildStandardLessonTimeline(bells, grade, shift, maxLessons, true);
        }

        return BuildFallbackRoomTimeline(maxLessons);
    }

    private sealed class OverviewRoomTimelineDef
    {
        public required ConstructorTimelineColumn Column { get; init; }
        public int ClassShift { get; init; } = 1;
        public int ProfileGrade { get; init; } = 5;
        public int StartMinutes { get; init; }
    }

    private static List<OverviewRoomTimelineDef> BuildRoomTimelineDefs(
        int roomId,
        IReadOnlyList<LessonSlot> allSlots,
        IReadOnlyList<BellPeriod> bells,
        int maxLessons,
        bool showBreaks)
    {
        var roomSlots = allSlots.Where(s => s.RoomId == roomId).ToList();
        var shifts = roomSlots.Select(s => s.ClassShift).Distinct().OrderBy(s => s).ToList();
        if (shifts.Count <= 1)
        {
            var (grade, shift) = roomSlots.Count > 0 ? ResolveRoomTimelineProfile(roomSlots) : (5, 1);
            var timeline = BuildRoomTimelineColumns(roomSlots, bells, maxLessons, showBreaks);
            return timeline.Select(c => ToRoomTimelineDef(c, grade, shift, bells)).ToList();
        }

        var merged = new List<OverviewRoomTimelineDef>();
        foreach (var shift in shifts)
        {
            var shiftSlots = roomSlots.Where(s => s.ClassShift == shift).ToList();
            if (shiftSlots.Count == 0)
                continue;

            var (grade, _) = ResolveRoomTimelineProfile(shiftSlots);
            var timeline = BuildRoomTimelineColumns(shiftSlots, bells, maxLessons, showBreaks);
            merged.AddRange(timeline.Select(c => ToRoomTimelineDef(c, grade, shift, bells)));
        }

        return merged
            .OrderBy(d => d.ClassShift)
            .ThenBy(d => d.StartMinutes > 0 ? d.StartMinutes : d.Column.LessonNumber * 100)
            .ThenBy(d => d.Column.LessonNumber)
            .ToList();
    }

    private static List<ConstructorTimelineColumn> BuildShiftTimeline(
        IReadOnlyList<OverviewRoomTimelineDef> timelineDefs,
        int classShift) =>
        timelineDefs.Where(d => d.ClassShift == classShift).Select(d => d.Column).ToList();

    private static OverviewRoomTimelineDef ToRoomTimelineDef(
        ConstructorTimelineColumn column,
        int grade,
        int shift,
        IReadOnlyList<BellPeriod> bells)
    {
        var time = column.BellTimeDisplay;
        if (string.IsNullOrWhiteSpace(time))
        {
            var period = column.IsDynamicPause
                ? BellScheduleResolver.FindDynamicPauseAfterLesson(bells, grade, shift, column.AfterLessonNumber)
                : column.IsBreak
                    ? BellScheduleResolver.GetBreaksForGrade(bells, grade, shift)
                        .FirstOrDefault(b => b.LessonNumber == column.AfterLessonNumber)
                    : BellScheduleResolver.FindLessonPeriod(
                        bells, grade, shift, ScheduleGridBuilder.ResolveColumnBellLessonNumber(column));
            time = BellScheduleResolver.FormatPeriodTime(period);
        }

        return new OverviewRoomTimelineDef
        {
            Column = column,
            ClassShift = shift,
            ProfileGrade = grade,
            StartMinutes = ParseOverviewMinutes(time).Start
        };
    }

    private static (int Start, int End) ParseOverviewMinutes(string timeDisplay)
    {
        if (string.IsNullOrWhiteSpace(timeDisplay))
            return (0, 0);

        var parts = timeDisplay.Split('–', '-', '—');
        if (parts.Length == 2
            && TryParseOverviewTime(parts[0], out var start)
            && TryParseOverviewTime(parts[1], out var end))
            return (start, end);

        return TryParseOverviewTime(timeDisplay, out var single) ? (single, single + 40) : (0, 0);
    }

    private static bool TryParseOverviewTime(string value, out int minutes)
    {
        minutes = 0;
        if (!TimeSpan.TryParse(value.Trim(), out var ts))
            return false;
        minutes = (int)ts.TotalMinutes;
        return true;
    }

    private static List<ConstructorTimelineColumn> BuildFallbackRoomTimeline(int maxLessons)
    {
        var columns = new List<ConstructorTimelineColumn>(maxLessons);
        for (var lesson = 1; lesson <= maxLessons; lesson++)
        {
            columns.Add(new ConstructorTimelineColumn
            {
                LessonNumber = lesson,
                StorageLessonNumber = lesson,
                Title = $"Урок {lesson}"
            });
        }

        return columns;
    }

    private static List<OverviewMatrixRow> BuildRoomRows(
        List<OverviewColumnHeader> dayHeaders,
        IReadOnlyList<LessonSlot> slots,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyList<Room> rooms,
        IReadOnlyDictionary<int, Room> roomsById,
        OverviewFilter filter,
        int maxLessons,
        bool showBreaks)
    {
        var lookup = slots
            .GroupBy(s => (s.RoomId, s.DayOfWeek, s.LessonNumber, s.ClassShift))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubgroupIndex).ToList());

        var activeIds = slots.Select(s => s.RoomId).Distinct().ToHashSet();
        IEnumerable<Room> source = rooms
            .Where(r => activeIds.Contains(r.Id))
            .OrderBy(r => r.BuildingName)
            .ThenBy(r => r.Number);
        if (filter.RoomId is int roomId)
            source = source.Where(r => r.Id == roomId);
        else if (!string.IsNullOrWhiteSpace(filter.BuildingName))
            source = source.Where(r => r.BuildingName.Equals(filter.BuildingName, StringComparison.OrdinalIgnoreCase));

        var rows = new List<OverviewMatrixRow>();
        var roomList = source.ToList();
        for (var roomIndex = 0; roomIndex < roomList.Count; roomIndex++)
        {
            var room = roomList[roomIndex];
            var row = new OverviewMatrixRow
            {
                RowLabel = $"каб. {room.Number}",
                RowSubLabel = room.BuildingName,
                RowKind = OverviewRowKinds.Room,
                EntityId = room.Id,
                BuildingName = room.BuildingName,
                BuildingColorHex = room.BuildingColorHex,
                HasRowSeparatorBelow = true,
                IsTrackGroupEnd = roomIndex == roomList.Count - 1
                    || !roomList[roomIndex + 1].BuildingName.Equals(room.BuildingName, StringComparison.OrdinalIgnoreCase)
            };
            FillRoomRow(row, dayHeaders, lookup, room.Id, slots, bells, maxLessons, roomsById, showBreaks);
            rows.Add(row);
        }

        return rows;
    }

    private static void FillClassTimelineRow(
        OverviewMatrixRow row,
        List<OverviewColumnHeader> dayHeaders,
        Dictionary<(int ClassId, int DayOfWeek, int LessonNumber), List<LessonSlot>> lookup,
        SchoolClass cls,
        IReadOnlyList<BellPeriod> bells,
        string? templateName,
        IReadOnlyDictionary<int, Room> roomsById,
        bool showBreaks)
    {
        var timeline = BellScheduleResolver.BuildPrimaryTimeline(
            bells, cls.Grade, cls.Shift, showBreaks, templateName);
        foreach (var day in dayHeaders)
        {
            var column = new OverviewDayColumn { DayOfWeek = day.DayOfWeek };
            foreach (var timelineColumn in timeline)
            {
                if (timelineColumn.IsBreak)
                {
                    column.Lessons.Add(BuildBreakTimelineCell(timelineColumn));
                    continue;
                }

                var group = ScheduleGridBuilder.ResolveClassTimelineSlots(
                    cls.Id, day.DayOfWeek, timelineColumn, timeline, lookup);
                var period = timelineColumn.IsDynamicPause
                    ? BellScheduleResolver.FindDynamicPauseAfterLesson(
                        bells, cls.Grade, cls.Shift, timelineColumn.AfterLessonNumber)
                    : BellScheduleResolver.FindLessonPeriod(
                        bells, cls.Grade, cls.Shift, timelineColumn.LessonNumber, templateName);
                column.Lessons.Add(BuildTimelineCell(
                    group, timelineColumn, period, slot => FormatClassCell(slot, roomsById)));
            }

            row.Days.Add(column);
        }
    }

    private static void FillClassStandardRow(
        OverviewMatrixRow row,
        List<OverviewColumnHeader> dayHeaders,
        Dictionary<(int ClassId, int DayOfWeek, int LessonNumber), List<LessonSlot>> lookup,
        SchoolClass cls,
        IReadOnlyList<BellPeriod> bells,
        string? templateName,
        int maxLessons,
        IReadOnlyDictionary<int, Room> roomsById,
        bool showBreaks)
    {
        var timeline = showBreaks
            ? BellScheduleResolver.BuildStandardLessonTimeline(bells, cls.Grade, cls.Shift, maxLessons, true)
            : null;

        foreach (var day in dayHeaders)
        {
            var column = new OverviewDayColumn { DayOfWeek = day.DayOfWeek };
            if (timeline is not null)
            {
                foreach (var timelineColumn in timeline)
                {
                    if (timelineColumn.IsBreak)
                    {
                        column.Lessons.Add(BuildBreakTimelineCell(timelineColumn));
                        continue;
                    }

                    lookup.TryGetValue((cls.Id, day.DayOfWeek, timelineColumn.LessonNumber), out var group);
                    if (group is not null)
                        group = group.Where(s => !SubjectScheduleRules.IsDynamicPause(s.SubjectName)).ToList();
                    if (group is { Count: 0 })
                        group = null;

                    var period = ResolvePeriodFromSlots(group, bells, timelineColumn.LessonNumber)
                        ?? BellScheduleResolver.FindLessonPeriod(
                            bells, cls.Grade, cls.Shift, timelineColumn.LessonNumber, templateName);
                    column.Lessons.Add(BuildTimelineCell(
                        group, timelineColumn, period, slot => FormatClassCell(slot, roomsById)));
                }
            }
            else
            {
                for (var lesson = 1; lesson <= maxLessons; lesson++)
                {
                    lookup.TryGetValue((cls.Id, day.DayOfWeek, lesson), out var group);
                    var period = ResolvePeriodFromSlots(group, bells, lesson);
                    var timelineColumn = new ConstructorTimelineColumn
                    {
                        LessonNumber = lesson,
                        StorageLessonNumber = lesson,
                        Title = $"Урок {lesson}"
                    };
                    column.Lessons.Add(BuildTimelineCell(
                        group, timelineColumn, period, slot => FormatClassCell(slot, roomsById)));
                }
            }

            row.Days.Add(column);
        }
    }

    private static void FillTeacherTimelineRow(
        OverviewMatrixRow row,
        List<OverviewColumnHeader> dayHeaders,
        Dictionary<(int TeacherId, int DayOfWeek, int LessonNumber), List<LessonSlot>> lookup,
        int teacherId,
        IReadOnlyList<LessonSlot> allSlots,
        IReadOnlyList<BellPeriod> bells,
        int maxLessons,
        IReadOnlyDictionary<int, Room> roomsById,
        bool showBreaks)
    {
        var timeline = BuildTeacherTimelineColumns(teacherId, allSlots, bells, maxLessons, showBreaks);
        var (grade, shift) = ResolveTeacherTimelineProfile(allSlots.Where(s => s.TeacherId == teacherId));

        foreach (var day in dayHeaders)
        {
            var column = new OverviewDayColumn { DayOfWeek = day.DayOfWeek };
            foreach (var timelineColumn in timeline)
            {
                if (timelineColumn.IsBreak)
                {
                    column.Lessons.Add(BuildBreakTimelineCell(timelineColumn));
                    continue;
                }

                var group = ScheduleGridBuilder.ResolveTeacherTimelineSlots(
                    teacherId, day.DayOfWeek, timelineColumn, timeline, lookup);
                var period = timelineColumn.IsDynamicPause
                    ? BellScheduleResolver.FindDynamicPauseAfterLesson(
                        bells, grade, shift, timelineColumn.AfterLessonNumber)
                    : ResolveTeacherTimelinePeriod(group, timelineColumn, bells, grade, shift);
                column.Lessons.Add(BuildTimelineCell(
                    group, timelineColumn, period, slot => FormatTeacherCell(slot, roomsById)));
            }

            row.Days.Add(column);
        }
    }

    private static BellPeriod? ResolveTeacherTimelinePeriod(
        List<LessonSlot>? group,
        ConstructorTimelineColumn timelineColumn,
        IReadOnlyList<BellPeriod> bells,
        int defaultGrade,
        int defaultShift)
    {
        if (group is { Count: > 0 })
            return ResolvePeriodFromSlots(group, bells, timelineColumn.LessonNumber);

        return BellScheduleResolver.FindLessonPeriod(bells, defaultGrade, defaultShift, timelineColumn.LessonNumber);
    }

    private static void FillTeacherStandardRow(
        OverviewMatrixRow row,
        List<OverviewColumnHeader> dayHeaders,
        Dictionary<(int TeacherId, int DayOfWeek, int LessonNumber), List<LessonSlot>> lookup,
        int teacherId,
        IReadOnlyList<BellPeriod> bells,
        int maxLessons,
        IReadOnlyDictionary<int, Room> roomsById,
        bool showBreaks)
    {
        var profileSlots = lookup.Values.SelectMany(g => g).Where(s => s.TeacherId == teacherId).ToList();
        var (grade, shift) = profileSlots.Count > 0
            ? (profileSlots[0].ClassGrade, profileSlots[0].ClassShift)
            : (5, 1);
        var timeline = showBreaks
            ? BellScheduleResolver.BuildStandardLessonTimeline(bells, grade, shift, maxLessons, true)
            : null;

        foreach (var day in dayHeaders)
        {
            var column = new OverviewDayColumn { DayOfWeek = day.DayOfWeek };
            if (timeline is not null)
            {
                foreach (var timelineColumn in timeline)
                {
                    if (timelineColumn.IsBreak)
                    {
                        column.Lessons.Add(BuildBreakTimelineCell(timelineColumn));
                        continue;
                    }

                    lookup.TryGetValue((teacherId, day.DayOfWeek, timelineColumn.LessonNumber), out var group);
                    var period = ResolvePeriodFromSlots(group, bells, timelineColumn.LessonNumber)
                        ?? BellScheduleResolver.FindLessonPeriod(bells, grade, shift, timelineColumn.LessonNumber);
                    column.Lessons.Add(BuildTimelineCell(
                        group, timelineColumn, period, slot => FormatTeacherCell(slot, roomsById)));
                }
            }
            else
            {
                for (var lesson = 1; lesson <= maxLessons; lesson++)
                {
                    lookup.TryGetValue((teacherId, day.DayOfWeek, lesson), out var group);
                    var period = ResolvePeriodFromSlots(group, bells, lesson);
                    var timelineColumn = new ConstructorTimelineColumn
                    {
                        LessonNumber = lesson,
                        StorageLessonNumber = lesson,
                        Title = $"Урок {lesson}"
                    };
                    column.Lessons.Add(BuildTimelineCell(
                        group, timelineColumn, period, slot => FormatTeacherCell(slot, roomsById)));
                }
            }

            row.Days.Add(column);
        }
    }

    private static void FillRoomRow(
        OverviewMatrixRow row,
        List<OverviewColumnHeader> dayHeaders,
        Dictionary<(int RoomId, int DayOfWeek, int LessonNumber, int ClassShift), List<LessonSlot>> lookup,
        int roomId,
        IReadOnlyList<LessonSlot> allSlots,
        IReadOnlyList<BellPeriod> bells,
        int maxLessons,
        IReadOnlyDictionary<int, Room> roomsById,
        bool showBreaks)
    {
        var timelineDefs = BuildRoomTimelineDefs(roomId, allSlots, bells, maxLessons, showBreaks);

        foreach (var day in dayHeaders)
        {
            var column = new OverviewDayColumn { DayOfWeek = day.DayOfWeek };
            int? prevShift = null;
            foreach (var def in timelineDefs)
            {
                var isShiftBoundary = def.ClassShift >= 2 && prevShift is int prev && prev < def.ClassShift;
                prevShift = def.ClassShift;
                var shiftTimeline = BuildShiftTimeline(timelineDefs, def.ClassShift);

                if (def.Column.IsBreak)
                {
                    column.Lessons.Add(BuildBreakTimelineCell(
                        def.Column, def.ClassShift, isShiftBoundary));
                    continue;
                }

                var group = ScheduleGridBuilder.ResolveRoomTimelineSlots(
                    roomId, day.DayOfWeek, def.ClassShift, def.Column, shiftTimeline, lookup);
                var period = def.Column.IsDynamicPause
                    ? BellScheduleResolver.FindDynamicPauseAfterLesson(
                        bells, def.ProfileGrade, def.ClassShift, def.Column.AfterLessonNumber)
                    : ResolveRoomTimelinePeriod(group, def.Column, bells, def.ProfileGrade, def.ClassShift);
                column.Lessons.Add(BuildTimelineCell(
                    group,
                    def.Column,
                    period,
                    slot => FormatRoomCell(slot, roomsById),
                    def.ClassShift,
                    isShiftBoundary));
            }

            row.Days.Add(column);
        }
    }

    private static BellPeriod? ResolveRoomTimelinePeriod(
        List<LessonSlot>? group,
        ConstructorTimelineColumn timelineColumn,
        IReadOnlyList<BellPeriod> bells,
        int defaultGrade,
        int defaultShift)
    {
        if (group is { Count: > 0 })
            return ResolvePeriodFromSlots(group, bells, group[0].LessonNumber);

        return BellScheduleResolver.FindLessonPeriod(bells, defaultGrade, defaultShift, timelineColumn.LessonNumber);
    }

    private static BellPeriod? ResolvePeriodFromSlots(
        List<LessonSlot>? group,
        IReadOnlyList<BellPeriod> bells,
        int lessonNumber)
    {
        if (group is null || group.Count == 0)
            return null;

        var sample = group[0];
        if (SubjectScheduleRules.IsDynamicPause(sample.SubjectName))
        {
            return BellScheduleResolver.FindDynamicPauseAfterLesson(
                       bells, sample.ClassGrade, sample.ClassShift, sample.LessonNumber - 1)
                   ?? BellScheduleResolver.FindDynamicPauseAfterLesson(
                       bells, sample.ClassGrade, sample.ClassShift, lessonNumber - 1);
        }

        return BellScheduleResolver.FindLessonPeriod(
            bells, sample.ClassGrade, sample.ClassShift, lessonNumber);
    }

    private static OverviewTimelineCell BuildTimelineCell(
        List<LessonSlot>? group,
        ConstructorTimelineColumn timelineColumn,
        BellPeriod? period,
        Func<LessonSlot, OverviewCellLines> lineFormatter,
        int columnClassShift = 1,
        bool isShiftBoundaryRow = false)
    {
        if (group is null || group.Count == 0)
        {
            var emptyLabel = FormatTimelineTimeLabel(timelineColumn, period);
            if (isShiftBoundaryRow && columnClassShift >= 2)
                emptyLabel = $"2 смена · {emptyLabel}";

            return new OverviewTimelineCell
            {
                LessonNumber = timelineColumn.StorageLessonNumber,
                IsDynamicPauseColumn = timelineColumn.IsDynamicPause,
                IsBreakColumn = timelineColumn.IsBreak,
                TimeLabel = emptyLabel,
                ColumnClassShift = columnClassShift,
                IsShiftBoundaryRow = isShiftBoundaryRow
            };
        }

        var first = group[0];
        var timeLabel = FormatTimeLabel(first.LessonNumber, period);
        if (isShiftBoundaryRow && first.ClassShift >= 2)
            timeLabel = $"2 смена · {timeLabel}";

        var showSubgroupLabels = group.Count > 1 || group.Any(s => s.SubgroupIndex > 0);
        var parts = group
            .Select(slot => ToPart(slot, lineFormatter, showSubgroupLabels))
            .ToList();

        return new OverviewTimelineCell
        {
            LessonNumber = first.LessonNumber,
            IsDynamicPauseColumn = timelineColumn.IsDynamicPause
                || SubjectScheduleRules.IsDynamicPause(first.SubjectName),
            TimeLabel = timeLabel,
            Parts = parts,
            ClassId = first.ClassId,
            TeacherId = first.TeacherId,
            RoomId = first.RoomId,
            BuildingName = first.BuildingName,
            ColumnClassShift = first.ClassShift,
            IsShiftBoundaryRow = isShiftBoundaryRow && first.ClassShift >= 2
        };
    }

    private static OverviewTimelinePart ToPart(
        LessonSlot slot,
        Func<LessonSlot, OverviewCellLines> lineFormatter,
        bool showSubgroupLabels)
    {
        var lines = lineFormatter(slot);
        var subgroupLabel = showSubgroupLabels ? (slot.SubgroupIndex + 1).ToString() : "";
        return new OverviewTimelinePart
        {
            SubgroupLabel = subgroupLabel,
            SubjectLine = lines.Subject,
            TeacherLine = lines.Teacher,
            RoomLine = lines.Room,
            RoomBuildingColorHex = lines.RoomColorHex,
            PrimaryLine = lines.Subject,
            SecondaryLine = string.IsNullOrWhiteSpace(lines.Room)
                ? lines.Teacher
                : $"{lines.Teacher} · {lines.Room}",
            ClassId = slot.ClassId,
            TeacherId = slot.TeacherId,
            RoomId = slot.RoomId,
            BuildingName = slot.BuildingName
        };
    }

    private static string FormatTimelineTimeLabel(ConstructorTimelineColumn column, BellPeriod? period)
    {
        if (column.IsBreak)
        {
            var time = !string.IsNullOrWhiteSpace(column.BellTimeDisplay)
                ? column.BellTimeDisplay
                : BellScheduleResolver.FormatPeriodTime(period);
            return string.IsNullOrWhiteSpace(time) ? "Перемена" : $"Пер. · {time}";
        }

        if (column.IsDynamicPause)
        {
            var time = BellScheduleResolver.FormatPeriodTime(period);
            return string.IsNullOrWhiteSpace(time) ? "Дин. пауза" : $"Дин. · {time}";
        }

        return FormatTimeLabel(column.LessonNumber, period);
    }

    private static string FormatTimeLabel(int lessonNumber, BellPeriod? period)
    {
        if (period is null || string.IsNullOrWhiteSpace(period.StartTime))
            return $"Урок {lessonNumber}";

        var start = TrimTime(period.StartTime);
        var end = TrimTime(period.EndTime);
        return string.IsNullOrWhiteSpace(end)
            ? $"{lessonNumber} · {start}"
            : $"{lessonNumber} · {start}–{end}";
    }

    private static string TrimTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        if (TimeSpan.TryParse(value, out var ts))
            return ts.ToString(@"hh\:mm");

        return value.Length >= 5 ? value[..5] : value;
    }

    private static OverviewCellLines FormatClassCell(LessonSlot slot, IReadOnlyDictionary<int, Room> roomsById) =>
        new(
            slot.SubjectName,
            slot.TeacherName,
            ScheduleGridBuilder.FormatOverviewRoomLine(slot),
            ScheduleGridBuilder.ResolveOverviewRoomColor(slot.RoomId, roomsById));

    private static OverviewCellLines FormatTeacherCell(LessonSlot slot, IReadOnlyDictionary<int, Room> roomsById) =>
        new(
            $"{slot.ClassName} · {slot.SubjectName}",
            slot.TeacherName,
            ScheduleGridBuilder.FormatOverviewRoomLine(slot),
            ScheduleGridBuilder.ResolveOverviewRoomColor(slot.RoomId, roomsById));

    private static OverviewCellLines FormatRoomCell(LessonSlot slot, IReadOnlyDictionary<int, Room> roomsById) =>
        new(
            $"{slot.ClassName} · {slot.SubjectName}",
            slot.TeacherName,
            "",
            ScheduleGridBuilder.ResolveOverviewRoomColor(slot.RoomId, roomsById));

    private sealed record OverviewCellLines(string Subject, string Teacher, string Room, string RoomColorHex);

    private static OverviewTimelineCell BuildBreakTimelineCell(
        ConstructorTimelineColumn column,
        int columnClassShift = 1,
        bool isShiftBoundaryRow = false)
    {
        var timeLabel = FormatTimelineTimeLabel(column, null);
        if (isShiftBoundaryRow && columnClassShift >= 2)
            timeLabel = $"2 смена · {timeLabel}";

        return new OverviewTimelineCell
        {
            IsBreakColumn = true,
            TimeLabel = timeLabel,
            ColumnClassShift = columnClassShift,
            IsShiftBoundaryRow = isShiftBoundaryRow
        };
    }

    private static void EnsureLeadingDayHeaderRow(List<OverviewMatrixRow> rows)
    {
        if (rows.Count == 0 || rows.Any(r => r.IsDayHeaderRow))
            return;

        var hasSectionDayHeaders = rows.Any(r => r.IsSectionHeader && r.ShowDayHeadersBelow);
        if (hasSectionDayHeaders)
            return;

        rows.Insert(0, new OverviewMatrixRow { IsDayHeaderRow = true });
    }

    private static OverviewMatrixRow CreateSectionHeader(string title, bool showDayHeadersBelow = false) => new()
    {
        IsSectionHeader = true,
        SectionTitle = title,
        ShowDayHeadersBelow = showDayHeadersBelow
    };
}
