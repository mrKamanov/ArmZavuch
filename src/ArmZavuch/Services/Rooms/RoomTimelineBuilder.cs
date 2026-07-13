using ArmZavuch.Models;
using ArmZavuch.Services.Schedule;

namespace ArmZavuch.Services.Rooms;

/// <summary>Временная шкала занятости кабинетов на день (Gantt).</summary>
public static class RoomTimelineBuilder
{
    public static RoomTimelineLayout BuildDayLayout(
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyList<LessonSlot> lessons)
    {
        var bounds = CollectDayBounds(bells, lessons);
        var dayStart = RoundDown(bounds.Min, 30);
        var dayEnd = RoundUp(bounds.Max, 30);
        if (dayEnd <= dayStart)
        {
            dayStart = new TimeSpan(8, 0, 0);
            dayEnd = new TimeSpan(15, 0, 0);
        }

        var totalMinutes = (dayEnd - dayStart).TotalMinutes;
        var layout = new RoomTimelineLayout
        {
            DayStart = dayStart,
            DayEnd = dayEnd,
            TotalWidthPixels = Math.Max(640, totalMinutes * RoomTimelineLayout.PixelsPerMinute),
            DayRangeLabel = $"{FormatTime(dayStart)}–{FormatTime(dayEnd)}",
            Ticks = BuildAxisTicks(dayStart, dayEnd),
            HourBands = BuildHourBands(dayStart, dayEnd)
        };
        return layout;
    }

