using System.Text.RegularExpressions;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Rooms;

/// <summary>
/// Физическая идентичность кабинета: варианты «с/з», «спортзал», «Спортивный зал» — один спортзал.
/// </summary>
public static class RoomPhysicalIdentity
{
    public const string SportHallDisplayName = "Спортзал";

    public static bool IsSportsHall(string? roomNumber, string? roomKind = null)
    {
        if (!string.IsNullOrWhiteSpace(roomKind)
            && roomKind.Contains("спорт", StringComparison.OrdinalIgnoreCase))
            return true;

        return IsSportsHallName(roomNumber);
    }

    public static bool IsSportsHallName(string? roomNumber)
    {
        if (string.IsNullOrWhiteSpace(roomNumber))
            return false;

        var key = Normalize(roomNumber);
        if (key is "сз")
            return true;

        return key.Contains("спорт", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Два урока используют один и тот же кабинет (по id или спортзал в том же здании).</summary>
    public static bool SharePhysicalSpace(LessonSlot a, LessonSlot b)
    {
        if (a.RoomId > 0 && b.RoomId > 0)
            return a.RoomId == b.RoomId;

        if (IsSportsHallName(a.RoomNumber) && IsSportsHallName(b.RoomNumber))
            return SameBuilding(a.BuildingName, b.BuildingName);

        return false;
    }

    private static bool SameBuilding(string? buildingA, string? buildingB)
    {
        var a = buildingA?.Trim() ?? "";
        var b = buildingB?.Trim() ?? "";
        if (a.Length == 0 || b.Length == 0)
            return false;
        return a.Equals(b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Одновременная работа в спортзале — допустима, но требует мягкого предупреждения.</summary>
    public static bool IsSportsHallSharedUse(LessonSlot a, LessonSlot b) =>
        SharePhysicalSpace(a, b) && IsSportsHallName(a.RoomNumber) && IsSportsHallName(b.RoomNumber);

    /// <summary>
    /// Накладка по кабинету — только предупреждение, если у спортзала включены параллельные группы.
    /// </summary>
    public static bool TreatsOverlapAsSharedUse(
        LessonSlot a,
        LessonSlot b,
        IReadOnlyDictionary<int, Room>? roomsById)
    {
        if (!SharePhysicalSpace(a, b))
            return false;

        if (a.RoomId > 0 && b.RoomId > 0 && a.RoomId == b.RoomId)
            return RoomAllowsParallelGroups(a.RoomId, roomsById);

        if (IsSportsHallName(a.RoomNumber) && IsSportsHallName(b.RoomNumber) && SameBuilding(a.BuildingName, b.BuildingName))
        {
            var aAllows = a.RoomId <= 0 || RoomAllowsParallelGroups(a.RoomId, roomsById);
            var bAllows = b.RoomId <= 0 || RoomAllowsParallelGroups(b.RoomId, roomsById);
            return aAllows && bAllows;
        }

        return false;
    }

    private static bool RoomAllowsParallelGroups(int roomId, IReadOnlyDictionary<int, Room>? roomsById)
    {
        if (roomsById is null || !roomsById.TryGetValue(roomId, out var room))
            return false;
        return room.AllowsParallelGroups && IsSportsHall(room.Number, room.RoomKind);
    }

    public static string FormatRoomLabel(LessonSlot slot) =>
        IsSportsHallName(slot.RoomNumber) ? SportHallDisplayName : slot.RoomNumber;

    private static string Normalize(string name) =>
        Regex.Replace(name.Trim(), @"[\s\./\\\-–—]+", "", RegexOptions.CultureInvariant)
            .ToLowerInvariant();
}
