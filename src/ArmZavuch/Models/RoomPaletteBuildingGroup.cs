using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArmZavuch.Models;

/// <summary>Кабинеты палитры конструктора, сгруппированные по зданию.</summary>
public sealed partial class RoomPaletteBuildingGroup : ObservableObject
{
    public string BuildingName { get; init; } = "";
    public string BuildingColorHex { get; init; } = "#94A3B8";
    public int RoomCount { get; init; }
    public ObservableCollection<RoomPaletteItem> Rooms { get; } = [];

    public string HeaderText => RoomCount > 0 ? $"{BuildingName} ({RoomCount})" : BuildingName;

    [ObservableProperty] private bool _isExpanded = true;
}
