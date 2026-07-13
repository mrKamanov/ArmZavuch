using ArmZavuch.Models;
using ArmZavuch.Services.Rooms;

namespace ArmZavuch.Services.Schedule;

/// <summary>Поиск накладок учителя и кабинета с учётом реального времени звонков.</summary>
public sealed class ScheduleConflictDetector
{
    public void EnrichWithBellTimes(
        IReadOnlyList<LessonSlot> slots,
        IReadOnlyList<BellPeriod> bells,
        BellTemplateAssignmentSnapshot? assignment = null)
    {
        foreach (var slot in slots)
        {
            if (!string.IsNullOrWhiteSpace(slot.StartTime) && !string.IsNullOrWhiteSpace(slot.EndTime))
                continue;

            var templateName = assignment?.GetTemplateName(slot.ClassId, slot.ClassGrade, slot.ClassShift);
            var period = BellScheduleResolver.FindLessonPeriod(
                bells, slot.ClassGrade, slot.ClassShift, slot.LessonNumber, templateName);
            BellScheduleResolver.ApplyLessonTimes(slot, period);
        }
    }

    public List<ScheduleConflict> Detect(
        IReadOnlyList<LessonSlot> slots,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyDictionary<int, Room>? roomsById = null,
        BellTemplateAssignmentSnapshot? assignment = null)
    {
        if (slots.Count < 2)
            return [];

        var working = slots.Where(s => !s.IsCancelled).Select(Clone).ToList();
        if (working.Count < 2)
            return [];

        EnrichWithBellTimes(working, bells, assignment);

        var conflicts = new List<ScheduleConflict>();
        for (var i = 0; i < working.Count; i++)
        {
            for (var j = i + 1; j < working.Count; j++)
            {
                var a = working[i];
                var b = working[j];
                if (a.DayOfWeek != b.DayOfWeek)
                    continue;
                if (!BellScheduleResolver.TimesOverlap(a, b))
                    continue;

                if (a.TeacherId > 0 && a.TeacherId == b.TeacherId)
                    conflicts.Add(CreateTeacherConflict(a, b));

                if (RoomPhysicalIdentity.SharePhysicalSpace(a, b))
                {
                    if (RoomPhysicalIdentity.TreatsOverlapAsSharedUse(a, b, roomsById))
                        conflicts.Add(CreateRoomSharedWarning(a, b));
                    else
                        conflicts.Add(CreateRoomConflict(a, b));
                }
            }
        }

        return conflicts;
    }

    /// <summary>Конфликты, в которые попадает предлагаемый урок (редактирование / drag-and-drop).</summary>
    public List<ScheduleConflict> DetectForProposed(
        IReadOnlyList<LessonSlot> daySlots,
        LessonSlot proposed,
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyDictionary<int, Room>? roomsById = null,
        BellTemplateAssignmentSnapshot? assignment = null)
    {
        var merged = daySlots
            .Where(s => !(s.ClassId == proposed.ClassId
                          && s.LessonNumber == proposed.LessonNumber
                          && s.SubgroupIndex == proposed.SubgroupIndex))
            .Select(Clone)
            .ToList();

        var copy = Clone(proposed);
        copy.SlotId = proposed.SlotId > 0 ? proposed.SlotId : -1;
        copy.DayOfWeek = proposed.DayOfWeek;
        merged.Add(copy);

        return Detect(merged, bells, roomsById, assignment)
            .Where(c => c.SlotIdA == copy.SlotId || c.SlotIdB == copy.SlotId)
            .ToList();
    }

    private static ScheduleConflict CreateTeacherConflict(LessonSlot a, LessonSlot b) =>
        new()
        {
            Kind = ScheduleConflict.TeacherDoubleBook,
            DayOfWeek = a.DayOfWeek,
            ClassId = a.ClassId,
            LessonNumber = a.LessonNumber,
            TeacherId = a.TeacherId,
            SlotIdA = a.SlotId,
            SlotIdB = b.SlotId,
            ClassName = a.ClassName,
            Message =
                $"{a.TeacherName}: одновременно {a.ClassName} ({FormatSlotTime(a)}) и {b.ClassName} ({FormatSlotTime(b)})"
        };

    private static ScheduleConflict CreateRoomConflict(LessonSlot a, LessonSlot b) =>
        new()
        {
            Kind = ScheduleConflict.RoomDoubleBook,
            DayOfWeek = a.DayOfWeek,
            ClassId = a.ClassId,
            LessonNumber = a.LessonNumber,
            TeacherId = a.TeacherId,
            SlotIdA = a.SlotId,
            SlotIdB = b.SlotId,
            ClassName = a.ClassName,
            Message =
                $"Каб. {a.RoomNumber}: одновременно {a.ClassName} ({FormatSlotTime(a)}) и {b.ClassName} ({FormatSlotTime(b)})"
        };

    private static ScheduleConflict CreateRoomSharedWarning(LessonSlot a, LessonSlot b)
    {
        var hallLabel = string.IsNullOrWhiteSpace(a.BuildingName)
            ? RoomPhysicalIdentity.SportHallDisplayName
            : $"{RoomPhysicalIdentity.SportHallDisplayName} · {a.BuildingName.Trim()}";
        return new()
        {
            Kind = ScheduleConflict.RoomSharedUse,
            IsBlocking = false,
            DayOfWeek = a.DayOfWeek,
            ClassId = a.ClassId,
            LessonNumber = a.LessonNumber,
            TeacherId = a.TeacherId,
            SlotIdA = a.SlotId,
            SlotIdB = b.SlotId,
            ClassName = a.ClassName,
            Message =
                $"{hallLabel}: одновременно {a.ClassName} ({FormatSlotTime(a)}) " +
                $"и {b.ClassName} ({FormatSlotTime(b)}) — зал может вмещать несколько групп, проверьте нагрузку"
        };
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
        BellTemplateName = s.BellTemplateName,
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
}
