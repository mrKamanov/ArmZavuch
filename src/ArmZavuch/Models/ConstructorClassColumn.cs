namespace ArmZavuch.Models;

/// <summary>Столбец сетки — один класс в шапке таблицы.</summary>
public sealed class ConstructorClassColumn
{
    public int ClassId { get; init; }
    public string ClassName { get; init; } = "";
    public int ClassGrade { get; init; }
    public int ClassShift { get; init; } = 1;
    public string BuildingName { get; init; } = "";
    public string BuildingColorHex { get; init; } = "#94A3B8";
    public string DefaultRoomDisplay { get; init; } = "";
    public string HeaderLine => string.IsNullOrWhiteSpace(DefaultRoomDisplay)
        ? ClassName
        : $"{ClassName} · {DefaultRoomDisplay}";
}
