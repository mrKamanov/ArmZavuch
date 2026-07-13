using ArmZavuch.Data;
using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Schedule;
using ArmZavuch.Services.Staff;

namespace ArmZavuch.Services.Validation;

/// <summary>
/// Проверка недельного шаблона по СанПiN и методике Menobr.
/// Режим Advisory — мягкие алерты при ручной сборке; Strict — те же правила с уровнем Error.
/// </summary>
public sealed class ScheduleComplianceChecker
{
    private readonly WeekTemplateRepository _templates;
    private readonly SchoolClassRepository _classes;
    private readonly SubjectRepository _subjects;
    private readonly CurriculumRepository _curriculum;
    private readonly BellRepository _bells;
    private readonly BellTemplateAssignmentService _bellAssignment;
    private readonly ScheduleConflictDetector _conflicts;
    private readonly BuildingTransitionChecker _transitions;
    private readonly TeacherUnavailabilityRepository _unavailability;
    private readonly TeacherBuildingDayRepository _buildingDays;
    private readonly TeacherRepository _teachers;
    private readonly RoomRepository _rooms;

    public ScheduleComplianceChecker(
        WeekTemplateRepository templates,
        SchoolClassRepository classes,
        SubjectRepository subjects,
        CurriculumRepository curriculum,
        BellRepository bells,
        BellTemplateAssignmentService bellAssignment,
        ScheduleConflictDetector conflicts,
        BuildingTransitionChecker transitions,
        TeacherUnavailabilityRepository unavailability,
        TeacherBuildingDayRepository buildingDays,
        TeacherRepository teachers,
        RoomRepository rooms)
    {
        _templates = templates;
        _classes = classes;
        _subjects = subjects;
        _curriculum = curriculum;
        _bells = bells;
        _bellAssignment = bellAssignment;
        _conflicts = conflicts;
        _transitions = transitions;
        _unavailability = unavailability;
        _buildingDays = buildingDays;
        _teachers = teachers;
        _rooms = rooms;
    }

    private Dictionary<int, SchoolClass> _checkClassMap = new();
    private BellTemplateAssignmentSnapshot _checkAssignment = BellTemplateAssignmentSnapshot.Fallback;
    private IReadOnlyList<BellPeriod> _checkBells = [];

    public Task<List<ComplianceIssue>> CheckTemplateAsync(int templateId) =>
        CheckTemplateAsync(templateId, ScheduleRuleMode.Advisory);

