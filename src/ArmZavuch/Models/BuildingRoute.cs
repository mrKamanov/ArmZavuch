namespace ArmZavuch.Models;

/// <summary>Матрица переходов между зданиями в минутах (ТЗ §3).</summary>
public sealed class BuildingRoute
{
    public int Id { get; set; }
    public int FromBuildingId { get; set; }
    public int ToBuildingId { get; set; }
    public int Minutes { get; set; }
    public string FromBuildingName { get; set; } = "";
    public string ToBuildingName { get; set; } = "";
}

public static class BuildingRouteDefaults
{
    public const int Minutes = 40;

    public static string MinutesText => Minutes.ToString();
}
