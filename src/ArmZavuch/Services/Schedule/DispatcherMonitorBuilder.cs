using ArmZavuch.Models;
using ArmZavuch.Services.Staff;

namespace ArmZavuch.Services.Schedule;

/// <summary>Сборка timeline-монитора дня для диспетчерской, включая нерабочее время педагогов из анкеты.</summary>
public static class DispatcherMonitorBuilder
{
    private const string BuildingTransitionIcon = "🚶 ";

    public static List<DispatcherMonitorSection> Build(
        string mode,
        IReadOnlyList<LessonSlot> lessons,
        IReadOnlyList<Teacher> teachers,
        IReadOnlyList<Room> rooms,
        IReadOnlyDictionary<string, string> buildingColors,
        IReadOnlyList<BellPeriod> bells,
        DateOnly? highlightNowForDate = null,
        int? filterTeacherId = null,
        int? filterClassId = null,
        string? filterBuilding = null,
        BellTemplateAssignmentSnapshot? assignment = null,
        IReadOnlyList<TeacherUnavailability>? teacherUnavailabilities = null,
        DayBellAdjustment? dayBellAdjustment = null)
    {
        var active = lessons.Where(l => !l.IsCancelled).ToList();
        if (filterTeacherId is int teacherId)
            active = active.Where(l => l.TeacherId == teacherId).ToList();
        if (filterClassId is int classId)
            active = active.Where(l => l.ClassId == classId).ToList();
        if (!string.IsNullOrWhiteSpace(filterBuilding))
            active = active.Where(l =>
                l.BuildingName.Equals(filterBuilding, StringComparison.OrdinalIgnoreCase)).ToList();

        var nowMinutes = ResolveNowMinutes(highlightNowForDate);
        assignment ??= BellTemplateAssignmentSnapshot.Fallback;
        var unavailByTeacher = (teacherUnavailabilities ?? [])
            .GroupBy(u => u.TeacherId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TeacherUnavailability>)g.ToList());

        if (filterClassId is not null || filterTeacherId is not null)
            return [BuildFilteredSection(mode, active, teachers, rooms, buildingColors, bells, nowMinutes, assignment, highlightNowForDate, unavailByTeacher, dayBellAdjustment)];

        if (!string.IsNullOrWhiteSpace(filterBuilding))
            return BuildFilteredBuildingSections(mode, active, teachers, rooms, buildingColors, bells, nowMinutes, assignment, highlightNowForDate, unavailByTeacher, dayBellAdjustment);

