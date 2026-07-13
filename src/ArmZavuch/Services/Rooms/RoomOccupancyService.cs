using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Schedule;

namespace ArmZavuch.Services.Rooms;

/// <summary>Шахматка занятости кабинетов и поиск свободных.</summary>
public sealed class RoomOccupancyService
{
    private readonly DayScheduleResolver _schedule;
    private readonly RoomRepository _rooms;
    private readonly BellRepository _bells;

    public RoomOccupancyService(
        DayScheduleResolver schedule,
        RoomRepository rooms,
        BellRepository bells)
    {
        _schedule = schedule;
        _rooms = rooms;
        _bells = bells;
    }

    public async Task<(bool IsSchoolDay, RoomTimelineLayout Layout, List<RoomBuildingGroup> Groups)> GetTimelineAsync(
        DateOnly date, string? buildingFilter = null)
    {
        var bells = await _bells.GetAllPeriodsAsync();
        var (isSchoolDay, lessons) = await _schedule.ResolveAsync(date);
        var active = lessons.Where(l => !l.IsCancelled).ToList();
        var layout = RoomTimelineBuilder.BuildDayLayout(bells, active);
        var rooms = await _rooms.GetAllAsync();
        var groups = RoomTimelineBuilder.BuildBuildingGroups(rooms, active, layout, bells, buildingFilter);
        return (isSchoolDay, layout, groups);
    }

    public async Task<List<Room>> FindFreeRoomsAsync(
        DateOnly date,
        TimeSpan? windowStart,
        TimeSpan? windowEnd,
        int? minCapacity,
        string? buildingName)
    {
        if (windowStart is null || windowEnd is null || windowEnd <= windowStart)
            return [];

        var bells = await _bells.GetAllPeriodsAsync();
        var (_, lessons) = await _schedule.ResolveAsync(date);
        var busyRoomIds = lessons
            .Where(l => !l.IsCancelled && l.RoomId > 0)
            .Where(l => RoomTimelineBuilder.SlotOccupiesWindow(l, windowStart.Value, windowEnd.Value, bells))
            .Select(l => l.RoomId)
            .ToHashSet();

        var query = (await _rooms.GetAllAsync()).Where(r => !busyRoomIds.Contains(r.Id));
        if (minCapacity is int cap)
            query = query.Where(r => r.Capacity >= cap);
        if (!string.IsNullOrWhiteSpace(buildingName))
            query = query.Where(r => r.BuildingName.Equals(buildingName, StringComparison.OrdinalIgnoreCase));

        return query
            .OrderBy(r => r.BuildingName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Number, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<(TimeSpan Start, TimeSpan End)?> ResolveLessonWindowAsync(
        int lessonNumber,
        RoomSearchGradeBand band)
    {
        var bells = await GetBellPeriodsAsync();
        return RoomTimelineBuilder.ResolveLessonWindow(bells, lessonNumber, band);
    }

    public async Task<IReadOnlyList<BellPeriod>> GetBellPeriodsAsync() =>
        await _bells.GetAllPeriodsAsync();
}
