using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Проверка переходов учителя между зданиями: матрица минут + рекомендация «закладывать урок на переход».
/// Сравнивает соседние по времени уроки педагога; при достаточном зазоре предупреждение не показывается.
/// </summary>
public sealed class BuildingTransitionChecker
{
    /// <summary>Если маршрут не задан — ориентир для сравнения по времени.</summary>
    public const int DefaultTransitionMinutes = BuildingRouteDefaults.Minutes;

    /// <summary>Запас сверх матрицы переходов — больше не считаем «бегом между корпусами».</summary>
    private const int ComfortableTransitionBufferMinutes = 15;

    private readonly BuildingRepository _buildings;
    private readonly ScheduleConflictDetector _conflicts;

    public BuildingTransitionChecker(BuildingRepository buildings, ScheduleConflictDetector conflicts)
    {
        _buildings = buildings;
        _conflicts = conflicts;
    }

    public async Task<IReadOnlyDictionary<(string From, string To), int>> LoadRouteMapAsync()
    {
        var map = new Dictionary<(string From, string To), int>(StringTupleComparer.Instance);
        foreach (var route in await _buildings.GetRoutesAsync())
            map[(route.FromBuildingName, route.ToBuildingName)] = route.Minutes;
        return map;
    }

    public IReadOnlyList<BuildingTransitionWarning> CheckTeacherDay(
        IReadOnlyList<LessonSlot> lessons,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyDictionary<(string From, string To), int> routeMap,
        int? onlyTeacherId = null,
        int? focusClassId = null,
        int? focusLessonNumber = null)
    {
        if (lessons.Count == 0)
            return [];

        var working = lessons
            .Where(l => !l.IsCancelled && l.TeacherId > 0)
            .Select(Clone)
            .ToList();
        _conflicts.EnrichWithBellTimes(working, bells);

        var warnings = new List<BuildingTransitionWarning>();
        foreach (var group in working.GroupBy(l => l.TeacherId))
        {
            if (onlyTeacherId is int tid && group.Key != tid)
                continue;

            var ordered = group
                .OrderBy(l => GetChronologicalSortKey(l))
                .ThenBy(l => l.LessonNumber)
                .ToList();

            for (var i = 0; i < ordered.Count - 1; i++)
            {
                var earlier = ordered[i];
                var later = ordered[i + 1];
                var focus = ResolveMessageFocus(focusClassId, focusLessonNumber, earlier, later);
                if (focusClassId is int classId && focusLessonNumber is int lessonNo)
                {
                    var touchesFocus = focus != TransitionMessageFocus.Neutral;
                    if (!touchesFocus)
                        continue;
                }

                var warning = AnalyzePair(earlier, later, routeMap, focus);
                if (warning is null)
                    continue;

                warnings.Add(warning);
            }
        }

        return warnings;
    }

    public IReadOnlyList<BuildingTransitionWarning> CheckProposedSlot(
        IReadOnlyList<LessonSlot> dayLessons,
        LessonSlot proposed,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyDictionary<(string From, string To), int> routeMap)
    {
        var teacherDay = TeacherDayTimelineBuilder.Build(dayLessons, proposed.TeacherId, proposed);
        return CheckTeacherDay(
            teacherDay, bells, routeMap,
            onlyTeacherId: proposed.TeacherId,
            focusClassId: proposed.ClassId,
            focusLessonNumber: proposed.LessonNumber);
    }

    public IReadOnlyList<BuildingTransitionWarning> CheckTeacherDayWithSubstitutions(
        IReadOnlyList<LessonSlot> dayLessons,
        int teacherId,
        LessonSlot proposed,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyDictionary<(string From, string To), int> routeMap) =>
        CheckTeacherDay(
            TeacherDayTimelineBuilder.Build(dayLessons, teacherId, proposed),
            bells,
            routeMap,
            onlyTeacherId: teacherId);

    private enum TransitionMessageFocus
    {
        Neutral,
        ThisIsEarlier,
        ThisIsLater
    }

