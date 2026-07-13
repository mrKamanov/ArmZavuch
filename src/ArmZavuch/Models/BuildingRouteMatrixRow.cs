using CommunityToolkit.Mvvm.ComponentModel;

namespace ArmZavuch.Models;

/// <summary>Строка матрицы переходов «из здания → в здание, минут».</summary>
public partial class BuildingRouteMatrixRow : ObservableObject
{
    public int FromBuildingId { get; init; }
    public string FromBuildingName { get; init; } = "";
    public string FromBuildingColorHex { get; init; } = "#2563EB";
    public int ToBuildingId { get; init; }
    public string ToBuildingName { get; init; } = "";
    public string ToBuildingColorHex { get; init; } = "#2563EB";
    public string RouteLabel => $"{FromBuildingName} → {ToBuildingName}";

    [ObservableProperty] private string _minutes = "";
}