    public static List<RoomBuildingGroup> BuildBuildingGroups(
        IReadOnlyList<Room> rooms,
        IReadOnlyList<LessonSlot> lessons,
        RoomTimelineLayout layout,
        IReadOnlyList<BellPeriod> bells,
        string? buildingFilter)
    {
        var filtered = string.IsNullOrWhiteSpace(buildingFilter)
            ? rooms
            : rooms.Where(r => r.BuildingName.Equals(buildingFilter, StringComparison.OrdinalIgnoreCase));

        var groups = filtered
            .GroupBy(r => r.BuildingName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        var result = new List<RoomBuildingGroup>();
        foreach (var group in groups)
        {
            var buildingGroup = new RoomBuildingGroup
            {
                BuildingName = group.Key,
                BuildingColorHex = group.First().BuildingColorHex
            };

            foreach (var room in group.OrderBy(r => r.Number, StringComparer.OrdinalIgnoreCase))
                buildingGroup.Rooms.Add(BuildRoomRow(room, lessons, layout, bells));

            if (buildingGroup.Rooms.Count > 0)
                result.Add(buildingGroup);
        }

        return result;
    }

    public static RoomOccupancyRow BuildRoomRow(
        Room room,
        IReadOnlyList<LessonSlot> lessons,
        RoomTimelineLayout layout,
        IReadOnlyList<BellPeriod> bells)
    {
        var row = new RoomOccupancyRow
        {
            RoomId = room.Id,
            RoomNumber = room.Number,
            BuildingName = room.BuildingName,
            BuildingColorHex = room.BuildingColorHex,
            Capacity = room.Capacity,
            TimelineWidthPixels = layout.TotalWidthPixels
        };

        var roomSlots = lessons
            .Where(l => l.RoomId == room.Id && !l.IsCancelled)
            .OrderBy(l => l.LessonNumber)
            .ToList();

        foreach (var slot in roomSlots)
        {
            if (!TryGetSlotWindow(slot, bells, out var start, out var end))
                continue;

            var left = layout.ToPixels(start);
            var width = Math.Max(72, (end - start).TotalMinutes * RoomTimelineLayout.PixelsPerMinute - 2);
            var isPause = SubjectScheduleRules.IsDynamicPause(slot.SubjectName);

            row.Blocks.Add(new RoomOccupancyBlock
            {
                Start = start,
                End = end,
                LeftPixels = left,
                WidthPixels = width,
                TimeDisplay = FormatSlotTimeRange(start, end, slot),
                PrimaryLine = slot.ClassName,
                SecondaryLine = isPause ? "Дин. пауза" : slot.SubjectName,
                TeacherLine = isPause ? "" : slot.TeacherName,
                IsDynamicPause = isPause,
                ToolTip = FormatBlockToolTip(room, slot, start, end)
            });
        }

        return row;
    }

    public static (TimeSpan Start, TimeSpan End)? ResolveLessonWindow(
        IReadOnlyList<BellPeriod> bells,
        int lessonNumber,
        RoomSearchGradeBand band,
        int shift = 1)
    {
        var grade = band == RoomSearchGradeBand.Grade1 ? 1 : 5;
        var period = BellScheduleResolver.FindLessonPeriod(bells, grade, shift, lessonNumber);
        return PeriodToWindow(period);
    }

    public static bool TryParseSearchTime(string? text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim()
            .Replace('.', ':')
            .Replace('-', ':')
            .Replace('，', ':');

        if (TryParseTime(trimmed, out time))
            return true;

        if (trimmed.Contains(':'))
            return false;

        if (trimmed.Length is >= 3 and <= 4 && int.TryParse(trimmed, out var digits))
        {
            var hours = digits / 100;
            var minutes = digits % 100;
            if (hours is >= 0 and <= 23 && minutes is >= 0 and <= 59)
            {
                time = new TimeSpan(hours, minutes, 0);
                return true;
            }
        }

        return int.TryParse(trimmed, out var hourOnly)
               && hourOnly is >= 0 and <= 23
               && TryParseTime($"{hourOnly:D2}:00", out time);
    }

    public static bool SlotOccupiesWindow(
        LessonSlot slot,
        TimeSpan windowStart,
        TimeSpan windowEnd,
        IReadOnlyList<BellPeriod> bells) =>
        TryGetSlotWindow(slot, bells, out var start, out var end)
        && start < windowEnd
        && windowStart < end;

    public static bool TryGetSlotWindow(
        LessonSlot slot,
        IReadOnlyList<BellPeriod> bells,
        out TimeSpan start,
        out TimeSpan end)
    {
        start = end = default;
        if (HasTimeRange(slot)
            && TryParseTime(slot.StartTime, out start)
            && TryParseTime(slot.EndTime, out end)
            && end > start)
            return true;

        var period = ScheduleGridBuilder.ResolvePeriodForSlot(bells, slot);
        if (period is null
            || !TryParseTime(period.StartTime, out start)
            || !TryParseTime(period.EndTime, out end)
            || end <= start)
            return false;

        return true;
    }

    public static bool Overlaps(LessonSlot slot, TimeSpan start, TimeSpan end, IReadOnlyList<BellPeriod> bells) =>
        SlotOccupiesWindow(slot, start, end, bells);

    public static bool Overlaps(TimeSpan startA, TimeSpan endA, TimeSpan startB, TimeSpan endB) =>
        startA < endB && startB < endA;

    public static (double Left, double Width) SearchBandPixels(
        RoomTimelineLayout layout,
        TimeSpan searchStart,
        TimeSpan searchEnd)
    {
        var left = layout.ToPixels(searchStart);
        var right = layout.ToPixels(searchEnd);
        return (left, Math.Max(4, right - left));
    }

    private static List<RoomTimeAxisTick> BuildAxisTicks(TimeSpan dayStart, TimeSpan dayEnd)
    {
        var ticks = new List<RoomTimeAxisTick>();
        var cursor = dayStart;
        while (cursor <= dayEnd)
        {
            var isMajor = cursor.Minutes == 0;
            ticks.Add(new RoomTimeAxisTick
            {
                LeftPixels = (cursor - dayStart).TotalMinutes * RoomTimelineLayout.PixelsPerMinute,
                Label = FormatTime(cursor),
                IsMajor = isMajor
            });
            cursor = cursor.Add(TimeSpan.FromMinutes(30));
        }

        return ticks;
    }

    private static List<RoomTimelineHourBand> BuildHourBands(TimeSpan dayStart, TimeSpan dayEnd)
    {
        var bands = new List<RoomTimelineHourBand>();
        var cursor = dayStart;
        var index = 0;

        while (cursor < dayEnd)
        {
            var nextHour = new TimeSpan(cursor.Hours, 0, 0).Add(TimeSpan.FromHours(1));
            if (nextHour <= cursor)
                nextHour = nextHour.Add(TimeSpan.FromHours(1));

            var next = nextHour < dayEnd ? nextHour : dayEnd;
            var left = (cursor - dayStart).TotalMinutes * RoomTimelineLayout.PixelsPerMinute;
            var width = (next - cursor).TotalMinutes * RoomTimelineLayout.PixelsPerMinute;
            bands.Add(new RoomTimelineHourBand
            {
                LeftPixels = left,
                WidthPixels = Math.Max(1, width),
                BackgroundHex = index % 2 == 0 ? "#FFFFFF" : "#EEF2F6"
            });

            cursor = next;
            index++;
        }

        return bands;
    }

    private static (TimeSpan Min, TimeSpan Max) CollectDayBounds(
        IReadOnlyList<BellPeriod> bells,
        IReadOnlyList<LessonSlot> lessons)
    {
        var points = new List<TimeSpan>();
        foreach (var period in bells)
        {
            if (!BellPeriodKinds.IsLesson(period.PeriodKind)
                && period.PeriodKind != BellPeriodKinds.DynamicPause)
                continue;

            if (TryParseTime(period.StartTime, out var start))
                points.Add(start);
            if (TryParseTime(period.EndTime, out var end))
                points.Add(end);
        }

        foreach (var lesson in lessons.Where(l => !l.IsCancelled && HasTimeRange(l)))
        {
            if (TryParseTime(lesson.StartTime, out var start))
                points.Add(start);
            if (TryParseTime(lesson.EndTime, out var end))
                points.Add(end);
        }

        if (points.Count == 0)
            return (new TimeSpan(8, 0, 0), new TimeSpan(15, 0, 0));

        return (points.Min(), points.Max());
    }

    private static TimeSpan RoundDown(TimeSpan value, int minutes)
    {
        var total = (int)value.TotalMinutes;
        var rounded = total / minutes * minutes;
        return TimeSpan.FromMinutes(rounded);
    }

    private static TimeSpan RoundUp(TimeSpan value, int minutes)
    {
        var total = (int)value.TotalMinutes;
        var rounded = (total + minutes - 1) / minutes * minutes;
        return TimeSpan.FromMinutes(rounded);
    }

    private static (TimeSpan Start, TimeSpan End)? PeriodToWindow(BellPeriod? period)
    {
        if (period is null)
            return null;
        if (!TryParseTime(period.StartTime, out var start) || !TryParseTime(period.EndTime, out var end))
            return null;
        if (end <= start)
            return null;
        return (start, end);
    }

    private static bool HasTimeRange(LessonSlot slot) =>
        TryParseTime(slot.StartTime, out _) && TryParseTime(slot.EndTime, out _);

    private static string FormatSlotTime(LessonSlot slot) =>
        string.IsNullOrWhiteSpace(slot.EndTime)
            ? slot.StartTime
            : $"{NormalizeClock(slot.StartTime)}–{NormalizeClock(slot.EndTime)}";

    private static string FormatSlotTimeRange(TimeSpan start, TimeSpan end, LessonSlot slot)
    {
        if (HasTimeRange(slot))
            return FormatSlotTime(slot);
        return $"{FormatTime(start)}–{FormatTime(end)}";
    }

    private static string FormatBlockToolTip(Room room, LessonSlot slot, TimeSpan start, TimeSpan end)
    {
        var kind = SubjectScheduleRules.IsDynamicPause(slot.SubjectName)
            ? "Дин. пауза"
            : slot.SubjectName;
        return $"каб. {room.Number} · {FormatTime(start)}–{FormatTime(end)}\n{slot.ClassName} · {kind}\n{slot.TeacherName}";
    }

    private static string NormalizeClock(string? text)
    {
        if (!TryParseTime(text, out var ts))
            return text ?? "";
        return ts.ToString(@"hh\:mm");
    }

    public static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (TimeSpan.TryParse(text, out time))
            return true;

        if (TimeOnly.TryParse(text, out var timeOnly))
        {
            time = timeOnly.ToTimeSpan();
            return true;
        }

        return false;
    }

    private static string FormatTime(TimeSpan time) =>
        time.ToString(@"hh\:mm");
}
