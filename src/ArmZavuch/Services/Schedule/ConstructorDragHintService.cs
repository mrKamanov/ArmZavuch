using ArmZavuch.Models;
using ArmZavuch.Services.Staff;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Подсказки при drag-and-drop в конструкторе: оценка ячейки до отпускания карточки.
/// Вход: слоты шаблона, звонки, маршруты. Выход: уровень и текст для подсветки.
/// </summary>
public sealed class ConstructorDragHintService
{
    private readonly ScheduleConflictDetector _conflicts;
    private readonly BuildingTransitionChecker _transitions;

    public ConstructorDragHintService(
        ScheduleConflictDetector conflicts,
        BuildingTransitionChecker transitions)
    {
        _conflicts = conflicts;
        _transitions = transitions;
    }

    public sealed class EvaluationContext
    {
        public required IReadOnlyList<LessonSlot> TemplateSlots { get; init; }
        public required IReadOnlyList<BellPeriod> Bells { get; init; }
        public required IReadOnlyDictionary<int, Room> RoomsById { get; init; }
        public required BellTemplateAssignmentSnapshot BellAssignment { get; init; }
        public required IReadOnlyDictionary<(string From, string To), int> RouteMap { get; init; }
        public required Func<int, bool> ClassHasSubgroups { get; init; }
        public required Func<int, Subject?> ResolvePauseSubjectForClass { get; init; }
        public IReadOnlyList<TeacherUnavailability>? TeacherUnavailability { get; init; }
        public required IReadOnlyDictionary<int, SchoolClass> ClassesById { get; init; }
    }

    public DragHintResult EvaluateTeacherDrop(
        EvaluationContext ctx,
        Teacher teacher,
        GridCell cell,
        int dayOfWeek)
    {
        if (cell.IsBreakColumn)
            return DragHintResult.None;

        if (cell.IsDynamicPauseColumn)
            return EvaluateTeacherDropOnPause(ctx, teacher, cell, dayOfWeek);

        return EvaluateTeacherDropOnLesson(ctx, teacher, cell, dayOfWeek);
    }

    private DragHintResult EvaluateTeacherDropOnPause(
        EvaluationContext ctx,
        Teacher teacher,
        GridCell cell,
        int dayOfWeek)
    {
        var pauseSubject = ctx.ResolvePauseSubjectForClass(cell.ClassId);
        if (pauseSubject is null)
        {
            return new(DragHintLevel.Caution,
                $"{cell.ClassName}: в нагрузке нет предмета «Динамическая пауза» — добавьте в справочниках");
        }

        var hints = new List<DragHintResult>();
        var occupancy = EvaluatePauseOccupancy(cell, teacher);
        if (occupancy is not null)
            hints.Add(occupancy);

        var daySlots = ctx.TemplateSlots.Where(s => s.DayOfWeek == dayOfWeek).ToList();
        var existing = daySlots.FirstOrDefault(s =>
            s.ClassId == cell.ClassId
            && s.LessonNumber == cell.LessonNumber
            && s.SubgroupIndex == 0
            && SubjectScheduleRules.IsDynamicPause(s.SubjectName));

        var proposed = BuildProposedSlot(cell, teacher, dayOfWeek, existing, estimatedRoom: null, ctx,
            subjectOverride: pauseSubject);

        var blocking = _conflicts.DetectForProposed(
                daySlots, proposed, ctx.Bells, ctx.RoomsById, ctx.BellAssignment)
            .Where(c => c.IsBlocking)
            .Select(c => c.Message)
            .Distinct()
            .ToList();
        if (blocking.Count > 0)
            hints.Add(new(DragHintLevel.Blocked, blocking[0]));

        if (ctx.TeacherUnavailability is { Count: > 0 })
        {
            var unavail = TeacherUnavailabilityCompliance
                .GetTemplateWarnings(ctx.TeacherUnavailability, dayOfWeek, cell.LessonNumber, teacher.FullName)
                .FirstOrDefault();
            if (unavail is not null)
                hints.Add(new(DragHintLevel.Caution, unavail));
        }

        var merged = DragHintResult.Merge(hints);
        if (merged.Level >= DragHintLevel.Caution)
            return merged;

        if (DynamicPauseScheduleHelper.TeacherLeadsPauseForClass(teacher, cell.ClassId, pauseSubject))
            return new(DragHintLevel.Recommended, $"Ведёт дин. паузу для {cell.ClassName}");

        return new(DragHintLevel.Recommended, $"Дин. пауза · {cell.ClassName} (кабинет не нужен)");
    }