    public async Task<List<ComplianceIssue>> CheckTemplateAsync(int templateId, ScheduleRuleMode mode)
    {
        var issues = new List<ComplianceIssue>();
        var slots = await _templates.GetAllSlotsForTemplateAsync(templateId);
        if (slots.Count == 0)
            return issues;

        var classes = await _classes.GetAllAsync();
        var classMap = classes.ToDictionary(c => c.Id);
        var subjects = await _subjects.GetAllAsync();
        var subjectMap = subjects.ToDictionary(s => s.Id);
        var curriculumDifficulty = (await _curriculum.GetAllAsync())
            .ToDictionary(c => (c.ClassId, c.SubjectId), c => c.SubjectDifficultyScore);

        var bells = await _bells.GetAllPeriodsAsync();
        var assignment = _bellAssignment.CreateSnapshot(classes);
        _checkClassMap = classMap;
        _checkAssignment = assignment;
        _checkBells = bells;

        var teacherMap = (await _teachers.GetAllAsync()).ToDictionary(t => t.Id);
        var buildingDayMap = (await _buildingDays.GetAllAsync())
            .GroupBy(b => b.TeacherId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(x => x.DayOfWeek, x => x.BuildingName, comparer: EqualityComparer<int>.Default));

        CheckMaxLessonsPerDay(slots, classMap, issues);
        CheckGaps(slots, classMap, issues);
        CheckConsecutiveSameSubject(slots, classMap, subjectMap, curriculumDifficulty, issues, mode);
        CheckHardSubjectsConsecutive(slots, classMap, subjectMap, curriculumDifficulty, issues);
        CheckDailyDifficulty(slots, classMap, subjectMap, curriculumDifficulty, issues);
        CheckSubjectOverloadPerDay(slots, classMap, issues);
        CheckDynamicPauses(slots, classMap, issues);
        CheckSecondShift(slots, classMap, issues);
        await CheckTeacherUnavailabilityAsync(slots, issues);
        var roomsById = (await _rooms.GetAllAsync()).ToDictionary(r => r.Id);
        CheckTeacherAndRoomConflicts(slots, bells, roomsById, issues);
        CheckTeacherDailyLoad(slots, issues);
        CheckTeacherWindows(slots, teacherMap, issues, mode);
        CheckTeacherBuildingDays(slots, buildingDayMap, issues, mode);
        CheckMondayFridayLoad(slots, classMap, subjectMap, curriculumDifficulty, issues);
        CheckImportantTalksMondayFirst(slots, classMap, subjectMap, issues);
        CheckPhysicalEducationNotFirst(slots, classMap, subjectMap, issues);
        await CheckBuildingTransitionsAsync(slots, bells, issues);

        return issues
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.ClassName)
            .ThenBy(i => i.DayName)
            .ToList();
    }

    private void CheckMaxLessonsPerDay(
        List<LessonSlot> slots, Dictionary<int, SchoolClass> classMap,
        List<ComplianceIssue> issues)
    {
        foreach (var group in slots.GroupBy(s => (s.ClassId, s.DayOfWeek)))
        {
            if (!classMap.TryGetValue(group.Key.ClassId, out var cls))
                continue;
            var count = SanPiNCountableLessons(TimelineRegularLessons(group, cls))
                .Select(s => LogicalLessonNumber(s))
                .Distinct()
                .Count();
            var max = SanPiNRules.MaxLessonsPerDay(cls.Grade);
            if (count > max)
            {
                issues.Add(ComplianceIssueBuilder.Grid(
                    ComplianceSeverity.Warning,
                    "SANPIN_MAX_DAY",
                    $"{count} уроков при норме не более {max} для {cls.Grade} кл. (дин. пауза и «Разговоры о важном» не считаются)",
                    cls.DisplayName,
                    cls.Id,
                    group.Key.DayOfWeek));
            }
        }
    }

    private void CheckGaps(
        List<LessonSlot> slots, Dictionary<int, SchoolClass> classMap,
        List<ComplianceIssue> issues)
    {
        foreach (var group in slots.GroupBy(s => (s.ClassId, s.ClassName, s.DayOfWeek)))
        {
            if (!classMap.TryGetValue(group.Key.ClassId, out var cls))
                continue;

            var lessons = TimelineRegularLessons(group, cls)
                .Select(s => LogicalLessonNumber(s))
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            if (lessons.Count < 2)
                continue;
            for (var i = 1; i < lessons.Count; i++)
            {
                if (lessons[i] - lessons[i - 1] > 1)
                {
                    issues.Add(ComplianceIssueBuilder.Grid(
                        ComplianceSeverity.Warning,
                        "GAP_LESSON",
                        $"«Нулевой» урок между {lessons[i - 1]} и {lessons[i]} (окно у класса)",
                        group.Key.ClassName,
                        group.Key.ClassId,
                        group.Key.DayOfWeek,
                        lessonNumber: lessons[i]));
                }
            }
        }
    }

    private void CheckConsecutiveSameSubject(
        List<LessonSlot> slots, Dictionary<int, SchoolClass> classMap,
        Dictionary<int, Subject> subjectMap,
        Dictionary<(int ClassId, int SubjectId), double> curriculumDifficulty,
        List<ComplianceIssue> issues, ScheduleRuleMode mode)
    {
        foreach (var group in slots.GroupBy(s => (s.ClassId, s.DayOfWeek)))
        {
            if (!classMap.TryGetValue(group.Key.ClassId, out var cls))
                continue;

            var byLesson = RegularLessons(group)
                .GroupBy(s => LogicalLessonNumber(s))
                .OrderBy(g => g.Key)
                .ToList();

            var violationReported = false;
            for (var i = 1; i < byLesson.Count && !violationReported; i++)
            {
                if (byLesson[i].Key != byLesson[i - 1].Key + 1)
                    continue;

                foreach (var prev in byLesson[i - 1])
                {
                    foreach (var cur in byLesson[i])
                    {
                        if (!SchedulePedagogyRules.SameSubjectName(prev.SubjectName, cur.SubjectName))
                            continue;

                        var score = ResolveDifficulty(prev, cls, subjectMap, curriculumDifficulty);
                        if (SchedulePedagogyRules.AllowsConsecutiveSameSubject(cls.Grade, prev.SubjectName, score))
                            continue;

                        var severity = cls.Grade <= 4
                            ? SchedulePedagogyRules.NooPairViolationSeverity(mode)
                            : SchedulePedagogyRules.PairViolationSeverity(mode);

                        issues.Add(ComplianceIssueBuilder.Grid(
                            severity,
                            cls.Grade <= 4 ? "NOO_SAME_SUBJECT" : "PAIR_SAME_SUBJECT",
                            cls.Grade <= 4
                                ? $"Подряд два урока «{prev.SubjectName}» (НОО: не рекомендуется; исключения — физкультура, труд)"
                                : SchedulePedagogyRules.FormatPairViolationMessage(cls.Grade, prev.SubjectName, score),
                            cls.DisplayName,
                            cls.Id,
                            group.Key.DayOfWeek,
                            lessonNumber: byLesson[i].Key));
                        violationReported = true;
                        break;
                    }

                    if (violationReported)
                        break;
                }
            }
        }
    }

    private void CheckHardSubjectsConsecutive(
        List<LessonSlot> slots, Dictionary<int, SchoolClass> classMap,
        Dictionary<int, Subject> subjectMap,
        Dictionary<(int ClassId, int SubjectId), double> curriculumDifficulty,
        List<ComplianceIssue> issues)
    {
        foreach (var group in slots.GroupBy(s => (s.ClassId, s.DayOfWeek)))
        {
            if (!classMap.TryGetValue(group.Key.ClassId, out var cls) || cls.Grade < 5)
                continue;

            var ordered = RegularLessons(group)
                .Select(s => (Slot: s, Lesson: LogicalLessonNumber(s)))
                .OrderBy(x => x.Lesson)
                .ToList();
            var streak = 1;
            for (var i = 1; i < ordered.Count; i++)
            {
                var prevHard = ResolveDifficulty(ordered[i - 1].Slot, cls, subjectMap, curriculumDifficulty) >= SanPiNRules.HardSubjectThreshold;
                var curHard = ResolveDifficulty(ordered[i].Slot, cls, subjectMap, curriculumDifficulty) >= SanPiNRules.HardSubjectThreshold;
                if (ordered[i].Lesson == ordered[i - 1].Lesson + 1 && prevHard && curHard)
                    streak++;
                else
                    streak = 1;

                if (streak >= 2)
                {
                    issues.Add(ComplianceIssueBuilder.Grid(
                        ComplianceSeverity.Warning,
                        "HARD_CONSECUTIVE",
                        "Два и более сложных предмета подряд (рекомендуется разбавлять)",
                        cls.DisplayName,
                        cls.Id,
                        group.Key.DayOfWeek,
                        lessonNumber: ordered[i].Lesson));
                    break;
                }
            }
        }
    }

    private static void CheckDailyDifficulty(
        List<LessonSlot> slots, Dictionary<int, SchoolClass> classMap,
        Dictionary<int, Subject> subjectMap,
        Dictionary<(int ClassId, int SubjectId), double> curriculumDifficulty,
        List<ComplianceIssue> issues)
    {
        foreach (var group in slots.GroupBy(s => (s.ClassId, s.DayOfWeek)))
        {
            if (!classMap.TryGetValue(group.Key.ClassId, out var cls))
                continue;
            var sum = SchedulePedagogyRules.SumDailyDifficulty(
                RegularLessons(group), cls, subjectMap,
                (slot, c, map) => ResolveDifficulty(slot, c, map, curriculumDifficulty));
            var max = SanPiNRules.MaxDailyDifficultySum(cls.Grade);
            if (sum > max)
            {
                issues.Add(ComplianceIssueBuilder.Grid(
                    ComplianceSeverity.Warning,
                    "SANPIN_DIFFICULTY",
                    $"Суммарная трудность {sum:0.#} при рекомендуемом пределе {max:0.#} " +
                    "(баллы Сивкова; подгруппы считаются по каждому слоту отдельно)",
                    cls.DisplayName,
                    cls.Id,
                    group.Key.DayOfWeek));
            }
        }
    }

    private static void CheckSubjectOverloadPerDay(
        List<LessonSlot> slots, Dictionary<int, SchoolClass> classMap, List<ComplianceIssue> issues)
    {
        foreach (var group in RegularLessons(slots).GroupBy(s => (s.ClassId, s.DayOfWeek, s.SubjectName)))
        {
            if (!classMap.TryGetValue(group.Key.ClassId, out var cls))
                continue;
            var count = group.Count();
            var max = SanPiNRules.MaxSameSubjectPerDay(cls.Grade);
            if (count > max)
            {
                issues.Add(ComplianceIssueBuilder.Grid(
                    ComplianceSeverity.Info,
                    "SUBJECT_SAME_DAY",
                    $"«{group.Key.SubjectName}» встречается {count} раз при обычном лимите {max}",
                    cls.DisplayName,
                    cls.Id,
                    group.Key.DayOfWeek));
            }
        }
    }

    private void CheckDynamicPauses(
        List<LessonSlot> slots,
        Dictionary<int, SchoolClass> classMap,
        List<ComplianceIssue> issues)
    {
        foreach (var cls in classMap.Values)
        {
            var templateName = _checkAssignment.GetTemplateName(cls);
            var bellsScope = ResolveBellsForClass(cls);
            var requiredPauses = BellScheduleResolver.GetDynamicPausesForGrade(bellsScope, cls.Grade, cls.Shift);
            if (requiredPauses.Count == 0)
                continue;

            var timeline = BellScheduleResolver.BuildPrimaryTimeline(
                bellsScope, cls.Grade, cls.Shift, templateName: templateName);
            var pauseColumns = timeline.Where(c => c.IsDynamicPause).ToList();
            if (pauseColumns.Count == 0)
                continue;

            var classSlots = slots.Where(s => s.ClassId == cls.Id).ToList();
            foreach (var dayGroup in classSlots.GroupBy(s => s.DayOfWeek))
            {
                var daySlots = dayGroup.ToList();
                var regular = RegularLessons(daySlots).ToList();
                if (regular.Count == 0)
                    continue;

                var maxLogical = regular.Max(s => LogicalLessonNumber(s));

                foreach (var pauseCol in pauseColumns)
                {
                    if (maxLogical <= pauseCol.AfterLessonNumber)
                        continue;

                    var storageNumber = ScheduleGridBuilder.ResolveDefaultStorageLessonNumber(pauseCol, timeline);
                    var hasPause = daySlots.Any(s =>
                        SubjectScheduleRules.IsDynamicPause(s.SubjectName)
                        && s.LessonNumber == storageNumber);

                    if (hasPause)
                        continue;

                    issues.Add(ComplianceIssueBuilder.Grid(
                        ComplianceSeverity.Warning,
                        "MISSING_DYNAMIC_PAUSE",
                        $"Нет динамической паузы после {pauseCol.AfterLessonNumber}-го урока (требуется по сетке звонков 1 класса)",
                        cls.DisplayName,
                        cls.Id,
                        dayGroup.Key,
                        lessonNumber: storageNumber));
                }
            }
        }
    }

    private static void CheckSecondShift(
        List<LessonSlot> slots, Dictionary<int, SchoolClass> classMap, List<ComplianceIssue> issues)
    {
        foreach (var cls in classMap.Values.Where(ClassShiftCompliance.ViolatesSecondShiftRule))
        {
            var days = slots.Where(s => s.ClassId == cls.Id).Select(s => s.DayOfWeek).Distinct().OrderBy(d => d).ToList();
            if (days.Count == 0)
            {
                issues.Add(ClassShiftCompliance.CreateComplianceIssue(cls, null));
                continue;
            }

            foreach (var day in days)
                issues.Add(ClassShiftCompliance.CreateComplianceIssue(cls, day));
        }
    }

    private async Task CheckTeacherUnavailabilityAsync(List<LessonSlot> slots, List<ComplianceIssue> issues)
    {
        if (slots.Count == 0)
            return;

        var allBlocks = await _unavailability.GetAllAsync();
        var byTeacher = allBlocks
            .GroupBy(b => b.TeacherId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TeacherUnavailability>)g.ToList());

        foreach (var slot in slots)
        {
            if (SubjectScheduleRules.IsDynamicPause(slot.SubjectName))
                continue;

            if (!byTeacher.TryGetValue(slot.TeacherId, out var blocks))
                continue;

            foreach (var message in TeacherUnavailabilityCompliance.GetTemplateWarnings(
                blocks, slot.DayOfWeek, slot.LessonNumber, slot.TeacherName))
            {
                issues.Add(ComplianceIssueBuilder.Grid(
                    ComplianceSeverity.Warning,
                    "TEACHER_UNAVAIL",
                    message,
                    slot.ClassName,
                    slot.ClassId,
                    slot.DayOfWeek,
                    lessonNumber: slot.LessonNumber,
                    teacherId: slot.TeacherId));
            }
        }
    }

    private void CheckTeacherAndRoomConflicts(
        List<LessonSlot> slots,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyDictionary<int, Room> roomsById,
        List<ComplianceIssue> issues)
    {
        if (slots.Count < 2)
            return;

        foreach (var conflict in _conflicts.Detect(slots, bells, roomsById))
        {
            issues.Add(ComplianceIssueBuilder.Grid(
                conflict.IsBlocking ? ComplianceSeverity.Error : ComplianceSeverity.Warning,
                conflict.Kind,
                conflict.Message,
                conflict.ClassName,
                conflict.ClassId,
                conflict.DayOfWeek,
                lessonNumber: conflict.LessonNumber,
                teacherId: conflict.TeacherId > 0 ? conflict.TeacherId : null));
        }
    }

    private void CheckTeacherWindows(
        List<LessonSlot> slots,
        Dictionary<int, Teacher> teacherMap,
        List<ComplianceIssue> issues,
        ScheduleRuleMode mode)
    {
        foreach (var group in slots.GroupBy(s => (s.TeacherId, s.DayOfWeek)))
        {
            if (group.Key.TeacherId <= 0)
                continue;

            var lessons = RegularLessons(group)
                .Select(s => LogicalLessonNumber(s))
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            if (lessons.Count < 2)
                continue;

            var maxLoad = teacherMap.TryGetValue(group.Key.TeacherId, out var teacher)
                ? teacher.MaxLoadHours
                : 18;
            var teacherName = group.First().TeacherName;

            foreach (var windowIssue in SchedulePedagogyRules.EvaluateTeacherWindows(lessons, maxLoad, mode))
            {
                issues.Add(ComplianceIssueBuilder.Grid(
                    windowIssue.Severity,
                    windowIssue.Code,
                    $"{teacherName}: {windowIssue.Message}",
                    dayOfWeek: group.Key.DayOfWeek,
                    teacherId: group.Key.TeacherId));
            }
        }
    }

    private static void CheckTeacherBuildingDays(
        List<LessonSlot> slots,
        Dictionary<int, Dictionary<int, string>> buildingDayMap,
        List<ComplianceIssue> issues,
        ScheduleRuleMode mode)
    {
        if (buildingDayMap.Count == 0)
            return;

        var severity = SchedulePedagogyRules.BuildingDaySeverity(mode);
        foreach (var slot in RegularLessons(slots))
        {
            if (slot.TeacherId <= 0
                || string.IsNullOrWhiteSpace(slot.BuildingName)
                || !buildingDayMap.TryGetValue(slot.TeacherId, out var byDay)
                || !byDay.TryGetValue(slot.DayOfWeek, out var expectedBuilding))
                continue;

            if (expectedBuilding.Equals(slot.BuildingName, StringComparison.OrdinalIgnoreCase))
                continue;

            issues.Add(ComplianceIssueBuilder.Grid(
                severity,
                "TEACHER_BUILDING_DAY",
                $"{slot.TeacherName}: в {SanPiNRules.DayName(slot.DayOfWeek)} указано здание «{expectedBuilding}», " +
                $"урок в «{slot.BuildingName.Trim()}»",
                slot.ClassName,
                slot.ClassId,
                slot.DayOfWeek,
                lessonNumber: slot.LessonNumber,
                teacherId: slot.TeacherId));
        }
    }

    private void CheckTeacherDailyLoad(
        List<LessonSlot> slots, List<ComplianceIssue> issues)
    {
        foreach (var group in slots.GroupBy(s => (s.TeacherId, s.DayOfWeek)))
        {
            var count = RegularLessons(group)
                .Select(s => LogicalLessonNumber(s))
                .Distinct()
                .Count();
            var teacher = group.First().TeacherName;
            if (count >= SanPiNRules.MaxTeacherLessonsPerDay)
            {
                issues.Add(ComplianceIssueBuilder.Grid(
                    ComplianceSeverity.Warning,
                    "TEACHER_OVERLOAD",
                    $"{teacher}: {count} уроков при лимите СанПиН {SanPiNRules.MaxTeacherLessonsPerDay}",
                    dayOfWeek: group.Key.DayOfWeek,
                    teacherId: group.Key.TeacherId));
            }
            else if (count >= SanPiNRules.MaxTeacherLessonsPerDayWarning)
            {
                issues.Add(ComplianceIssueBuilder.Grid(
                    ComplianceSeverity.Info,
                    "TEACHER_LOAD_INFO",
                    $"{teacher}: {count} уроков — близко к лимиту СанПиН ({SanPiNRules.MaxTeacherLessonsPerDay} в день, дин. пауза не считается)",
                    dayOfWeek: group.Key.DayOfWeek,
                    teacherId: group.Key.TeacherId));
            }
        }
    }

    private async Task CheckBuildingTransitionsAsync(
        List<LessonSlot> slots, IReadOnlyList<BellPeriod> bells, List<ComplianceIssue> issues)
    {
        if (slots.Count < 2)
            return;

        var routeMap = await _transitions.LoadRouteMapAsync();
        foreach (var day in slots.GroupBy(s => s.DayOfWeek))
        {
            var warnings = _transitions.CheckTeacherDay(day.ToList(), bells, routeMap);
            foreach (var warning in warnings)
            {
                var later = warning.LaterLesson;
                issues.Add(ComplianceIssueBuilder.Grid(
                    warning.IsTimeCritical ? ComplianceSeverity.Warning : ComplianceSeverity.Info,
                    "BUILDING_TRANSITION",
                    warning.Message,
                    later.ClassName,
                    later.ClassId,
                    later.DayOfWeek,
                    lessonNumber: later.LessonNumber,
                    teacherId: warning.TeacherId));
            }
        }
    }

    private void CheckImportantTalksMondayFirst(
        List<LessonSlot> slots,
        Dictionary<int, SchoolClass> classMap,
        Dictionary<int, Subject> subjectMap,
        List<ComplianceIssue> issues)
    {
        foreach (var cls in classMap.Values)
        {
            var rovSlots = slots
                .Where(s => s.ClassId == cls.Id && IsImportantTalksSlot(s, subjectMap))
                .ToList();
            if (rovSlots.Count == 0)
                continue;

            foreach (var slot in rovSlots)
            {
                var logical = LogicalLessonNumber(slot);
                if (slot.DayOfWeek == SubjectScheduleRules.ImportantTalksPreferredDayOfWeek
                    && logical == SubjectScheduleRules.ImportantTalksPreferredLessonNumber)
                    continue;

                var dayName = SanPiNRules.DayName(slot.DayOfWeek);
                var message = slot.DayOfWeek != SubjectScheduleRules.ImportantTalksPreferredDayOfWeek
                    ? "«Разговоры о важном» обычно ставят в понедельник, 1-й урок (внеурочка в сетке; шаблон Минпросвещения 2025)"
                    : $"«Разговоры о важном» — {logical}-й урок; принято 1-й урок понедельника";

                issues.Add(ComplianceIssueBuilder.Grid(
                    ComplianceSeverity.Warning,
                    "SOFT_ROV_MONDAY_FIRST",
                    message,
                    cls.DisplayName,
                    cls.Id,
                    slot.DayOfWeek,
                    lessonNumber: LogicalLessonNumber(slot)));
            }
        }
    }

    private void CheckPhysicalEducationNotFirst(
        List<LessonSlot> slots,
        Dictionary<int, SchoolClass> classMap,
        Dictionary<int, Subject> subjectMap,
        List<ComplianceIssue> issues)
    {
        foreach (var cls in classMap.Values)
        {
            var peSlots = slots
                .Where(s => s.ClassId == cls.Id && IsPhysicalEducationSlot(s, subjectMap))
                .ToList();
            foreach (var slot in peSlots)
            {
                if (LogicalLessonNumber(slot) != 1)
                    continue;

                issues.Add(ComplianceIssueBuilder.Grid(
                    ComplianceSeverity.Warning,
                    "SOFT_PE_FIRST",
                    "Физкультура на 1-м уроке — не рекомендуется (Menobr)",
                    cls.DisplayName,
                    cls.Id,
                    slot.DayOfWeek,
                    lessonNumber: 1));
            }
        }
    }

    private static bool IsPhysicalEducationSlot(LessonSlot slot, Dictionary<int, Subject> subjectMap)
    {
        if (SubjectScheduleRules.IsPhysicalEducationSubject(slot.SubjectName))
            return true;

        return slot.SubjectId > 0
               && subjectMap.TryGetValue(slot.SubjectId, out var subject)
               && SubjectScheduleRules.IsPhysicalEducationSubject(subject.Name);
    }

    private static bool IsImportantTalksSlot(LessonSlot slot, Dictionary<int, Subject> subjectMap)
    {
        if (SubjectScheduleRules.IsImportantTalksSubject(slot.SubjectName))
            return true;

        return slot.SubjectId > 0
               && subjectMap.TryGetValue(slot.SubjectId, out var subject)
               && SubjectScheduleRules.IsImportantTalksSubject(subject.Name);
    }

    private static void CheckMondayFridayLoad(
        List<LessonSlot> slots, Dictionary<int, SchoolClass> classMap,
        Dictionary<int, Subject> subjectMap,
        Dictionary<(int ClassId, int SubjectId), double> curriculumDifficulty,
        List<ComplianceIssue> issues)
    {
        foreach (var cls in classMap.Values.Where(c => c.Grade >= 5))
        {
            var classSlots = slots.Where(s => s.ClassId == cls.Id).ToList();
            var mon = AvgDifficulty(classSlots, 1, cls, subjectMap, curriculumDifficulty);
            var wed = AvgDifficulty(classSlots, 3, cls, subjectMap, curriculumDifficulty);
            var fri = AvgDifficulty(classSlots, 5, cls, subjectMap, curriculumDifficulty);
            if (mon > wed + 0.3)
            {
                issues.Add(ComplianceIssueBuilder.Grid(
                    ComplianceSeverity.Info,
                    "HEAVY_MONDAY",
                    "Нагруженнее среды — рассмотрите облегчение (Menobr)",
                    cls.DisplayName,
                    cls.Id,
                    dayOfWeek: 1));
            }
            if (fri > wed + 0.3)
            {
                issues.Add(ComplianceIssueBuilder.Grid(
                    ComplianceSeverity.Info,
                    "HEAVY_FRIDAY",
                    "Нагруженнее среды — рассмотрите облегчение (Menobr)",
                    cls.DisplayName,
                    cls.Id,
                    dayOfWeek: 5));
            }
        }
    }

    private static double AvgDifficulty(
        List<LessonSlot> slots, int day, SchoolClass cls, Dictionary<int, Subject> subjectMap,
        Dictionary<(int ClassId, int SubjectId), double> curriculumDifficulty)
    {
        var daySlots = RegularLessons(slots.Where(s => s.DayOfWeek == day)).ToList();
        return daySlots.Count == 0
            ? 0
            : daySlots.Average(s => ResolveDifficulty(s, cls, subjectMap, curriculumDifficulty));
    }

    private static double ResolveDifficulty(
        LessonSlot slot, SchoolClass cls, Dictionary<int, Subject> subjectMap,
        Dictionary<(int ClassId, int SubjectId), double> curriculumDifficulty)
    {
        if (slot.SubjectId <= 0 || !subjectMap.TryGetValue(slot.SubjectId, out var subject))
            return 1.0;
        var stored = curriculumDifficulty.TryGetValue((cls.Id, slot.SubjectId), out var score)
            ? score
            : subject.DifficultyScore;
        return OfficialSubjectDifficultyReference.ResolveForClass(
            subject.Name, cls.Grade, stored);
    }

    private int LogicalLessonNumber(LessonSlot slot)
    {
        _checkClassMap.TryGetValue(slot.ClassId, out var cls);
        var templateName = cls is not null ? _checkAssignment.GetTemplateName(cls) : null;
        var bells = ResolveBellsForSlot(slot, templateName);
        return ScheduleGridBuilder.ResolveLogicalLessonNumber(bells, slot, templateName);
    }

    private IReadOnlyList<BellPeriod> ResolveBellsForClass(SchoolClass cls)
    {
        if (cls.Grade != ScheduleGridBuilder.FirstGradeTimelineGrade)
            return _checkBells;

        var templateName = _checkAssignment.GetTemplateName(cls);
        var filtered = BellScheduleResolver.FilterByTemplate(_checkBells, templateName);
        return filtered.Count > 0 ? filtered : _checkBells;
    }

    private IReadOnlyList<BellPeriod> ResolveBellsForSlot(LessonSlot slot, string? templateName)
    {
        if (slot.ClassGrade != ScheduleGridBuilder.FirstGradeTimelineGrade)
            return _checkBells;

        var filtered = BellScheduleResolver.FilterByTemplate(_checkBells, templateName);
        return filtered.Count > 0 ? filtered : _checkBells;
    }

    private IEnumerable<LessonSlot> TimelineRegularLessons(IEnumerable<LessonSlot> slots, SchoolClass cls)
    {
        var regular = RegularLessons(slots);
        if (cls.Grade != ScheduleGridBuilder.FirstGradeTimelineGrade)
            return regular;

        var storages = GetTimelineLessonStorages(cls);
        return regular.Where(s => storages.Contains(s.LessonNumber));
    }

    private HashSet<int> GetTimelineLessonStorages(SchoolClass cls)
    {
        var templateName = _checkAssignment.GetTemplateName(cls);
        var bells = ResolveBellsForClass(cls);
        var timeline = BellScheduleResolver.BuildPrimaryTimeline(
            bells, cls.Grade, cls.Shift, templateName: templateName);
        return ScheduleGridBuilder.CollectTimelineLessonStorageNumbers(timeline);
    }

    private static IEnumerable<LessonSlot> RegularLessons(IEnumerable<LessonSlot> slots) =>
        slots.Where(IsRegularLesson);

    private static IEnumerable<LessonSlot> SanPiNCountableLessons(IEnumerable<LessonSlot> slots) =>
        slots.Where(s => !SubjectScheduleRules.IsImportantTalksSubject(s.SubjectName));

    private static bool IsRegularLesson(LessonSlot slot) =>
        !SubjectScheduleRules.IsDynamicPause(slot.SubjectName);
}
