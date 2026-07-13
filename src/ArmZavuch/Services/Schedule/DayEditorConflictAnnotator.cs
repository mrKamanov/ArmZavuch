using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Подсветка конфликтов в сетке конструктора дня: накладки учителя/кабинета и «окна» у класса.
/// Вход: секции сетки и уроки дня. Выход: HasConflict/ConflictHint на ячейках.
/// </summary>
public static class DayEditorConflictAnnotator
{
    public static void AnnotateSections(
        IEnumerable<DispatcherMonitorSection> sections,
        IReadOnlyList<LessonSlot> dayLessons,
        ScheduleConflictDetector detector,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyDictionary<int, Room> roomsById,
        BellTemplateAssignmentSnapshot assignment)
    {
        var active = dayLessons.Where(l => !l.IsCancelled).ToList();
        var conflicts = detector.Detect(active, bells, roomsById, assignment);
        var blockingHints = BuildHintMap(conflicts.Where(c => c.IsBlocking));
        var sharedHints = BuildHintMap(conflicts.Where(c => !c.IsBlocking));
        var blockingByLesson = BuildClassLessonHintMap(conflicts.Where(c => c.IsBlocking), active);
        var sharedByLesson = BuildClassLessonHintMap(conflicts.Where(c => !c.IsBlocking), active);

        foreach (var section in sections)
        {
            if (section.IsShiftHeader)
                continue;

            foreach (var row in section.Rows)
            {
                foreach (var cell in row.Cells)
                    ApplyCellHints(cell, blockingHints, sharedHints, blockingByLesson, sharedByLesson);
            }
        }
    }

    private static void ApplyCellHints(
        DispatcherMonitorCell cell,
        IReadOnlyDictionary<int, HashSet<string>> blockingHints,
        IReadOnlyDictionary<int, HashSet<string>> sharedHints,
        IReadOnlyDictionary<(int ClassId, int LessonNumber), HashSet<string>> blockingByLesson,
        IReadOnlyDictionary<(int ClassId, int LessonNumber), HashSet<string>> sharedByLesson)
    {
        cell.HasConflict = false;
        cell.ConflictHint = "";
        cell.HasRoomSharedWarning = false;
        cell.RoomSharedHint = "";

        if (IsClassGap(cell))
        {
            cell.ConflictHint = BuildGapMessage(cell);
            cell.HasConflict = true;
            return;
        }

        if (cell.Lesson?.SlotId is int slotId && slotId > 0)
        {
            if (blockingHints.TryGetValue(slotId, out var blockMessages))
                cell.ConflictHint = JoinHints(cell.ConflictHint, blockMessages);

            if (sharedHints.TryGetValue(slotId, out var sharedMessages))
                cell.RoomSharedHint = JoinHints(cell.RoomSharedHint, sharedMessages);
        }

        if (cell.Lesson is not null)
        {
            var key = (cell.Lesson.ClassId, cell.Lesson.LessonNumber);
            if (blockingByLesson.TryGetValue(key, out var blockByLesson))
                cell.ConflictHint = JoinHints(cell.ConflictHint, blockByLesson);

            if (sharedByLesson.TryGetValue(key, out var sharedByLessonMessages))
                cell.RoomSharedHint = JoinHints(cell.RoomSharedHint, sharedByLessonMessages);
        }

        cell.HasConflict = !string.IsNullOrWhiteSpace(cell.ConflictHint);
        cell.HasRoomSharedWarning = !cell.HasConflict && !string.IsNullOrWhiteSpace(cell.RoomSharedHint);
    }

    private static bool IsClassGap(DispatcherMonitorCell cell) =>
        cell.ClassId > 0
        && cell.LessonNumber > 0
        && !cell.HasLesson
        && cell.ColumnKind is DispatcherMonitorCell.KindEmpty or DispatcherMonitorCell.KindWindow;

    private static string BuildGapMessage(DispatcherMonitorCell cell)
    {
        var time = string.IsNullOrWhiteSpace(cell.TimeLabel) ? "" : $" · {cell.TimeLabel}";
        return $"Окно: дети без присмотра{time}";
    }

    private static Dictionary<int, HashSet<string>> BuildHintMap(IEnumerable<ScheduleConflict> conflicts)
    {
        var map = new Dictionary<int, HashSet<string>>();
        foreach (var conflict in conflicts)
        {
            foreach (var slotId in conflict.SlotIds.Where(id => id > 0))
            {
                if (!map.TryGetValue(slotId, out var messages))
                {
                    messages = [];
                    map[slotId] = messages;
                }

                messages.Add(conflict.Message);
            }
        }

        return map;
    }

    private static Dictionary<(int ClassId, int LessonNumber), HashSet<string>> BuildClassLessonHintMap(
        IEnumerable<ScheduleConflict> conflicts,
        IReadOnlyList<LessonSlot> lessons)
    {
        var slots = lessons.ToDictionary(l => l.SlotId);
        var map = new Dictionary<(int ClassId, int LessonNumber), HashSet<string>>();
        foreach (var conflict in conflicts)
        {
            foreach (var slotId in conflict.SlotIds.Where(id => id > 0))
            {
                if (!slots.TryGetValue(slotId, out var slot))
                    continue;

                var key = (slot.ClassId, slot.LessonNumber);
                if (!map.TryGetValue(key, out var messages))
                {
                    messages = [];
                    map[key] = messages;
                }

                messages.Add(conflict.Message);
            }
        }

        return map;
    }

    private static string JoinHints(string current, IEnumerable<string> messages)
    {
        var merged = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(current))
            merged.Add(current);

        foreach (var message in messages)
            merged.Add(message);

        return string.Join("\n", merged.OrderBy(m => m, StringComparer.Ordinal));
    }
}