    private DragHintResult EvaluateTeacherDropOnLesson(
        EvaluationContext ctx,
        Teacher teacher,
        GridCell cell,
        int dayOfWeek)
    {
        var hints = new List<DragHintResult>();

        var occupancy = EvaluateClassOccupancy(cell, teacher, ctx.ClassHasSubgroups(cell.ClassId));
        if (occupancy is not null)
            hints.Add(occupancy);

        var daySlots = ctx.TemplateSlots.Where(s => s.DayOfWeek == dayOfWeek).ToList();
        var existing = daySlots.FirstOrDefault(s =>
            s.ClassId == cell.ClassId
            && s.LessonNumber == cell.LessonNumber
            && s.SubgroupIndex == 0
            && !SubjectScheduleRules.IsDynamicPause(s.SubjectName));

        var estimatedRoom = EstimateRoom(cell.ClassId, teacher, ctx);
        var proposed = BuildProposedSlot(cell, teacher, dayOfWeek, existing, estimatedRoom, ctx);

        var blocking = _conflicts.DetectForProposed(
                daySlots, proposed, ctx.Bells, ctx.RoomsById, ctx.BellAssignment)
            .Where(c => c.IsBlocking)
            .Select(c => c.Message)
            .Distinct()
            .ToList();
        if (blocking.Count > 0)
            hints.Add(new(DragHintLevel.Blocked, blocking[0]));

        if (ctx.TeacherUnavailability is { Count: > 0 })
        {
            var unavail = TeacherUnavailabilityCompliance
                .GetTemplateWarnings(ctx.TeacherUnavailability, dayOfWeek, cell.LessonNumber, teacher.FullName)
                .FirstOrDefault();
            if (unavail is not null)
                hints.Add(new(DragHintLevel.Caution, unavail));
        }

        var transitions = _transitions.CheckProposedSlot(daySlots, proposed, ctx.Bells, ctx.RouteMap);
        if (transitions.Count > 0)
            hints.Add(new(DragHintLevel.Caution, transitions[0].Message));

        var merged = DragHintResult.Merge(hints);
        if (merged.Level >= DragHintLevel.Caution)
            return merged;

        if (TeacherRecommendation.IsRecommendedFor(teacher, cell.ClassId, cell.ClassGrade, cell.ClassName))
            return new(DragHintLevel.Recommended, $"Педагог привязан к классу {cell.ClassName}");

        return merged;
    }

    private static DragHintResult? EvaluatePauseOccupancy(GridCell cell, Teacher teacher)
    {
        var occupied = cell.Parts
            .Where(p => p.TeacherId is int tid && tid > 0)
            .ToList();
        if (occupied.Count == 0)
            return null;

        if (occupied.Any(p => p.TeacherId == teacher.Id))
            return new(DragHintLevel.Recommended, "Педагог уже назначен на дин. паузу");

        return new(DragHintLevel.Blocked, $"{cell.ClassName}: дин. пауза уже занята другим педагогом");
    }

    private static DragHintResult? EvaluateClassOccupancy(GridCell cell, Teacher teacher, bool classHasSubgroups)
    {
        var occupied = cell.Parts
            .Where(p => p.TeacherId is int tid && tid > 0)
            .ToList();
        if (occupied.Count == 0)
            return null;

        if (occupied.Any(p => p.TeacherId == teacher.Id))
            return new(DragHintLevel.Recommended, "Педагог уже назначен в этой ячейке");

        if (occupied.Count >= 2)
            return new(DragHintLevel.Blocked, $"{cell.ClassName}: обе подгруппы заняты");

        if (classHasSubgroups)
        {
            return new(DragHintLevel.Caution,
                $"{cell.ClassName}: педагог уже есть — можно только во 2-ю подгруппу (п/г в нагрузке)");
        }

        return new(DragHintLevel.Blocked,
            $"{cell.ClassName}: уже есть педагог, подгруппы в нагрузке не предусмотрены");
    }

    private static Room? EstimateRoom(int classId, Teacher teacher, EvaluationContext ctx)
    {
        if (ctx.ClassesById.TryGetValue(classId, out var cls) && cls.DefaultRoomId is int roomId
            && ctx.RoomsById.TryGetValue(roomId, out var classRoom))
            return classRoom;

        if (teacher.RoomId is int teacherRoomId && ctx.RoomsById.TryGetValue(teacherRoomId, out var teacherRoom))
            return teacherRoom;

        return null;
    }

    private static LessonSlot BuildProposedSlot(
        GridCell cell,
        Teacher teacher,
        int dayOfWeek,
        LessonSlot? existing,
        Room? estimatedRoom,
        EvaluationContext ctx,
        Subject? subjectOverride = null)
    {
        ctx.ClassesById.TryGetValue(cell.ClassId, out var cls);
        var subjectId = subjectOverride?.Id ?? existing?.SubjectId ?? 0;
        var subjectName = subjectOverride?.Name ?? existing?.SubjectName ?? "";
        var isPause = SubjectScheduleRules.IsDynamicPause(subjectName);

        return new LessonSlot
        {
            SlotId = existing?.SlotId ?? 0,
            DayOfWeek = dayOfWeek,
            LessonNumber = cell.LessonNumber,
            ClassId = cell.ClassId,
            ClassName = cls?.DisplayName ?? cell.ClassName,
            ClassGrade = cls?.Grade ?? cell.ClassGrade,
            ClassShift = cls?.Shift ?? 1,
            SubjectId = subjectId,
            SubjectName = subjectName,
            TeacherId = teacher.Id,
            TeacherName = teacher.FullName,
            RoomId = isPause ? 0 : existing?.RoomId ?? estimatedRoom?.Id ?? 0,
            RoomNumber = isPause ? "" : existing?.RoomNumber ?? estimatedRoom?.Number ?? "",
            BuildingName = isPause ? "" : estimatedRoom?.BuildingName ?? existing?.BuildingName ?? "",
            SubgroupIndex = 0
        };
    }
}