        return mode switch
        {
            DispatcherMonitorModes.Classes => BuildClassSections(mode, active, buildingColors, bells, nowMinutes, assignment, dayBellAdjustment),
            DispatcherMonitorModes.Buildings => BuildRoomSections(mode, active, rooms, buildingColors, bells, nowMinutes, assignment, dayBellAdjustment),
            _ => BuildTeacherSections(mode, active, teachers, buildingColors, bells, nowMinutes, assignment, highlightNowForDate, unavailByTeacher, dayBellAdjustment)
        };
    }

    private enum MonitorBand
    {
        FirstGrade,
        JuniorPrimary,
        Standard
    }

    private static List<DispatcherMonitorSection> BuildClassSections(
        string mode,
        List<LessonSlot> active,
        IReadOnlyDictionary<string, string> buildingColors,
        IReadOnlyList<BellPeriod> bells,
        int? nowMinutes,
        BellTemplateAssignmentSnapshot? assignment = null,
        DayBellAdjustment? dayBellAdjustment = null)
    {
        assignment ??= BellTemplateAssignmentSnapshot.Fallback;
        var sections = new List<DispatcherMonitorSection>();
        var shifts = active.Select(l => l.ClassShift).Distinct().OrderBy(s => s).ToList();
        var showShiftHeaders = shifts.Count > 1 || shifts.Any(s => s > 1);

        foreach (var shift in shifts)
        {
            if (showShiftHeaders)
                sections.Add(CreateShiftHeader(shift));

            var shiftLessons = active.Where(l => l.ClassShift == shift).ToList();
            foreach (var track in BellScheduleTrackGrouper.GroupLessonTracks(shiftLessons, assignment))
            {
                var templatePeriods = BellScheduleResolver.FilterByTemplate(bells, track.TemplateName);
                var sample = track.Lessons[0];
                var usePrimary = sample.ClassGrade == 1
                    || BellScheduleResolver.GetDynamicPausesForGrade(templatePeriods, sample.ClassGrade, track.Shift).Count > 0;
                var dayColumns = BuildSectionTimelineColumns(
                    track.Lessons, templatePeriods, sample.ClassGrade, track.Shift, usePrimary, assignment, track.TemplateName, dayBellAdjustment);
                var timeline = dayColumns.Select(d => d.Column).ToList();
                var rows = BuildClassRows(mode, track.Lessons.ToList(), dayColumns, timeline, buildingColors, templatePeriods, dayBellAdjustment);
                if (rows.Count == 0)
                    continue;

                sections.Add(new DispatcherMonitorSection
                {
                    Title = BellScheduleTrackGrouper.BuildLessonTrackTitle(track),
                    SubTitle = showShiftHeaders ? $"{track.Shift} смена" : "",
                    Columns = ToMonitorColumns(dayColumns, nowMinutes),
                    Rows = rows
                });
            }
        }

        return sections;
    }

    private static List<DispatcherMonitorSection> BuildTeacherSections(
        string mode,
        List<LessonSlot> active,
        IReadOnlyList<Teacher> teachers,
        IReadOnlyDictionary<string, string> buildingColors,
        IReadOnlyList<BellPeriod> bells,
        int? nowMinutes,
        BellTemplateAssignmentSnapshot assignment,
        DateOnly? monitorDate,
        IReadOnlyDictionary<int, IReadOnlyList<TeacherUnavailability>> unavailByTeacher,
        DayBellAdjustment? dayBellAdjustment = null)
    {
        var sections = new List<DispatcherMonitorSection>();
        var shifts = active.Select(l => l.ClassShift).Distinct().OrderBy(s => s).ToList();
        var showShiftHeaders = shifts.Count > 1 || shifts.Any(s => s > 1);

        foreach (var shift in shifts)
        {
            var shiftLessons = active.Where(l => l.ClassShift == shift).ToList();
            if (showShiftHeaders)
            {
                var priorLessons = active.Where(l => l.ClassShift < shift).ToList();
                var interShiftHint = BuildInterShiftSummaryHint(priorLessons, shiftLessons);
                sections.Add(CreateShiftHeader(shift, interShiftHint));
            }

            var priorShiftTails = BuildTeacherChronologicalTails(
                active.Where(l => l.ClassShift < shift).ToList());
            AppendBandSections(sections, mode, shiftLessons, shift, showShiftHeaders, buildingColors, bells, nowMinutes, assignment,
                rowBuilder: (bandLessons, dayColumns, timeline) =>
                    BuildTeacherRows(mode, bandLessons, dayColumns, timeline, teachers, buildingColors, bells, assignment, priorShiftTails, monitorDate, unavailByTeacher),
                dayBellAdjustment);
        }

        return sections;
    }

    private static List<DispatcherMonitorSection> BuildRoomSections(
        string mode,
        List<LessonSlot> active,
        IReadOnlyList<Room> rooms,
        IReadOnlyDictionary<string, string> buildingColors,
        IReadOnlyList<BellPeriod> bells,
        int? nowMinutes,
        BellTemplateAssignmentSnapshot assignment,
        DayBellAdjustment? dayBellAdjustment = null)
    {
        var sections = new List<DispatcherMonitorSection>();
        var shifts = active.Select(l => l.ClassShift).Distinct().OrderBy(s => s).ToList();
        var showShiftHeaders = shifts.Count > 1 || shifts.Any(s => s > 1);

        foreach (var shift in shifts)
        {
            if (showShiftHeaders)
                sections.Add(CreateShiftHeader(shift));

            var shiftLessons = active.Where(l => l.ClassShift == shift).ToList();
            AppendBandSections(sections, mode, shiftLessons, shift, showShiftHeaders, buildingColors, bells, nowMinutes, assignment,
                rowBuilder: (bandLessons, dayColumns, timeline) =>
                    BuildRoomRows(mode, bandLessons, dayColumns, timeline, rooms, buildingColors, bells),
                dayBellAdjustment);
        }

        return sections;
    }

    private static void AppendBandSections(
        List<DispatcherMonitorSection> sections,
        string mode,
        List<LessonSlot> shiftLessons,
        int shift,
        bool showShiftHeaders,
        IReadOnlyDictionary<string, string> buildingColors,
        IReadOnlyList<BellPeriod> bells,
        int? nowMinutes,
        BellTemplateAssignmentSnapshot assignment,
        Func<List<LessonSlot>, IReadOnlyList<TimelineColumnDef>, IReadOnlyList<ConstructorTimelineColumn>, List<DispatcherMonitorRow>> rowBuilder,
        DayBellAdjustment? dayBellAdjustment = null)
    {
        foreach (var band in new[] { MonitorBand.FirstGrade, MonitorBand.JuniorPrimary, MonitorBand.Standard })
        {
            var bandLessons = shiftLessons.Where(l => ClassifyBand(l, bells) == band).ToList();
            if (bandLessons.Count == 0)
                continue;

            var (profileGrade, usePrimary) = ResolveBandProfile(band, bandLessons);
            var dayColumns = BuildSectionTimelineColumns(bandLessons, bells, profileGrade, shift, usePrimary, assignment, dayBellAdjustment: dayBellAdjustment);
            var timeline = dayColumns.Select(d => d.Column).ToList();
            var rows = rowBuilder(bandLessons, dayColumns, timeline);
            if (rows.Count == 0)
                continue;

            sections.Add(new DispatcherMonitorSection
            {
                Title = BandTitle(band),
                SubTitle = showShiftHeaders ? $"{shift} смена" : "",
                Columns = ToMonitorColumns(dayColumns, nowMinutes),
                Rows = rows
            });
        }
    }

    private static DispatcherMonitorSection BuildFilteredSection(
        string mode,
        List<LessonSlot> active,
        IReadOnlyList<Teacher> teachers,
        IReadOnlyList<Room> rooms,
        IReadOnlyDictionary<string, string> buildingColors,
        IReadOnlyList<BellPeriod> bells,
        int? nowMinutes,
        BellTemplateAssignmentSnapshot assignment,
        DateOnly? monitorDate,
        IReadOnlyDictionary<int, IReadOnlyList<TeacherUnavailability>> unavailByTeacher,
        DayBellAdjustment? dayBellAdjustment = null)
    {
        if (active.Count == 0)
        {
            var fallback = BuildSectionTimelineColumns([], bells, 5, 1, usePrimaryTimeline: false, assignment, dayBellAdjustment: dayBellAdjustment);
            return new DispatcherMonitorSection
            {
                Title = "Расписание",
                Columns = ToMonitorColumns(fallback, nowMinutes),
                Rows = []
            };
        }

        var sample = active[0];
        var band = ClassifyBand(sample, bells);
        var (profileGrade, usePrimary) = ResolveBandProfile(band, active);
        var dayColumns = BuildSectionTimelineColumns(active, bells, profileGrade, sample.ClassShift, usePrimary, assignment, dayBellAdjustment: dayBellAdjustment);
        var timeline = dayColumns.Select(d => d.Column).ToList();
        var rows = mode switch
        {
            DispatcherMonitorModes.Classes => BuildClassRows(mode, active, dayColumns, timeline, buildingColors, bells, dayBellAdjustment),
            DispatcherMonitorModes.Buildings => BuildRoomRows(mode, active, dayColumns, timeline, rooms, buildingColors, bells),
            _ => BuildTeacherRows(mode, active, dayColumns, timeline, teachers, buildingColors, bells, assignment,
                monitorDate: monitorDate, unavailByTeacher: unavailByTeacher)
        };

        var title = mode switch
        {
            DispatcherMonitorModes.Classes => active.Select(l => l.ClassName).Distinct().Count() == 1
                ? active[0].ClassName
                : "Классы",
            DispatcherMonitorModes.Buildings => string.IsNullOrWhiteSpace(sample.BuildingName)
                ? "Кабинеты"
                : sample.BuildingName,
            _ => teachers.FirstOrDefault(t => t.Id == sample.TeacherId)?.FullName ?? "Педагог"
        };

        return new DispatcherMonitorSection
        {
            Title = title,
            SubTitle = sample.ClassShift > 1 ? $"{sample.ClassShift} смена" : "",
            Columns = ToMonitorColumns(dayColumns, nowMinutes),
            Rows = rows
        };
    }

    private static List<DispatcherMonitorSection> BuildFilteredBuildingSections(
        string mode,
        List<LessonSlot> active,
        IReadOnlyList<Teacher> teachers,
        IReadOnlyList<Room> rooms,
        IReadOnlyDictionary<string, string> buildingColors,
        IReadOnlyList<BellPeriod> bells,
        int? nowMinutes,
        BellTemplateAssignmentSnapshot assignment,
        DateOnly? monitorDate,
        IReadOnlyDictionary<int, IReadOnlyList<TeacherUnavailability>> unavailByTeacher,
        DayBellAdjustment? dayBellAdjustment)
    {
        if (mode != DispatcherMonitorModes.Buildings || active.Count == 0)
            return [BuildFilteredSection(mode, active, teachers, rooms, buildingColors, bells, nowMinutes, assignment, monitorDate, unavailByTeacher, dayBellAdjustment)];

        var shifts = active.Select(l => l.ClassShift).Distinct().OrderBy(s => s).ToList();
        if (shifts.Count <= 1)
            return [BuildFilteredSection(mode, active, teachers, rooms, buildingColors, bells, nowMinutes, assignment, monitorDate, unavailByTeacher, dayBellAdjustment)];

        var sample = active[0];
        var dayColumns = BuildChronologicalMultiShiftTimelineColumns(active, bells, assignment, dayBellAdjustment);
        var timeline = dayColumns.Select(d => d.Column).ToList();
        var rows = BuildRoomRows(mode, active, dayColumns, timeline, rooms, buildingColors, bells);

        return
        [
            new DispatcherMonitorSection
            {
                Title = string.IsNullOrWhiteSpace(sample.BuildingName) ? "Кабинеты" : sample.BuildingName,
                SubTitle = string.Join(" · ", shifts.Select(s => $"{s} смена")),
                Columns = ToMonitorColumns(dayColumns, nowMinutes),
                Rows = rows
            }
        ];
    }

    private static List<TimelineColumnDef> BuildChronologicalMultiShiftTimelineColumns(
        IReadOnlyList<LessonSlot> lessons,
        IReadOnlyList<BellPeriod> bells,
        BellTemplateAssignmentSnapshot assignment,
        DayBellAdjustment? dayBellAdjustment)
    {
        var merged = new List<TimelineColumnDef>();
        foreach (var shift in lessons.Select(l => l.ClassShift).Distinct().OrderBy(s => s))
        {
            var shiftLessons = lessons.Where(l => l.ClassShift == shift).ToList();
            if (shiftLessons.Count == 0)
                continue;

            var band = ClassifyBand(shiftLessons[0], bells);
            var (profileGrade, usePrimary) = ResolveBandProfile(band, shiftLessons);
            merged.AddRange(BuildSectionTimelineColumns(
                shiftLessons, bells, profileGrade, shift, usePrimary, assignment, dayBellAdjustment: dayBellAdjustment));
        }

        return merged
            .OrderBy(ColumnOrderMinutes)
            .ThenBy(d => d.ClassShift)
            .ThenBy(d => d.Title, StringComparer.Ordinal)
            .ToList();
    }

    private static DispatcherMonitorSection CreateShiftHeader(int shift, string subTitle = "") =>
        new()
        {
            Title = $"{shift} смена",
            SubTitle = subTitle,
            IsShiftHeader = true
        };

    private static MonitorBand ClassifyBand(LessonSlot slot, IReadOnlyList<BellPeriod> bells)
    {
        if (slot.ClassGrade == ScheduleGridBuilder.FirstGradeTimelineGrade)
            return MonitorBand.FirstGrade;

        if (slot.ClassGrade is >= 2 and <= 4 && UsesPrimaryTimeline(bells, slot.ClassGrade, slot.ClassShift))
            return MonitorBand.JuniorPrimary;

        return MonitorBand.Standard;
    }

    private static (int ProfileGrade, bool UsePrimary) ResolveBandProfile(MonitorBand band, IReadOnlyList<LessonSlot> lessons) =>
        band switch
        {
            MonitorBand.FirstGrade => (ScheduleGridBuilder.FirstGradeTimelineGrade, true),
            MonitorBand.JuniorPrimary => (
                lessons.Where(l => l.ClassGrade is >= 2 and <= 4).Select(l => l.ClassGrade).DefaultIfEmpty(2).Min(),
                true),
            _ => (
                lessons.Select(l => l.ClassGrade).DefaultIfEmpty(5).Min(),
                false)
        };

    private static string BandTitle(MonitorBand band) => band switch
    {
        MonitorBand.FirstGrade => "1 класс · звонки и дин. пауза между уроками",
        MonitorBand.JuniorPrimary => "Начальная школа 2–4 · звонки и дин. пауза",
        _ => "2–11 классы · стандартная сетка уроков"
    };

    private static List<DispatcherMonitorColumn> ToMonitorColumns(
        IReadOnlyList<TimelineColumnDef> dayColumns,
        int? nowMinutes) =>
        dayColumns
            .Select((def, index) =>
            {
                var prevShift = index > 0 ? dayColumns[index - 1].ClassShift : def.ClassShift;
                return new DispatcherMonitorColumn
                {
                    Index = index,
                    Number = def.Column.IsDynamicPause ? 0 : def.Column.LessonNumber,
                    Title = def.Title,
                    Time = def.TimeDisplay,
                    ColumnKind = def.ColumnKind,
                    StartMinutes = def.StartMinutes,
                    EndMinutes = def.EndMinutes,
                    IsNow = nowMinutes is int nm && nm >= def.StartMinutes && nm < def.EndMinutes,
                    ClassShift = def.ClassShift,
                    IsShiftBoundary = def.ClassShift >= 2 && prevShift < def.ClassShift
                };
            })
            .ToList();

    private static List<TimelineColumnDef> BuildSectionTimelineColumns(
        IReadOnlyList<LessonSlot> sectionLessons,
        IReadOnlyList<BellPeriod> bells,
        int profileGrade,
        int shift,
        bool usePrimaryTimeline,
        BellTemplateAssignmentSnapshot assignment,
        string? sectionTemplateName = null,
        DayBellAdjustment? dayBellAdjustment = null)
    {
        if (sectionLessons.Count == 0)
            return BuildFallbackTimeline(bells, profileGrade, shift, sectionTemplateName);

        var merged = new Dictionary<string, TimelineColumnDef>(StringComparer.Ordinal);
        var templateName = sectionTemplateName ?? assignment.ResolveShiftStandardTemplateName(shift);

        if (dayBellAdjustment is { IsEmpty: false })
        {
            var timeline = DayBellAdjuster.BuildAdjustedTimeline(
                bells, profileGrade, shift, dayBellAdjustment, usePrimaryTimeline ? null : templateName);
            foreach (var column in timeline)
                MergeTimelineColumn(merged, MapColumnDef(column, bells, profileGrade, shift));
        }
        else if (usePrimaryTimeline)
        {
            var primaryTemplateName = sectionTemplateName;
            if (profileGrade == ScheduleGridBuilder.FirstGradeTimelineGrade
                && string.IsNullOrWhiteSpace(primaryTemplateName))
            {
                var sample = sectionLessons.FirstOrDefault(l =>
                    l.ClassGrade == ScheduleGridBuilder.FirstGradeTimelineGrade);
                if (sample is not null)
                {
                    primaryTemplateName = !string.IsNullOrWhiteSpace(sample.BellTemplateName)
                        ? sample.BellTemplateName
                        : assignment.GetTemplateName(sample.ClassId, sample.ClassGrade, sample.ClassShift);
                }
            }

            var timeline = BellScheduleResolver.BuildPrimaryTimeline(
                bells, profileGrade, shift, includeBreaks: true, primaryTemplateName);
            foreach (var column in timeline)
            {
                MergeTimelineColumn(merged, MapColumnDef(column, bells, profileGrade, shift));
            }

            foreach (var lessonNumber in sectionLessons
                         .Where(l => l.ClassGrade >= 5 && !SubjectScheduleRules.IsDynamicPause(l.SubjectName))
                         .Select(l => l.LessonNumber)
                         .Distinct()
                         .OrderBy(n => n))
            {
                if (merged.Values.Any(d => !d.Column.IsDynamicPause && !d.Column.IsBreak && d.Column.LessonNumber == lessonNumber))
                    continue;

                var sample = sectionLessons.First(l => l.ClassGrade >= 5 && l.LessonNumber == lessonNumber);
                var period = BellScheduleResolver.FindLessonPeriod(bells, sample.ClassGrade, sample.ClassShift, lessonNumber);
                var column = new ConstructorTimelineColumn
                {
                    LessonNumber = lessonNumber,
                    StorageLessonNumber = lessonNumber,
                    Title = $"Урок {lessonNumber}",
                    BellTimeDisplay = BellScheduleResolver.FormatPeriodTime(period)
                };
                MergeTimelineColumn(merged, MapColumnDef(column, bells, sample.ClassGrade, sample.ClassShift));
            }
        }
        else
        {
            var templatePeriods = BellScheduleResolver.ResolveShiftBellPeriods(bells, shift, templateName);
            var timeline = BellScheduleResolver.BuildStandardLessonTimeline(
                templatePeriods,
                BellScheduleResolver.StandardBellLookupGrade,
                shift,
                8,
                includeBreaks: true,
                templateName);
            foreach (var column in timeline)
                MergeTimelineColumn(merged, MapColumnDef(column, templatePeriods, BellScheduleResolver.StandardBellLookupGrade, shift));
        }

        return merged.Values
            .GroupBy(TimelineMergeKey)
            .Select(PickPreferredColumn)
            .Select(def => MaybeEnrichWithResolvedTimes(def, sectionLessons, dayBellAdjustment))
            .Where(def => dayBellAdjustment?.SkipDynamicPause != true || !def.Column.IsDynamicPause)
            .OrderBy(ColumnOrderMinutes)
            .ThenBy(d => d.Title, StringComparer.Ordinal)
            .ToList();
    }

    private static TimelineColumnDef MaybeEnrichWithResolvedTimes(
        TimelineColumnDef def,
        IReadOnlyList<LessonSlot> lessons,
        DayBellAdjustment? dayBellAdjustment)
    {
        if (dayBellAdjustment is { IsEmpty: false }
            && !def.Column.IsBreak
            && !def.Column.IsDynamicPause)
            return def;

        return EnrichWithResolvedTimes(def, lessons);
    }

    private static TimelineColumnDef EnrichWithResolvedTimes(
        TimelineColumnDef def,
        IReadOnlyList<LessonSlot> lessons)
    {
        if (def.Column.IsBreak)
        {
            if (def.EndMinutes > def.StartMinutes)
                return def;

            var after = def.Column.AfterLessonNumber > 0 ? def.Column.AfterLessonNumber : def.Column.LessonNumber;
            var afterStorage = def.Column.StorageLessonNumber > 0 ? def.Column.StorageLessonNumber : after;
            var prevEnd = FindLessonEndTime(lessons, afterStorage);
            var nextStart = FindLessonStartTime(lessons, afterStorage + 1)
                ?? FindNextLessonStartTime(lessons, afterStorage);
            if (string.IsNullOrWhiteSpace(prevEnd) || string.IsNullOrWhiteSpace(nextStart))
                return def;

            return CloneTimelineDef(def, $"{prevEnd}–{nextStart}");
        }

        if (def.Column.IsDynamicPause)
            return def;

        var slot = FindLessonSlotForColumn(def.Column, lessons);
        return slot is null ? def : CloneTimelineDef(def, slot.TimeDisplay);
    }

    private static LessonSlot? FindLessonSlotForColumn(
        ConstructorTimelineColumn column,
        IReadOnlyList<LessonSlot> lessons)
    {
        if (column.IsBreak || column.IsDynamicPause)
            return null;

        var storageNumber = column.StorageLessonNumber > 0
            ? column.StorageLessonNumber
            : column.LessonNumber;

        return lessons.FirstOrDefault(l =>
            !l.IsCancelled
            && l.LessonNumber == storageNumber
            && !string.IsNullOrWhiteSpace(l.StartTime)
            && !string.IsNullOrWhiteSpace(l.EndTime));
    }

    private static string? FindLessonEndTime(IReadOnlyList<LessonSlot> lessons, int lessonNumber) =>
        lessons.FirstOrDefault(l => !l.IsCancelled && l.LessonNumber == lessonNumber)?.EndTime;

    private static string? FindLessonStartTime(IReadOnlyList<LessonSlot> lessons, int lessonNumber) =>
        lessons.FirstOrDefault(l => !l.IsCancelled && l.LessonNumber == lessonNumber)?.StartTime;

    private static string? FindNextLessonStartTime(IReadOnlyList<LessonSlot> lessons, int afterLessonNumber) =>
        lessons.Where(l => !l.IsCancelled && l.LessonNumber > afterLessonNumber && !SubjectScheduleRules.IsDynamicPause(l.SubjectName))
            .OrderBy(l => l.LessonNumber)
            .FirstOrDefault()?.StartTime;

    private static TimelineColumnDef CloneTimelineDef(TimelineColumnDef source, string timeDisplay)
    {
        var (start, end) = ParseMinutes(timeDisplay);
        return new TimelineColumnDef
        {
            Column = source.Column,
            ColumnKind = source.ColumnKind,
            Title = source.Title,
            TimeDisplay = timeDisplay,
            StartMinutes = start,
            EndMinutes = end,
            MergeKey = source.MergeKey,
            ClassShift = source.ClassShift
        };
    }

    private static void MergeTimelineColumn(Dictionary<string, TimelineColumnDef> merged, TimelineColumnDef def)
    {
        if (!merged.TryGetValue(def.MergeKey, out var existing))
        {
            merged[def.MergeKey] = def;
            return;
        }

        if (ShouldPreferTimelineColumn(def, existing))
            merged[def.MergeKey] = def;
    }

    private static bool ShouldPreferTimelineColumn(TimelineColumnDef candidate, TimelineColumnDef current)
    {
        var candidateHasTime = !string.IsNullOrWhiteSpace(candidate.TimeDisplay);
        var currentHasTime = !string.IsNullOrWhiteSpace(current.TimeDisplay);
        if (candidateHasTime != currentHasTime)
            return candidateHasTime;

        return candidate.StartMinutes > current.StartMinutes;
    }

    private static int ColumnOrderMinutes(TimelineColumnDef column)
    {
        if (column.StartMinutes > 0)
            return column.StartMinutes;

        if (column.Column.IsBreak)
            return column.Column.AfterLessonNumber * 1000 + 500;

        if (column.Column.IsDynamicPause)
            return column.Column.AfterLessonNumber * 1000 + 550;

        return column.Column.LessonNumber * 1000;
    }

    private static string TimelineMergeKey(TimelineColumnDef column)
    {
        if (column.ColumnKind == DispatcherMonitorCell.KindLesson)
        {
            return $"L|{column.Column.LessonNumber}|{column.Column.StorageLessonNumber}|{column.ClassShift}|{column.StartMinutes}|{column.EndMinutes}";
        }

        return column.StartMinutes > 0
            ? $"{column.StartMinutes}|{column.EndMinutes}|{column.ColumnKind}"
            : column.MergeKey;
    }

    private static int? ResolveNowMinutes(DateOnly? date)
    {
        if (date is null || date.Value != DateOnly.FromDateTime(DateTime.Now))
            return null;

        return (int)TimeOnly.FromDateTime(DateTime.Now).ToTimeSpan().TotalMinutes;
    }

    private sealed class TimelineColumnDef
    {
        public required ConstructorTimelineColumn Column { get; init; }
        public required string ColumnKind { get; init; }
        public required string Title { get; init; }
        public required string TimeDisplay { get; init; }
        public required int StartMinutes { get; init; }
        public required int EndMinutes { get; init; }
        public required string MergeKey { get; init; }
        public int ClassShift { get; init; }
    }

    private static TimelineColumnDef PickPreferredColumn(IEnumerable<TimelineColumnDef> group) =>
        group.OrderBy(d => string.IsNullOrWhiteSpace(d.TimeDisplay) ? 1 : 0)
            .ThenBy(d => d.ColumnKind switch
            {
                DispatcherMonitorCell.KindLesson => 0,
                DispatcherMonitorCell.KindDynamicPause => 1,
                DispatcherMonitorCell.KindBreak => 2,
                _ => 3
            }).First();

    private static List<TimelineColumnDef> BuildFallbackTimeline(
        IReadOnlyList<BellPeriod> bells,
        int grade,
        int shift,
        string? templateName = null)
    {
        var templatePeriods = BellScheduleResolver.ResolveShiftBellPeriods(bells, shift, templateName);
        var timeline = BellScheduleResolver.BuildStandardLessonTimeline(
            templatePeriods,
            BellScheduleResolver.StandardBellLookupGrade,
            shift,
            6,
            includeBreaks: true,
            templateName);
        return timeline.Select(c => MapColumnDef(c, templatePeriods, BellScheduleResolver.StandardBellLookupGrade, shift)).ToList();
    }

    private static List<ConstructorTimelineColumn> BuildEntityTimeline(
        IReadOnlyList<LessonSlot> slots,
        IReadOnlyList<BellPeriod> bells)
    {
        var merged = new List<ConstructorTimelineColumn>();
        foreach (var (grade, shift) in slots.Select(s => (s.ClassGrade, s.ClassShift)).Distinct())
        {
            string? templateName = null;
            if (grade == ScheduleGridBuilder.FirstGradeTimelineGrade)
            {
                var sample = slots.First(s => s.ClassGrade == grade && s.ClassShift == shift);
                templateName = sample.BellTemplateName;
            }

            var timeline = UsesPrimaryTimeline(bells, grade, shift)
                ? BellScheduleResolver.BuildPrimaryTimeline(bells, grade, shift, includeBreaks: true, templateName)
                : BellScheduleResolver.BuildStandardLessonTimeline(bells, grade, shift, 8, includeBreaks: true);
            foreach (var column in timeline)
            {
                if (merged.Any(existing => ColumnsMatch(existing, column)))
                    continue;
                merged.Add(column);
            }
        }

        return merged
            .OrderBy(c => ColumnSortMinutes(c, bells, slots))
            .ThenBy(c => c.LessonNumber)
            .ToList();
    }

    private static int ColumnSortMinutes(
        ConstructorTimelineColumn column,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyList<LessonSlot> slots)
    {
        var sample = slots.FirstOrDefault(s => s.ClassGrade is >= 1 and <= 11)
            ?? slots.FirstOrDefault();
        if (sample is null)
            return column.LessonNumber;

        var period = column.IsDynamicPause
            ? BellScheduleResolver.FindDynamicPauseAfterLesson(bells, sample.ClassGrade, sample.ClassShift, column.AfterLessonNumber)
            : column.IsBreak
                ? BellScheduleResolver.GetBreaksForGrade(bells, sample.ClassGrade, sample.ClassShift)
                    .FirstOrDefault(b => b.LessonNumber == column.AfterLessonNumber)
                : BellScheduleResolver.FindLessonPeriod(bells, sample.ClassGrade, sample.ClassShift, column.LessonNumber);

        if (period is not null && TryParseTime(period.StartTime, out var start))
            return start;

        return column.LessonNumber * 100;
    }

    private static bool ColumnsMatch(ConstructorTimelineColumn left, ConstructorTimelineColumn right)
    {
        if (left.IsBreak != right.IsBreak || left.IsDynamicPause != right.IsDynamicPause)
            return false;

        return left.IsBreak || left.IsDynamicPause
            ? left.AfterLessonNumber == right.AfterLessonNumber
            : left.LessonNumber == right.LessonNumber;
    }

    private static bool UsesPrimaryTimeline(IReadOnlyList<BellPeriod> bells, int grade, int shift) =>
        grade is >= 1 and <= 4
        && BellScheduleResolver.GetDynamicPausesForGrade(bells, grade, shift).Count > 0;

    private static TimelineColumnDef MapColumnDef(
        ConstructorTimelineColumn column,
        IReadOnlyList<BellPeriod> bells,
        int grade,
        int shift)
    {
        var kind = column.IsBreak
            ? DispatcherMonitorCell.KindBreak
            : column.IsDynamicPause
                ? DispatcherMonitorCell.KindDynamicPause
                : DispatcherMonitorCell.KindLesson;

        var title = column.IsBreak
            ? "Перемена"
            : column.IsDynamicPause
                ? "Дин. пауза"
                : $"Урок {column.LessonNumber}";

        var time = column.BellTimeDisplay;
        if (string.IsNullOrWhiteSpace(time))
        {
            var period = column.IsDynamicPause
                ? BellScheduleResolver.FindDynamicPauseAfterLesson(bells, grade, shift, column.AfterLessonNumber)
                : column.IsBreak
                    ? BellScheduleResolver.GetBreaksForGrade(bells, grade, shift)
                        .FirstOrDefault(b => b.LessonNumber == column.AfterLessonNumber)
                    : BellScheduleResolver.FindLessonPeriod(
                        bells,
                        grade,
                        shift,
                        ScheduleGridBuilder.ResolveColumnBellLessonNumber(column));
            time = BellScheduleResolver.FormatPeriodTime(period);
        }

        var (start, end) = ParseMinutes(time);
        var mergeKey = column.IsBreak
            ? $"S{shift}|B|{column.AfterLessonNumber}|{start}"
            : column.IsDynamicPause
                ? $"S{shift}|P|{column.AfterLessonNumber}|{start}"
                : $"S{shift}|L|{column.LessonNumber}|{start}";

        return new TimelineColumnDef
        {
            Column = column,
            ColumnKind = kind,
            Title = title,
            TimeDisplay = time,
            StartMinutes = start,
            EndMinutes = end > start ? end : start + 45,
            MergeKey = mergeKey,
            ClassShift = shift
        };
    }

    private static (int Start, int End) ParseMinutes(string timeDisplay)
    {
        if (string.IsNullOrWhiteSpace(timeDisplay))
            return (0, 0);

        var parts = timeDisplay.Split('–', '-', '—');
        if (parts.Length == 2
            && TryParseTime(parts[0], out var start)
            && TryParseTime(parts[1], out var end))
            return (start, end);

        return TryParseTime(timeDisplay, out var single) ? (single, single + 40) : (0, 0);
    }

    private static bool TryParseTime(string value, out int minutes)
    {
        minutes = 0;
        if (!TimeSpan.TryParse(value.Trim(), out var ts))
            return false;
        minutes = (int)ts.TotalMinutes;
        return true;
    }

    private static List<DispatcherMonitorRow> BuildTeacherRows(
        string mode,
        List<LessonSlot> active,
        IReadOnlyList<TimelineColumnDef> dayColumns,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        IReadOnlyList<Teacher> teachers,
        IReadOnlyDictionary<string, string> buildingColors,
        IReadOnlyList<BellPeriod> bells,
        BellTemplateAssignmentSnapshot assignment,
        IReadOnlyDictionary<int, LessonSlot>? priorShiftTails = null,
        DateOnly? monitorDate = null,
        IReadOnlyDictionary<int, IReadOnlyList<TeacherUnavailability>>? unavailByTeacher = null)
    {
        var lookup = BuildTeacherLookup(active);
        var ids = active.Select(l => l.TeacherId).Distinct().OrderBy(id =>
            teachers.FirstOrDefault(t => t.Id == id)?.FullName ?? "").ToList();

        var rows = new List<DispatcherMonitorRow>(ids.Count);
        foreach (var id in ids)
        {
            var teacher = teachers.FirstOrDefault(t => t.Id == id);
            var rowLessons = active.Where(l => l.TeacherId == id).ToList();
            LessonSlot? priorShiftLesson = null;
            priorShiftTails?.TryGetValue(id, out priorShiftLesson);
            var teacherTimeline = TeacherTimelineBuilder.BuildColumns(
                id, active, bells, includeBreaks: true, assignment: assignment);
            var interShiftTransition = priorShiftLesson is not null
                ? BuildBuildingTransition(
                    priorShiftLesson,
                    rowLessons.Where(l => !l.IsCancelled).OrderBy(GetChronologicalSortKey).FirstOrDefault())
                : "";
            var subject = teacher?.PrimarySubject ?? "";
            var unavailHint = BuildTeacherUnavailabilityRowHint(id, monitorDate, unavailByTeacher);
            var subLabel = ComposeTeacherSubLabel(subject, interShiftTransition, unavailHint);
            rows.Add(new DispatcherMonitorRow
            {
                Label = teacher?.FullName ?? $"ID {id}",
                SubLabel = subLabel,
                Cells = BuildRowCells(
                    mode, dayColumns, timeline, teacherTimeline, rowLessons, lookup, id,
                    buildingColors, bells, treatGapsAsWindows: true, priorShiftLesson: priorShiftLesson,
                    monitorDate: monitorDate, unavailByTeacher: unavailByTeacher)
            });
        }

        return rows;
    }

    private static string ComposeTeacherSubLabel(string subject, string interShiftTransition, string unavailHint)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(subject))
            parts.Add(subject);
        if (!string.IsNullOrWhiteSpace(interShiftTransition))
            parts.Add(interShiftTransition);
        if (!string.IsNullOrWhiteSpace(unavailHint))
            parts.Add(unavailHint);
        return string.Join(" · ", parts);
    }

    private static string BuildTeacherUnavailabilityRowHint(
        int teacherId,
        DateOnly? monitorDate,
        IReadOnlyDictionary<int, IReadOnlyList<TeacherUnavailability>>? unavailByTeacher)
    {
        if (monitorDate is not DateOnly date
            || unavailByTeacher is null
            || !unavailByTeacher.TryGetValue(teacherId, out var blocks))
            return "";

        var active = TeacherUnavailabilityResolver.FilterForDate(blocks, date);
        if (active.Count == 0)
            return "";

        var allDay = active.FirstOrDefault(b => b.AllDay);
        if (allDay is not null)
            return $"нерабочее: {TeacherUnavailabilityResolver.FormatDispatcherPrimary(allDay)}";

        return active.Count == 1
            ? $"нерабочее: {TeacherUnavailabilityResolver.FormatDispatcherSecondary(active[0])}"
            : $"нерабочее: {active.Count} интервала";
    }

    private static List<DispatcherMonitorRow> BuildClassRows(
        string mode,
        List<LessonSlot> active,
        IReadOnlyList<TimelineColumnDef> dayColumns,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        IReadOnlyDictionary<string, string> buildingColors,
        IReadOnlyList<BellPeriod> bells,
        DayBellAdjustment? dayBellAdjustment = null)
    {
        var rows = new List<DispatcherMonitorRow>();
        foreach (var group in active.GroupBy(l => l.ClassId).OrderBy(g => g.First().ClassName))
        {
            var sample = group.First();
            var classTimeline = timeline;

            var lookup = BuildClassLookup(group.ToList());
            rows.Add(new DispatcherMonitorRow
            {
                ClassId = sample.ClassId,
                Label = sample.ClassName,
                SubLabel = $"{sample.ClassShift} смена",
                Cells = BuildRowCells(
                    mode, dayColumns, timeline, classTimeline, group.ToList(), lookup, sample.ClassId,
                    buildingColors, bells, treatGapsAsWindows: false, isClassRow: true)
            });
        }

        return rows;
    }


    private static List<DispatcherMonitorRow> BuildRoomRows(
        string mode,
        List<LessonSlot> active,
        IReadOnlyList<TimelineColumnDef> dayColumns,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        IReadOnlyList<Room> rooms,
        IReadOnlyDictionary<string, string> buildingColors,
        IReadOnlyList<BellPeriod> bells)
    {
        var rows = new List<DispatcherMonitorRow>();
        foreach (var group in active.GroupBy(l => l.RoomId)
                     .OrderBy(g => g.First().BuildingName).ThenBy(g => g.First().RoomNumber))
        {
            var sample = group.First();
            var lookup = BuildRoomLookup(group.ToList());
            rows.Add(new DispatcherMonitorRow
            {
                Label = sample.RoomId > 0 && !string.IsNullOrWhiteSpace(sample.RoomNumber)
                    ? $"каб. {sample.RoomNumber}"
                    : "прогулка",
                SubLabel = sample.RoomId > 0 ? sample.BuildingName : "дин. пауза без кабинета",
                BuildingColorHex = ResolveBuildingColor(sample.BuildingName, buildingColors),
                Cells = BuildRowCells(
                    mode, dayColumns, timeline, timeline, group.ToList(), lookup, sample.RoomId,
                    buildingColors, bells, treatGapsAsWindows: false, isRoomRow: true)
            });
        }

        return rows;
    }

    private static Dictionary<(int TeacherId, int DayOfWeek, int LessonNumber), List<LessonSlot>> BuildTeacherLookup(
        IReadOnlyList<LessonSlot> lessons) =>
        lessons.GroupBy(l => (l.TeacherId, l.DayOfWeek, l.LessonNumber))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubgroupIndex).ToList());

    private static Dictionary<(int ClassId, int DayOfWeek, int LessonNumber), List<LessonSlot>> BuildClassLookup(
        IReadOnlyList<LessonSlot> lessons) =>
        lessons.GroupBy(l => (l.ClassId, l.DayOfWeek, l.LessonNumber))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubgroupIndex).ToList());

    private static Dictionary<(int RoomId, int DayOfWeek, int LessonNumber, int ClassShift), List<LessonSlot>> BuildRoomLookup(
        IReadOnlyList<LessonSlot> lessons) =>
        lessons.GroupBy(l => (l.RoomId, l.DayOfWeek, l.LessonNumber, l.ClassShift))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubgroupIndex).ToList());

    private static List<DispatcherMonitorCell> BuildRowCells(
        string mode,
        IReadOnlyList<TimelineColumnDef> dayColumns,
        IReadOnlyList<ConstructorTimelineColumn> dayTimeline,
        IReadOnlyList<ConstructorTimelineColumn> rowTimeline,
        IReadOnlyList<LessonSlot> rowLessons,
        object lookup,
        int entityId,
        IReadOnlyDictionary<string, string> buildingColors,
        IReadOnlyList<BellPeriod> bells,
        bool treatGapsAsWindows,
        bool isClassRow = false,
        bool isRoomRow = false,
        LessonSlot? priorShiftLesson = null,
        DateOnly? monitorDate = null,
        IReadOnlyDictionary<int, IReadOnlyList<TeacherUnavailability>>? unavailByTeacher = null)
    {
        var day = rowLessons.FirstOrDefault()?.DayOfWeek ?? 1;
        var cells = new List<DispatcherMonitorCell>(dayColumns.Count);
        var transitionByColumn = treatGapsAsWindows
            ? BuildImmediateGapTransitions(
                dayColumns, dayTimeline, rowTimeline, rowLessons, lookup, entityId, bells, priorShiftLesson)
            : null;

        for (var index = 0; index < dayColumns.Count; index++)
        {
            var def = dayColumns[index];
            if (def.ColumnKind == DispatcherMonitorCell.KindBreak)
            {
                if (treatGapsAsWindows)
                {
                    cells.Add(BuildTeacherBreakCell(
                        index, def, rowLessons, bells, entityId, priorShiftLesson, monitorDate, unavailByTeacher, transitionByColumn));
                    continue;
                }

                if (!RowUsesColumn(rowTimeline, def.Column, def.ClassShift, rowLessons))
                {
                    cells.Add(BuildBreakOrPauseCell(index, def, isGlobalBreak: true));
                    continue;
                }
            }

            if (def.ColumnKind == DispatcherMonitorCell.KindDynamicPause
                && !RowUsesColumn(rowTimeline, def.Column, def.ClassShift, rowLessons))
            {
                cells.Add(BuildBreakOrPauseCell(index, def, isGlobalBreak: false));
                continue;
            }

            if (!RowUsesColumn(rowTimeline, def.Column, def.ClassShift, rowLessons))
            {
                cells.Add(BuildEmptyCell(index, def, isClassRow ? entityId : 0));
                continue;
            }

            var slots = ResolveSlots(lookup, entityId, day, def, dayTimeline, isClassRow, isRoomRow);
            if (slots is null || slots.Count == 0)
            {
                cells.Add(treatGapsAsWindows
                    ? BuildTeacherGapCell(index, def, rowLessons, bells, entityId, priorShiftLesson, monitorDate, unavailByTeacher, transitionByColumn, isBreak: false)
                    : BuildEmptyCell(index, def, isClassRow ? entityId : 0));
                continue;
            }

            cells.Add(BuildLessonCell(mode, index, def, slots, buildingColors, rowLessons, bells));
        }

        return cells;
    }

    private static bool RowUsesColumn(
        IReadOnlyList<ConstructorTimelineColumn> rowTimeline,
        ConstructorTimelineColumn column,
        int columnShift = 0,
        IReadOnlyList<LessonSlot>? rowLessons = null)
    {
        if (columnShift > 0 && rowLessons is not null && !rowLessons.Any(l => l.ClassShift == columnShift))
            return false;

        if (column.IsBreak)
            return rowTimeline.Any(c => c.IsBreak && c.AfterLessonNumber == column.AfterLessonNumber);

        if (column.IsDynamicPause)
            return rowTimeline.Any(c => c.IsDynamicPause && c.AfterLessonNumber == column.AfterLessonNumber);

        return rowTimeline.Any(c => !c.IsDynamicPause && !c.IsBreak && c.LessonNumber == column.LessonNumber);
    }

    private static List<LessonSlot>? ResolveSlots(
        object lookup,
        int entityId,
        int day,
        TimelineColumnDef def,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        bool isClassRow,
        bool isRoomRow)
    {
        var column = def.Column;
        if (isClassRow
            && lookup is Dictionary<(int ClassId, int DayOfWeek, int LessonNumber), List<LessonSlot>> classLookup)
            return ScheduleGridBuilder.ResolveClassTimelineSlots(entityId, day, column, timeline, classLookup);

        if (isRoomRow
            && lookup is Dictionary<(int RoomId, int DayOfWeek, int LessonNumber, int ClassShift), List<LessonSlot>> roomLookup)
            return ResolveRoomTimelineSlots(entityId, day, def.ClassShift, column, timeline, roomLookup);

        if (lookup is Dictionary<(int TeacherId, int DayOfWeek, int LessonNumber), List<LessonSlot>> teacherLookup)
            return ScheduleGridBuilder.ResolveTeacherTimelineSlots(entityId, day, column, timeline, teacherLookup);

        return null;
    }

    private static List<LessonSlot>? ResolveRoomTimelineSlots(
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

        foreach (var lessonNumber in ScheduleGridBuilder.ResolveLessonStorageNumbers(column, timeline))
        {
            if (!lookup.TryGetValue((roomId, dayOfWeek, lessonNumber, classShift), out var group))
                continue;

            var regular = group.Where(s => !SubjectScheduleRules.IsDynamicPause(s.SubjectName)).ToList();
            if (regular.Count > 0)
                return regular;
        }

        return null;
    }

    private static DispatcherMonitorCell BuildLessonCell(
        string mode,
        int index,
        TimelineColumnDef def,
        IReadOnlyList<LessonSlot> slots,
        IReadOnlyDictionary<string, string> buildingColors,
        IReadOnlyList<LessonSlot> rowLessons,
        IReadOnlyList<BellPeriod> bells)
    {
        var sample = slots[0];
        var (primary, secondary, statusLine) = FormatCellLines(mode, slots, rowLessons, bells, def);

        return new DispatcherMonitorCell
        {
            ColumnIndex = index,
            ClassId = sample.ClassId,
            ColumnClassShift = def.ClassShift,
            LessonNumber = sample.LessonNumber,
            ColumnKind = def.ColumnKind,
            TimeLabel = sample.TimeDisplay,
            HasLesson = true,
            PrimaryLine = primary,
            SecondaryLine = secondary,
            StatusLine = statusLine,
            BuildingColorHex = ResolveBuildingColor(sample.BuildingName, buildingColors),
            StatusKind = ResolveStatus(slots),
            Lesson = sample,
            ToolTip = BuildToolTip(def, primary, secondary, statusLine, slots)
        };
    }

    private static (string Primary, string Secondary, string Status) FormatCellLines(
        string mode,
        IReadOnlyList<LessonSlot> slots,
        IReadOnlyList<LessonSlot> rowLessons,
        IReadOnlyList<BellPeriod> bells,
        TimelineColumnDef def)
    {
        var sample = slots[0];
        if (def.ColumnKind == DispatcherMonitorCell.KindDynamicPause
            || SubjectScheduleRules.IsDynamicPause(sample.SubjectName))
            return FormatDynamicPauseCell(mode, slots, rowLessons, bells);

        return mode switch
        {
            DispatcherMonitorModes.Classes => (
                FormatSubjectLine(slots),
                slots.Count == 1
                    ? $"{FormatTeacherNameOnly(sample)} · {FormatRoomLine(sample)}"
                    : string.Join(" / ", slots.Select(s => $"{FormatTeacherNameOnly(s)} · {FormatRoomLine(s)}")),
                AppendTransition(BuildReplacementLine(sample), sample, rowLessons, bells)),

            DispatcherMonitorModes.Buildings => (
                FormatClassSubjectLine(slots),
                slots.Count == 1
                    ? FormatTeacherNameOnly(sample)
                    : string.Join(" / ", slots.Select(FormatTeacherNameOnly)),
                AppendTransition(BuildReplacementLine(sample), sample, rowLessons, bells)),

            _ => (
                FormatClassSubjectLine(slots),
                FormatRoomLine(sample),
                BuildStatusLine(sample))
        };
    }

    private static string CombineSecondary(string room, string teacher)
    {
        if (string.IsNullOrWhiteSpace(room))
            return teacher;
        if (string.IsNullOrWhiteSpace(teacher))
            return room;
        return $"{room} · {teacher}";
    }

    private static (string Primary, string Secondary, string Status) FormatDynamicPauseCell(
        string mode,
        IReadOnlyList<LessonSlot> slots,
        IReadOnlyList<LessonSlot> rowLessons,
        IReadOnlyList<BellPeriod> bells)
    {
        if (slots.Count == 1)
            return FormatSingleDynamicPauseCell(mode, slots[0], rowLessons, bells);

        var separator = slots.Count <= 2 ? "\n" : " / ";
        var primary = string.Join(separator, slots.Select(FormatDynamicPausePrimaryLine));
        var secondary = mode switch
        {
            DispatcherMonitorModes.Classes => string.Join(separator, slots.Select(FormatTeacherNameOnly)),
            DispatcherMonitorModes.Buildings => string.Join(separator, slots.Select(s =>
                CombineSecondary(FormatPauseActivityName(s), FormatTeacherNameOnly(s)))),
            _ => string.Join(separator, slots.Select(FormatPauseActivityName).Where(s => !string.IsNullOrWhiteSpace(s)))
        };

        var statusParts = slots
            .Select(s => mode == DispatcherMonitorModes.Teachers
                ? BuildStatusLine(s)
                : AppendTransition(BuildReplacementLine(s), s, rowLessons, bells))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var status = string.Join(separator, statusParts);

        return (primary, secondary, status);
    }

    private static (string Primary, string Secondary, string Status) FormatSingleDynamicPauseCell(
        string mode,
        LessonSlot sample,
        IReadOnlyList<LessonSlot> rowLessons,
        IReadOnlyList<BellPeriod> bells)
    {
        var activity = FormatPauseActivityName(sample);
        var primary = FormatDynamicPausePrimaryLine(sample);
        var secondary = mode switch
        {
            DispatcherMonitorModes.Classes => FormatTeacherNameOnly(sample),
            DispatcherMonitorModes.Buildings => CombineSecondary(activity, FormatTeacherNameOnly(sample)),
            _ => activity
        };

        var status = mode == DispatcherMonitorModes.Teachers
            ? BuildStatusLine(sample)
            : AppendTransition(BuildReplacementLine(sample), sample, rowLessons, bells);
        return (primary, secondary, status);
    }

    private static string FormatDynamicPausePrimaryLine(LessonSlot slot) =>
        string.IsNullOrWhiteSpace(slot.ClassName) ? "дин. пауза" : $"{slot.ClassName} · дин. пауза";

    private static string FormatPauseActivityName(LessonSlot slot)
    {
        if (string.IsNullOrWhiteSpace(slot.SubjectName)
            || SubjectScheduleRules.IsDynamicPause(slot.SubjectName))
            return "";

        return slot.SubjectName.Trim();
    }

    private static string FormatTeacherNameOnly(LessonSlot slot)
    {
        if (slot.HasAssignedReplacement && !string.IsNullOrWhiteSpace(slot.ReplacementTeacherName))
            return CompactPersonName(slot.ReplacementTeacherName);

        return CompactPersonName(slot.TeacherName);
    }

    private static string FormatRoomLine(LessonSlot slot) =>
        slot.RoomId > 0 && !string.IsNullOrWhiteSpace(slot.RoomNumber)
            ? $"каб.{slot.RoomNumber}"
            : "без каб.";

    private static string FormatClassSubjectLine(IReadOnlyList<LessonSlot> slots) =>
        slots.Count == 1
            ? FormatSingleClassSubject(slots[0])
            : string.Join(" / ", slots.Select(s =>
                s.SubgroupIndex > 0
                    ? $"{FormatSingleClassSubject(s)} ({s.SubgroupIndex + 1})"
                    : FormatSingleClassSubject(s)));

    private static string FormatSingleClassSubject(LessonSlot slot)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(slot.ClassName))
            parts.Add(slot.ClassName);
        if (!string.IsNullOrWhiteSpace(slot.SubjectName)
            && !SubjectScheduleRules.IsDynamicPause(slot.SubjectName))
            parts.Add(slot.SubjectName);

        if (parts.Count > 0)
            return string.Join(" · ", parts);

        return SubjectScheduleRules.IsDynamicPause(slot.SubjectName) ? "дин. пауза" : $"урок {slot.LessonNumber}";
    }

    private static string FormatSubjectLine(IReadOnlyList<LessonSlot> slots) =>
        slots.Count == 1
            ? FormatSingleSubject(slots[0])
            : string.Join(" / ", slots.Select(s =>
                s.SubgroupIndex > 0 ? $"{FormatSingleSubject(s)} ({s.SubgroupIndex + 1})" : FormatSingleSubject(s)));

    private static string FormatSingleSubject(LessonSlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.SubjectName))
            return slot.SubjectName;

        return SubjectScheduleRules.IsDynamicPause(slot.SubjectName) ? "дин. пауза" : $"урок {slot.LessonNumber}";
    }

    private static string BuildReplacementLine(LessonSlot slot)
    {
        if (slot.NeedsReplacement)
            return StaffStatusTypes.FormatPendingStatus(
                slot.AbsenceNote,
                $"нужна замена: {CompactPersonName(slot.TeacherName)}");

        if (slot.HasAssignedReplacement && !string.IsNullOrWhiteSpace(slot.ReplacementTeacherName))
        {
            var replacement = CompactPersonName(slot.ReplacementTeacherName);
            var original = CompactPersonName(slot.TeacherName);
            return string.IsNullOrWhiteSpace(original)
                ? $"→ {replacement}"
                : $"{original} → {replacement}";
        }

        return "";
    }

    private static string CompactPersonName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "";

        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            >= 3 => $"{parts[0]} {parts[1][0]}.{parts[2][0]}.",
            2 => $"{parts[0]} {parts[1][0]}.",
            _ => parts[0]
        };
    }

    private static string AppendTransition(
        string statusLine,
        LessonSlot slot,
        IReadOnlyList<LessonSlot> rowLessons,
        IReadOnlyList<BellPeriod> bells)
    {
        var transition = BuildBuildingTransition(
            rowLessons
                .Where(l => l.TeacherId == slot.TeacherId && !l.IsCancelled)
                .Where(l => IsChronologicallyBefore(l, slot))
                .OrderByDescending(GetChronologicalSortKey)
                .FirstOrDefault(),
            slot);
        if (string.IsNullOrWhiteSpace(transition))
            return statusLine;

        return string.IsNullOrWhiteSpace(statusLine) ? transition : $"{statusLine} · {transition}";
    }

    private static DispatcherMonitorCell BuildTeacherBreakCell(
        int index,
        TimelineColumnDef def,
        IReadOnlyList<LessonSlot> rowLessons,
        IReadOnlyList<BellPeriod> bells,
        int teacherId,
        LessonSlot? priorShiftLesson = null,
        DateOnly? monitorDate = null,
        IReadOnlyDictionary<int, IReadOnlyList<TeacherUnavailability>>? unavailByTeacher = null,
        IReadOnlyDictionary<int, string>? transitionByColumn = null) =>
        BuildTeacherGapCell(index, def, rowLessons, bells, teacherId, priorShiftLesson, monitorDate, unavailByTeacher, transitionByColumn, isBreak: true);

    private static DispatcherMonitorCell BuildTeacherGapCell(
        int index,
        TimelineColumnDef def,
        IReadOnlyList<LessonSlot> rowLessons,
        IReadOnlyList<BellPeriod> bells,
        int teacherId,
        LessonSlot? priorShiftLesson,
        DateOnly? monitorDate,
        IReadOnlyDictionary<int, IReadOnlyList<TeacherUnavailability>>? unavailByTeacher,
        IReadOnlyDictionary<int, string>? transitionByColumn,
        bool isBreak)
    {
        var transition = transitionByColumn?.GetValueOrDefault(index) ?? "";
        var hasTransition = !string.IsNullOrWhiteSpace(transition);
        var unavailBlock = ResolveUnavailabilityForColumn(teacherId, monitorDate, unavailByTeacher, def);

        if (unavailBlock is not null)
        {
            return new()
            {
                ColumnIndex = index,
                ColumnClassShift = def.ClassShift,
                ColumnKind = isBreak ? DispatcherMonitorCell.KindBreak : DispatcherMonitorCell.KindWindow,
                TimeLabel = def.TimeDisplay,
                IsWindow = !isBreak,
                PrimaryLine = TeacherUnavailabilityResolver.FormatDispatcherPrimary(unavailBlock),
                SecondaryLine = TeacherUnavailabilityResolver.FormatDispatcherSecondary(unavailBlock),
                StatusLine = transition,
                StatusKind = hasTransition ? "Transition" : "Unavailability",
                ToolTip = BuildUnavailabilityToolTip(
                    def, unavailBlock, transition, isBreak ? null : "Свободное время между уроками")
            };
        }

        if (isBreak)
        {
            return new()
            {
                ColumnIndex = index,
                ColumnClassShift = def.ClassShift,
                ColumnKind = DispatcherMonitorCell.KindBreak,
                TimeLabel = def.TimeDisplay,
                PrimaryLine = def.Title,
                SecondaryLine = def.TimeDisplay,
                StatusLine = transition,
                StatusKind = hasTransition ? "Transition" : "Normal",
                ToolTip = BuildGapToolTip(def, transition)
            };
        }

        return new()
        {
            ColumnIndex = index,
            ColumnClassShift = def.ClassShift,
            ColumnKind = DispatcherMonitorCell.KindWindow,
            TimeLabel = def.TimeDisplay,
            IsWindow = true,
            PrimaryLine = "ОКНО",
            SecondaryLine = "",
            StatusLine = transition,
            StatusKind = hasTransition ? "Transition" : "Normal",
            ToolTip = BuildGapToolTip(def, transition, "Свободное время между уроками")
        };
    }

    private static TeacherUnavailability? ResolveUnavailabilityForColumn(
        int teacherId,
        DateOnly? monitorDate,
        IReadOnlyDictionary<int, IReadOnlyList<TeacherUnavailability>>? unavailByTeacher,
        TimelineColumnDef def)
    {
        if (monitorDate is not DateOnly date
            || unavailByTeacher is null
            || !unavailByTeacher.TryGetValue(teacherId, out var blocks))
            return null;

        return TeacherUnavailabilityResolver.FindForColumn(blocks, date, def.ColumnKind, def.Column);
    }

    private static string BuildUnavailabilityToolTip(
        TimelineColumnDef def,
        TeacherUnavailability block,
        string transition,
        string? suffix)
    {
        var lines = new List<string>
        {
            TeacherUnavailabilityResolver.FormatDispatcherToolTip(block),
            def.Title,
            def.TimeDisplay
        };
        if (!string.IsNullOrWhiteSpace(transition))
            lines.Add(transition);
        if (!string.IsNullOrWhiteSpace(suffix))
            lines.Add(suffix);
        return string.Join('\n', lines.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private static string BuildGapToolTip(TimelineColumnDef def, string transition, string? suffix = null)
    {
        var lines = new List<string> { def.Title, def.TimeDisplay };
        if (!string.IsNullOrWhiteSpace(transition))
            lines.Add(transition);
        if (!string.IsNullOrWhiteSpace(suffix))
            lines.Add(suffix);
        return string.Join('\n', lines);
    }

    private static DispatcherMonitorCell BuildEmptyCell(int index, TimelineColumnDef def, int classId = 0)
    {
        var slotNumber = ResolveSlotLessonNumber(def.Column);
        return new()
        {
            ColumnIndex = index,
            ClassId = classId,
            ColumnClassShift = def.ClassShift,
            LessonNumber = slotNumber,
            ColumnKind = DispatcherMonitorCell.KindEmpty,
            TimeLabel = def.TimeDisplay,
            PrimaryLine = "—",
            ToolTip = classId > 0 && slotNumber > 0
                ? $"Урок {def.Column.LessonNumber} · {def.TimeDisplay} · пусто"
                : $"{def.Title} · {def.TimeDisplay}"
        };
    }

    private static int ResolveSlotLessonNumber(ConstructorTimelineColumn column)
    {
        if (column.IsBreak || column.IsDynamicPause)
            return 0;

        return column.StorageLessonNumber > 0
            ? column.StorageLessonNumber
            : column.LessonNumber;
    }

    private static DispatcherMonitorCell BuildBreakOrPauseCell(int index, TimelineColumnDef def, bool isGlobalBreak) =>
        new()
        {
            ColumnIndex = index,
            ColumnClassShift = def.ClassShift,
            ColumnKind = def.ColumnKind,
            TimeLabel = def.TimeDisplay,
            PrimaryLine = def.Title,
            SecondaryLine = def.TimeDisplay,
            ToolTip = $"{def.Title} · {def.TimeDisplay}"
        };

    private static string BuildStatusLine(LessonSlot slot)
    {
        if (slot.NeedsReplacement)
            return StaffStatusTypes.FormatPendingStatus(slot.AbsenceNote);
        if (slot.HasAssignedReplacement && !string.IsNullOrWhiteSpace(slot.ReplacementTeacherName))
            return $"→ {CompactPersonName(slot.ReplacementTeacherName)}";
        return "";
    }

    private static Dictionary<int, string> BuildImmediateGapTransitions(
        IReadOnlyList<TimelineColumnDef> dayColumns,
        IReadOnlyList<ConstructorTimelineColumn> dayTimeline,
        IReadOnlyList<ConstructorTimelineColumn> rowTimeline,
        IReadOnlyList<LessonSlot> rowLessons,
        object lookup,
        int teacherId,
        IReadOnlyList<BellPeriod> bells,
        LessonSlot? priorShiftLesson)
    {
        var result = new Dictionary<int, string>();
        var day = rowLessons.FirstOrDefault()?.DayOfWeek ?? 1;
        var lessonColumnIndices = new HashSet<int>();

        for (var index = 0; index < dayColumns.Count; index++)
        {
            var def = dayColumns[index];
            if (def.ColumnKind is not DispatcherMonitorCell.KindLesson and not DispatcherMonitorCell.KindDynamicPause)
                continue;
            if (!RowUsesColumn(rowTimeline, def.Column))
                continue;

            var slots = ResolveSlots(lookup, teacherId, day, def, dayTimeline, isClassRow: false, isRoomRow: false);
            if (slots is not { Count: > 0 })
                continue;

            foreach (var slot in slots)
                lessonColumnIndices.Add(index);
        }

        var ordered = rowLessons
            .Where(l => l.TeacherId == teacherId && !l.IsCancelled && !SubjectScheduleRules.IsDynamicPause(l.SubjectName))
            .OrderBy(GetChronologicalSortKey)
            .ThenBy(l => l.LessonNumber)
            .ToList();

        var lessonColumnBySlotId = new Dictionary<int, int>();
        for (var index = 0; index < dayColumns.Count; index++)
        {
            if (!lessonColumnIndices.Contains(index))
                continue;

            var def = dayColumns[index];
            var slots = ResolveSlots(lookup, teacherId, day, def, dayTimeline, isClassRow: false, isRoomRow: false);
            if (slots is null)
                continue;
            foreach (var slot in slots)
                lessonColumnBySlotId[slot.SlotId] = index;
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            var next = ordered[i];
            if (!lessonColumnBySlotId.TryGetValue(next.SlotId, out var lessonColumnIndex))
                continue;

            var prev = i > 0 ? ordered[i - 1] : priorShiftLesson;
            var transition = FormatBuildingTransitionForCell(prev, next);
            if (string.IsNullOrWhiteSpace(transition))
                continue;

            var gapIndices = FindImmediatePrecedingGapColumnIndices(
                dayColumns, rowTimeline, lessonColumnIndex, lessonColumnIndices);
            foreach (var gapIndex in gapIndices)
                result[gapIndex] = transition;
        }

        return result;
    }

    /// <summary>Перемена перед уроком; если перед ней есть окно(а) — и на них тоже.</summary>
    private static List<int> FindImmediatePrecedingGapColumnIndices(
        IReadOnlyList<TimelineColumnDef> dayColumns,
        IReadOnlyList<ConstructorTimelineColumn> rowTimeline,
        int lessonColumnIndex,
        HashSet<int> lessonColumnIndices)
    {
        var allGaps = new List<int>();
        for (var i = lessonColumnIndex - 1; i >= 0; i--)
        {
            var def = dayColumns[i];
            if (!RowUsesColumn(rowTimeline, def.Column))
                continue;

            if (lessonColumnIndices.Contains(i))
                break;

            allGaps.Add(i);
        }

        if (allGaps.Count == 0)
            return [];

        allGaps.Reverse();

        var trailing = new List<int> { allGaps[^1] };
        for (var j = allGaps.Count - 2; j >= 0; j--)
        {
            trailing.Insert(0, allGaps[j]);
            if (dayColumns[allGaps[j]].ColumnKind == DispatcherMonitorCell.KindBreak)
                break;
        }

        return trailing;
    }

    private static string BuildBuildingTransition(LessonSlot? from, LessonSlot? to)
    {
        if (from is null || to is null)
            return "";

        if (string.IsNullOrWhiteSpace(from.BuildingName) || string.IsNullOrWhiteSpace(to.BuildingName))
            return "";

        return from.BuildingName.Equals(to.BuildingName, StringComparison.OrdinalIgnoreCase)
            ? ""
            : $"{BuildingTransitionIcon}{from.BuildingName} → {to.BuildingName}";
    }

    /// <summary>Две строки для ячейки монитора: здание откуда, затем стрелка и куда.</summary>
    private static string FormatBuildingTransitionForCell(LessonSlot? from, LessonSlot? to)
    {
        if (from is null || to is null)
            return "";

        if (string.IsNullOrWhiteSpace(from.BuildingName) || string.IsNullOrWhiteSpace(to.BuildingName))
            return "";

        if (from.BuildingName.Equals(to.BuildingName, StringComparison.OrdinalIgnoreCase))
            return "";

        return $"{BuildingTransitionIcon}{from.BuildingName.Trim()}\n→ {to.BuildingName.Trim()}";
    }

    private static Dictionary<int, LessonSlot> BuildTeacherChronologicalTails(IReadOnlyList<LessonSlot> lessons) =>
        lessons
            .Where(l => !l.IsCancelled && l.TeacherId > 0)
            .GroupBy(l => l.TeacherId)
            .ToDictionary(g => g.Key, g => g.OrderBy(GetChronologicalSortKey).Last());

    private static string BuildInterShiftSummaryHint(
        IReadOnlyList<LessonSlot> priorLessons,
        IReadOnlyList<LessonSlot> shiftLessons)
    {
        if (priorLessons.Count == 0 || shiftLessons.Count == 0)
            return "";

        var tails = BuildTeacherChronologicalTails(priorLessons);
        var parts = shiftLessons
            .Where(l => !l.IsCancelled && l.TeacherId > 0)
            .GroupBy(l => l.TeacherId)
            .Select(g => g.OrderBy(GetChronologicalSortKey).First())
            .OrderBy(l => l.TeacherName, StringComparer.OrdinalIgnoreCase)
            .Select(first =>
            {
                if (!tails.TryGetValue(first.TeacherId, out var last))
                    return "";
                var transition = BuildBuildingTransition(last, first);
                return string.IsNullOrWhiteSpace(transition)
                    ? ""
                    : $"{CompactPersonName(first.TeacherName)}: {transition}";
            })
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return parts.Count == 0
            ? ""
            : $"Межсменные переходы: {string.Join(" · ", parts)}";
    }

    private static bool IsChronologicallyBefore(LessonSlot earlier, LessonSlot later)
    {
        var earlierKey = GetChronologicalSortKey(earlier);
        var laterKey = GetChronologicalSortKey(later);
        if (earlierKey != laterKey)
            return earlierKey < laterKey;

        return earlier.LessonNumber < later.LessonNumber;
    }

    private static int GetChronologicalSortKey(LessonSlot slot) =>
        TryParseTime(slot.StartTime, out var start)
            ? start
            : int.MaxValue / 2 + slot.LessonNumber;

    private static string BuildToolTip(
        TimelineColumnDef def,
        string primary,
        string secondary,
        string statusLine,
        IReadOnlyList<LessonSlot> slots)
    {
        var lines = new List<string> { def.Title, def.TimeDisplay };
        foreach (var slot in slots)
        {
            if (!string.IsNullOrWhiteSpace(slot.TimeDisplay))
                lines.Add(slot.TimeDisplay);
        }

        lines.Add(primary.Replace("\n", " / "));
        if (!string.IsNullOrWhiteSpace(secondary))
            lines.Add(secondary.Replace("\n", " / "));
        if (!string.IsNullOrWhiteSpace(statusLine))
            lines.Add(statusLine.Replace("\n", " / "));
        return string.Join('\n', lines.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string ResolveStatus(IReadOnlyList<LessonSlot> slots)
    {
        if (slots.Any(l => l.IsCancelled))
            return "Cancelled";
        if (slots.Any(l => l.NeedsReplacement))
            return "Pending";
        if (slots.Any(l => l.HasAssignedReplacement))
            return "Replacement";
        return "Normal";
    }

    private static string ResolveBuildingColor(string buildingName, IReadOnlyDictionary<string, string> colors) =>
        BuildingColors.ResolveHex(buildingName, colors);
}
