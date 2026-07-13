using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Сетка «классы в столбцах, уроки в строках» для конструктора.</summary>
public static class ConstructorTransposedGridBuilder
{
    public static List<ConstructorDayGridSection> BuildSections(
        IEnumerable<SchoolClass> classes,
        IReadOnlyList<LessonSlot> daySlots,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyDictionary<int, Room> roomsById,
        int maxLessons = ScheduleGridBuilder.DefaultMaxLessons,
        BellTemplateAssignmentSnapshot? assignment = null)
    {
        assignment ??= BellTemplateAssignmentSnapshot.Fallback;
        var classList = classes.ToList();
        var slotGroups = daySlots
            .GroupBy(s => (s.ClassId, s.LessonNumber))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubgroupIndex).ToList());

        var sections = new List<ConstructorDayGridSection>();
        var shifts = classList.Select(c => c.Shift).Distinct().OrderBy(s => s).ToList();
        var showShiftHeaders = shifts.Count > 1 || shifts.Any(s => s > 1);

        foreach (var shift in shifts)
        {
            if (showShiftHeaders)
            {
                sections.Add(new ConstructorDayGridSection
                {
                    Title = $"{shift} смена",
                    IsShiftHeader = true,
                    ShiftNumber = shift,
                    SectionKey = $"shift-{shift}"
                });
            }

            var shiftClasses = classList.Where(c => c.Shift == shift).ToList();
            foreach (var track in BellScheduleTrackGrouper.GroupTracks(shiftClasses, assignment))
                AppendTrackSection(sections, track, slotGroups, bells, roomsById, maxLessons, showShiftHeaders);
        }

        return sections;
    }

    private static void AppendTrackSection(
        List<ConstructorDayGridSection> sections,
        BellScheduleTrackGrouper.BellScheduleTrack track,
        Dictionary<(int ClassId, int LessonNumber), List<LessonSlot>> slotGroups,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyDictionary<int, Room> roomsById,
        int maxLessons,
        bool showShiftHeaders)
    {
        var templatePeriods = BellScheduleResolver.FilterByTemplate(bells, track.TemplateName);
        var usePrimary = BellScheduleTrackGrouper.UsePrimaryTimeline(templatePeriods, track);
        var profileGrade = BellScheduleTrackGrouper.ResolveProfileGrade(track);
        var timeline = usePrimary
            ? BellScheduleResolver.BuildPrimaryTimeline(
                templatePeriods, profileGrade, track.Shift, includeBreaks: true, track.TemplateName)
            : BellScheduleResolver.BuildStandardLessonTimeline(
                templatePeriods, profileGrade, track.Shift, maxLessons, includeBreaks: true);

        var classColumns = BuildClassColumns(track.Classes, roomsById);
        var lessonRows = BuildLessonRows(
            classColumns, timeline, slotGroups, templatePeriods, track.TemplateName,
            roomsById, usePrimary, profileGrade, track.Shift, maxLessons, track.Classes);

        sections.Add(new ConstructorDayGridSection
        {
            Title = BellScheduleTrackGrouper.BuildTitle(track),
            SubTitle = showShiftHeaders ? $"{track.Shift} смена" : "",
            IsTransposed = true,
            IsFirstGradeSection = track.Classes.All(c => c.Grade == 1),
            IsCustomBellTrack = track.IsCustom,
            ShiftNumber = track.Shift,
            SectionKey = TrackSectionKey(track),
            ClassColumns = classColumns,
            BuildingBands = BuildBuildingBands(classColumns),
            LessonRows = lessonRows
        });
    }

    private static string TrackSectionKey(BellScheduleTrackGrouper.BellScheduleTrack track) =>
        $"shift-{track.Shift}-track-{track.TemplateName.GetHashCode(StringComparison.OrdinalIgnoreCase)}-{(track.IsCustom ? "custom" : "default")}";

    private static List<ConstructorClassColumn> BuildClassColumns(
        IEnumerable<SchoolClass> classes,
        IReadOnlyDictionary<int, Room> roomsById) =>
        classes
            .OrderBy(c => ResolveBuildingName(c, roomsById), StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Grade)
            .ThenBy(c => c.Letter, StringComparer.OrdinalIgnoreCase)
            .Select(c =>
            {
                var (buildingName, buildingColor) = ResolveBuilding(c, roomsById);
                return new ConstructorClassColumn
                {
                    ClassId = c.Id,
                    ClassName = c.DisplayName,
                    ClassGrade = c.Grade,
                    ClassShift = c.Shift,
                    BuildingName = buildingName,
                    BuildingColorHex = buildingColor,
                    DefaultRoomDisplay = c.DefaultRoomDisplay
                };
            })
            .ToList();

    private static List<ConstructorBuildingBand> BuildBuildingBands(IReadOnlyList<ConstructorClassColumn> columns)
    {
        var bands = new List<ConstructorBuildingBand>();
        if (columns.Count == 0)
            return bands;

        var currentName = columns[0].BuildingName;
        var start = 0;
        for (var i = 1; i <= columns.Count; i++)
        {
            if (i < columns.Count && columns[i].BuildingName == currentName)
                continue;

            bands.Add(new ConstructorBuildingBand
            {
                BuildingName = currentName,
                BuildingColorHex = columns[start].BuildingColorHex,
                SpanCount = i - start
            });
            if (i < columns.Count)
            {
                currentName = columns[i].BuildingName;
                start = i;
            }
        }

        return bands;
    }

    private static List<ConstructorLessonRow> BuildLessonRows(
        IReadOnlyList<ConstructorClassColumn> classColumns,
        IReadOnlyList<ConstructorTimelineColumn> timeline,
        Dictionary<(int ClassId, int LessonNumber), List<LessonSlot>> slotGroups,
        IReadOnlyList<BellPeriod> templatePeriods,
        string templateName,
        IReadOnlyDictionary<int, Room> roomsById,
        bool usePrimary,
        int profileGrade,
        int shift,
        int maxLessons,
        IReadOnlyList<SchoolClass> trackClasses)
    {
        if (!usePrimary)
        {
            var rows = new List<ConstructorLessonRow>(maxLessons);
            for (var lesson = 1; lesson <= maxLessons; lesson++)
            {
                var row = new ConstructorLessonRow
                {
                    RowTitle = $"Урок {lesson}",
                    BellTimeDisplay = BellScheduleResolver.GetLessonBellTime(
                        templatePeriods, profileGrade, shift, lesson, templateName)
                };
                foreach (var col in classColumns)
                {
                    var cls = trackClasses.First(c => c.Id == col.ClassId);
                    row.Cells.Add(ScheduleGridBuilder.CreateLessonCellForClass(
                        cls, lesson, isDynamicPauseColumn: false, slotGroups, roomsById));
                }

                rows.Add(row);
            }

            return rows;
        }

        var timelineRows = new List<ConstructorLessonRow>(timeline.Count);
        foreach (var column in timeline)
        {
            var row = CreateLessonRow(column, templatePeriods, profileGrade, shift, templateName);
            foreach (var col in classColumns)
            {
                var cls = trackClasses.First(c => c.Id == col.ClassId);
                row.Cells.Add(ScheduleGridBuilder.CreateTimelineCellForClass(
                    cls, column, timeline, slotGroups, roomsById, templateName));
            }

            timelineRows.Add(row);
        }

        return timelineRows;
    }

    private static ConstructorLessonRow CreateLessonRow(
        ConstructorTimelineColumn column,
        IReadOnlyList<BellPeriod> templatePeriods,
        int profileGrade,
        int shift,
        string templateName)
    {
        var period = column.IsDynamicPause
            ? BellScheduleResolver.FindDynamicPauseAfterLesson(templatePeriods, profileGrade, shift, column.AfterLessonNumber)
            : column.IsBreak
                ? BellScheduleResolver.GetBreaksForGrade(templatePeriods, profileGrade, shift)
                    .FirstOrDefault(b => b.LessonNumber == column.AfterLessonNumber)
                : BellScheduleResolver.FindLessonPeriod(
                    templatePeriods, profileGrade, shift, column.LessonNumber, templateName);

        var time = BellScheduleResolver.FormatPeriodTime(period);
        if (string.IsNullOrWhiteSpace(time))
            time = column.BellTimeDisplay;

        return new ConstructorLessonRow
        {
            Column = column,
            RowTitle = column.Title,
            BellTimeDisplay = time
        };
    }

    private static string ResolveBuildingName(SchoolClass cls, IReadOnlyDictionary<int, Room> roomsById) =>
        ResolveBuilding(cls, roomsById).Name;

    private static (string Name, string Color) ResolveBuilding(
        SchoolClass cls,
        IReadOnlyDictionary<int, Room> roomsById)
    {
        var resolved = SchoolClassBuildingResolver.Resolve(cls, roomsById);
        return (resolved.Name, resolved.ColorHex);
    }
}
