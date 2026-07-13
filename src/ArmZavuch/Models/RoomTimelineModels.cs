using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArmZavuch.Models;

public enum RoomSearchGradeBand
{
    Grade1,
    Senior2To11
}

public sealed class RoomSearchGradeBandOption
{
    public required RoomSearchGradeBand Band { get; init; }
    public required string DisplayName { get; init; }
}

/// <summary>Общая временная шкала дня для шахматки кабинетов.</summary>
public sealed class RoomTimelineLayout
{
    public const double PixelsPerMinute = 2.8;

    public TimeSpan DayStart { get; init; }
    public TimeSpan DayEnd { get; init; }
    public double TotalWidthPixels { get; init; }
    public string DayRangeLabel { get; init; } = "";
    public IList<RoomTimeAxisTick> Ticks { get; init; } = [];
    public IList<RoomTimelineHourBand> HourBands { get; init; } = [];

    public double ToPixels(TimeSpan time) =>
        Math.Max(0, (time - DayStart).TotalMinutes * PixelsPerMinute);
}

/// <summary>Часовая полоса фона временной шкалы (чередование для ориентира).</summary>
public sealed class RoomTimelineHourBand
{
    public double LeftPixels { get; init; }
    public double WidthPixels { get; init; }
    public string BackgroundHex { get; init; } = "#FFFFFF";
}

/// <summary>Отметка на оси времени (час или получас).</summary>
public sealed class RoomTimeAxisTick
{
    public double LeftPixels { get; init; }
    public string Label { get; init; } = "";
    public bool IsMajor { get; init; }
}

/// <summary>Блок занятости кабинета на временной шкале.</summary>
public sealed class RoomOccupancyBlock : ObservableObject
{
    public TimeSpan Start { get; init; }
    public TimeSpan End { get; init; }
    public double LeftPixels { get; init; }
    public double WidthPixels { get; init; }
    public string TimeDisplay { get; init; } = "";
    public string PrimaryLine { get; init; } = "";
    public string SecondaryLine { get; init; } = "";
    public string TeacherLine { get; init; } = "";
    public bool IsDynamicPause { get; init; }
    public string ToolTip { get; init; } = "";

    private bool _isSearchMatch;
    public bool IsSearchMatch
    {
        get => _isSearchMatch;
        set => SetProperty(ref _isSearchMatch, value);
    }
}

/// <summary>Строка шахматки: кабинет и блоки занятости.</summary>
public sealed class RoomOccupancyRow : ObservableObject
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = "";
    public string BuildingName { get; set; } = "";
    public string BuildingColorHex { get; set; } = "#94A3B8";
    public int Capacity { get; set; }
    public double TimelineWidthPixels { get; set; }

    private bool _isSearchResult;
    public bool IsSearchResult
    {
        get => _isSearchResult;
        set => SetProperty(ref _isSearchResult, value);
    }

    private bool _isHidden;
    public bool IsHidden
    {
        get => _isHidden;
        set => SetProperty(ref _isHidden, value);
    }

    private bool _showSearchBand;
    public bool ShowSearchBand
    {
        get => _showSearchBand;
        set => SetProperty(ref _showSearchBand, value);
    }

    private double _searchBandLeft;
    public double SearchBandLeft
    {
        get => _searchBandLeft;
        set => SetProperty(ref _searchBandLeft, value);
    }

    private double _searchBandWidth;
    public double SearchBandWidth
    {
        get => _searchBandWidth;
        set => SetProperty(ref _searchBandWidth, value);
    }

    public ObservableCollection<RoomOccupancyBlock> Blocks { get; } = [];
}

/// <summary>Группа кабинетов одного здания (сворачиваемая).</summary>
public sealed class RoomBuildingGroup : ObservableObject
{
    public string BuildingName { get; init; } = "";
    public string BuildingColorHex { get; init; } = "#94A3B8";
    public ObservableCollection<RoomOccupancyRow> Rooms { get; } = [];

    private bool _isCollapsed;
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (!SetProperty(ref _isCollapsed, value))
                return;
            OnPropertyChanged(nameof(CollapseToggleGlyph));
            OnPropertyChanged(nameof(AreRoomsVisible));
        }
    }

    public bool AreRoomsVisible => !IsCollapsed;
    public string CollapseToggleGlyph => IsCollapsed ? "▸" : "▾";
    public int RoomCount => Rooms.Count;
}
