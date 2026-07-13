using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Здание класса для сортировки в конструкторе: явная привязка, иначе кабинет по умолчанию.
/// </summary>
public static class SchoolClassBuildingResolver
{
    private const string FallbackColor = "#94A3B8";

    public static (string Name, string ColorHex) Resolve(
        SchoolClass cls,
        IReadOnlyDictionary<int, Room>? roomsById = null)
    {
        if (!string.IsNullOrWhiteSpace(cls.BuildingName))
        {
            return (cls.BuildingName,
                string.IsNullOrWhiteSpace(cls.BuildingColorHex) ? FallbackColor : cls.BuildingColorHex);
        }

        if (cls.DefaultRoomId is int roomId
            && roomsById is not null
            && roomsById.TryGetValue(roomId, out var room))
            return (room.BuildingName, room.BuildingColorHex);

        return ("Без здания", FallbackColor);
    }
}