    private static TransitionMessageFocus ResolveMessageFocus(
        int? focusClassId,
        int? focusLessonNumber,
        LessonSlot earlier,
        LessonSlot later)
    {
        if (focusClassId is not int classId || focusLessonNumber is not int lessonNo)
            return TransitionMessageFocus.Neutral;

        if (earlier.ClassId == classId && earlier.LessonNumber == lessonNo)
            return TransitionMessageFocus.ThisIsEarlier;
        if (later.ClassId == classId && later.LessonNumber == lessonNo)
            return TransitionMessageFocus.ThisIsLater;

        return TransitionMessageFocus.Neutral;
    }

    private static BuildingTransitionWarning? AnalyzePair(
        LessonSlot earlier,
        LessonSlot later,
        IReadOnlyDictionary<(string From, string To), int> routeMap,
        TransitionMessageFocus focus = TransitionMessageFocus.Neutral)
    {
        if (string.IsNullOrWhiteSpace(earlier.BuildingName) || string.IsNullOrWhiteSpace(later.BuildingName))
            return null;

        if (earlier.BuildingName.Equals(later.BuildingName, StringComparison.OrdinalIgnoreCase))
            return null;

        var routeConfigured = routeMap.ContainsKey((earlier.BuildingName, later.BuildingName));
        var required = routeConfigured
            ? routeMap[(earlier.BuildingName, later.BuildingName)]
            : DefaultTransitionMinutes;

        var available = GetAvailableMinutes(earlier, later);
        var consecutiveNumbers = later.LessonNumber - earlier.LessonNumber == 1;
        var earlierTime = FormatSlotTime(earlier);
        var laterTime = FormatSlotTime(later);

        if (available is int gap)
        {
            if (gap < 0)
            {
                var criticalMessage = BuildInsufficientTimeMessage(
                    earlier.TeacherName,
                    earlierTime,
                    earlier.BuildingName,
                    laterTime,
                    later.BuildingName,
                    required,
                    gap,
                    routeConfigured,
                    consecutiveNumbers,
                    focus);

                return new BuildingTransitionWarning
                {
                    TeacherId = earlier.TeacherId,
                    TeacherName = earlier.TeacherName,
                    EarlierLesson = earlier,
                    LaterLesson = later,
                    RequiredMinutes = required,
                    AvailableMinutes = gap,
                    RouteConfigured = routeConfigured,
                    IsTimeCritical = true,
                    IsConsecutiveDifferentBuildings = consecutiveNumbers,
                    Message = criticalMessage
                };
            }

            if (gap >= required)
            {
                var comfortableGap = gap >= required + ComfortableTransitionBufferMinutes;
                if (!consecutiveNumbers || comfortableGap)
                    return null;

                return new BuildingTransitionWarning
                {
                    TeacherId = earlier.TeacherId,
                    TeacherName = earlier.TeacherName,
                    EarlierLesson = earlier,
                    LaterLesson = later,
                    RequiredMinutes = required,
                    AvailableMinutes = gap,
                    RouteConfigured = routeConfigured,
                    IsShuttleReminder = true,
                    IsConsecutiveDifferentBuildings = consecutiveNumbers,
                    Message = BuildShuttleReminderMessage(
                        earlier.TeacherName,
                        earlierTime,
                        earlier.BuildingName,
                        laterTime,
                        later.BuildingName,
                        required,
                        gap,
                        routeConfigured,
                        focus)
                };
            }

            var message = BuildInsufficientTimeMessage(
                earlier.TeacherName,
                earlierTime,
                earlier.BuildingName,
                laterTime,
                later.BuildingName,
                required,
                gap,
                routeConfigured,
                consecutiveNumbers,
                focus);

            return new BuildingTransitionWarning
            {
                TeacherId = earlier.TeacherId,
                TeacherName = earlier.TeacherName,
                EarlierLesson = earlier,
                LaterLesson = later,
                RequiredMinutes = required,
                AvailableMinutes = gap,
                RouteConfigured = routeConfigured,
                IsTimeCritical = true,
                IsConsecutiveDifferentBuildings = consecutiveNumbers,
                Message = message
            };
        }

        if (!consecutiveNumbers)
            return null;

        var unknownTimeMessage = BuildConsecutiveUnknownTimeMessage(
            earlier.TeacherName,
            earlierTime,
            earlier.BuildingName,
            laterTime,
            later.BuildingName,
            focus);

        return new BuildingTransitionWarning
        {
            TeacherId = earlier.TeacherId,
            TeacherName = earlier.TeacherName,
            EarlierLesson = earlier,
            LaterLesson = later,
            RequiredMinutes = required,
            AvailableMinutes = null,
            RouteConfigured = routeConfigured,
            IsTimeCritical = false,
            IsConsecutiveDifferentBuildings = true,
            Message = unknownTimeMessage
        };
    }

    private static string BuildInsufficientTimeMessage(
        string teacherName,
        string earlierTime,
        string earlierBuilding,
        string laterTime,
        string laterBuilding,
        int requiredMinutes,
        int availableMinutes,
        bool routeConfigured,
        bool consecutiveNumbers,
        TransitionMessageFocus focus)
    {
        var routeHint = routeConfigured
            ? $"по матрице переходов — {requiredMinutes} мин"
            : $"маршрут не задан, ориентир — {requiredMinutes} мин";

        if (consecutiveNumbers)
        {
            return
                $"{teacherName}: {DescribeEarlierSlot(focus)} ({earlierTime}) в «{earlierBuilding}», " +
                $"{DescribeLaterSlot(focus)} ({laterTime}) в «{laterBuilding}». " +
                $"Подряд идут соседние уроки в разных зданиях — обычно закладывают один урок на переход. " +
                $"Между звонками {availableMinutes} мин ({routeHint}).";
        }

        return focus switch
        {
            TransitionMessageFocus.ThisIsEarlier =>
                $"{teacherName}: этот урок ({earlierTime}) в «{earlierBuilding}» — " +
                $"следующий ({laterTime}) в «{laterBuilding}». " +
                $"На переход нужно {requiredMinutes} мин ({routeHint}), между уроками {availableMinutes} мин — может не успеть.",
            TransitionMessageFocus.ThisIsLater =>
                $"{teacherName}: предыдущий урок ({earlierTime}) в «{earlierBuilding}» — " +
                $"этот ({laterTime}) в «{laterBuilding}». " +
                $"На переход нужно {requiredMinutes} мин ({routeHint}), между уроками {availableMinutes} мин — может не успеть.",
            _ =>
                $"{teacherName}: после урока ({earlierTime}) в «{earlierBuilding}» — урок ({laterTime}) в «{laterBuilding}». " +
                $"На переход нужно {requiredMinutes} мин ({routeHint}), между уроками {availableMinutes} мин — может не успеть."
        };
    }

    private static string BuildConsecutiveUnknownTimeMessage(
        string teacherName,
        string earlierTime,
        string earlierBuilding,
        string laterTime,
        string laterBuilding,
        TransitionMessageFocus focus) =>
        focus switch
        {
            TransitionMessageFocus.ThisIsEarlier =>
                $"{teacherName}: этот урок {FormatLessonRef(earlierTime, earlierBuilding)} и следующий по номеру " +
                $"({laterTime}) в «{laterBuilding}». Обычно между зданиями закладывают один урок на переход — " +
                $"вы действительно хотите так поставить?",
            TransitionMessageFocus.ThisIsLater =>
                $"{teacherName}: предыдущий урок {FormatLessonRef(earlierTime, earlierBuilding)} и этот " +
                $"({laterTime}) в «{laterBuilding}». Обычно между зданиями закладывают один урок на переход — " +
                $"вы действительно хотите так поставить?",
            _ =>
                $"{teacherName}: урок {FormatLessonRef(earlierTime, earlierBuilding)} и следующий по номеру " +
                $"({laterTime}) в «{laterBuilding}». Обычно между зданиями закладывают один урок на переход — " +
                $"вы действительно хотите так поставить?"
        };

    private static string BuildShuttleReminderMessage(
        string teacherName,
        string earlierTime,
        string earlierBuilding,
        string laterTime,
        string laterBuilding,
        int requiredMinutes,
        int availableMinutes,
        bool routeConfigured,
        TransitionMessageFocus focus)
    {
        var routeHint = routeConfigured
            ? $"переход ~{requiredMinutes} мин"
            : $"ориентир ~{requiredMinutes} мин";

        var legs = focus switch
        {
            TransitionMessageFocus.ThisIsEarlier =>
                $"этот урок {earlierTime} в «{earlierBuilding}», затем {laterTime} в «{laterBuilding}»",
            TransitionMessageFocus.ThisIsLater =>
                $"{earlierTime} в «{earlierBuilding}», затем этот урок {laterTime} в «{laterBuilding}»",
            _ => $"после {earlierTime}, затем {laterTime}"
        };

        return
            $"{teacherName}: «{earlierBuilding}» → «{laterBuilding}» ({legs}). " +
            $"Между уроками {availableMinutes} мин ({routeHint}) — успевает, но придётся бегать между корпусами.";
    }

    private static string DescribeEarlierSlot(TransitionMessageFocus focus) =>
        focus switch
        {
            TransitionMessageFocus.ThisIsEarlier => "этот урок",
            TransitionMessageFocus.ThisIsLater => "предыдущий урок",
            _ => "урок"
        };

    private static string DescribeLaterSlot(TransitionMessageFocus focus) =>
        focus switch
        {
            TransitionMessageFocus.ThisIsLater => "этот урок",
            _ => "следующий"
        };

    private static string FormatLessonRef(string time, string building) =>
        string.IsNullOrWhiteSpace(time) ? $"в «{building}»" : $"{time} в «{building}»";

    private static int GetChronologicalSortKey(LessonSlot slot) =>
        TryParseTime(slot.StartTime, out var start)
            ? (int)start.TotalMinutes
            : int.MaxValue / 2 + slot.LessonNumber;

    private static int? GetAvailableMinutes(LessonSlot earlier, LessonSlot later)
    {
        if (!TryParseTime(earlier.EndTime, out var end) || !TryParseTime(later.StartTime, out var start))
            return null;
        return (int)Math.Floor((start - end).TotalMinutes);
    }

    private static bool TryParseTime(string value, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (TimeSpan.TryParse(value, out time))
            return true;
        if (TimeOnly.TryParse(value, out var t))
        {
            time = t.ToTimeSpan();
            return true;
        }

        return false;
    }

    private static string FormatSlotTime(LessonSlot slot) =>
        string.IsNullOrWhiteSpace(slot.StartTime)
            ? $"урок {slot.LessonNumber}"
            : $"{slot.StartTime}–{slot.EndTime}";

    private static LessonSlot Clone(LessonSlot s) => new()
    {
        SlotId = s.SlotId,
        Date = s.Date,
        LessonNumber = s.LessonNumber,
        StartTime = s.StartTime,
        EndTime = s.EndTime,
        ClassId = s.ClassId,
        ClassName = s.ClassName,
        ClassGrade = s.ClassGrade,
        ClassShift = s.ClassShift,
        SubjectId = s.SubjectId,
        SubjectName = s.SubjectName,
        TeacherId = s.TeacherId,
        TeacherName = s.TeacherName,
        RoomId = s.RoomId,
        RoomNumber = s.RoomNumber,
        BuildingName = s.BuildingName,
        SubgroupIndex = s.SubgroupIndex,
        DayOfWeek = s.DayOfWeek,
        IsCancelled = s.IsCancelled
    };

    private sealed class StringTupleComparer : IEqualityComparer<(string From, string To)>
    {
        public static StringTupleComparer Instance { get; } = new();

        public bool Equals((string From, string To) x, (string From, string To) y) =>
            x.From.Equals(y.From, StringComparison.OrdinalIgnoreCase)
            && x.To.Equals(y.To, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string From, string To) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.From),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.To));
    }
}
