using System.Collections.ObjectModel;
using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Data;
using ArmZavuch.Services.Rooms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArmZavuch.ViewModels;

/// <summary>Шахматка кабинетов по времени и поиск свободных.</summary>
public partial class RoomsViewModel : ObservableObject
{
    private readonly RoomOccupancyService _occupancy;
    private readonly BuildingRepository _buildings;
    private readonly IAppDataChangeNotifier _dataChangeNotifier;
    private HashSet<int> _freeRoomIds = [];
    private TimeSpan? _searchWindowStart;
    private TimeSpan? _searchWindowEnd;

    private RoomTimelineLayout _timelineLayout = new();

    public ObservableCollection<RoomBuildingGroup> BuildingGroups { get; } = [];
    public ObservableCollection<RoomTimeAxisTick> TimelineTicks { get; } = [];
    public ObservableCollection<RoomTimelineHourBand> TimelineHourBands { get; } = [];
    public ObservableCollection<Room> FreeRooms { get; } = [];
    public ObservableCollection<int> LessonNumbers { get; } = [];
    public ObservableCollection<string> BuildingNames { get; } = [];
    public ObservableCollection<RoomSearchGradeBandOption> GradeBandOptions { get; } = [];

    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private int _searchLesson = 1;
    [ObservableProperty] private RoomSearchGradeBandOption? _selectedGradeBand;
    [ObservableProperty] private string _searchTimeFromText = "";
    [ObservableProperty] private string _searchTimeToText = "";
    [ObservableProperty] private bool _useTimeRangeSearch;
    public string TimeRangeSearchHint =>
        "Формат: ЧЧ:ММ (24 часа). Пример для 2-го урока: с 09:15 по 09:50. " +
        "Можно также 915 → 09:15. Обязательно включите «Искать по времени».";
    [ObservableProperty] private bool _useCapacityFilter = true;
    [ObservableProperty] private string _minCapacityText = "25";
    [ObservableProperty] private string? _selectedBuildingFilter;
    [ObservableProperty] private string? _selectedSearchBuilding;
    [ObservableProperty] private bool _isSchoolDay = true;
    [ObservableProperty] private string _dayStatusText = "";
    [ObservableProperty] private string _searchSummary = "Укажите урок или время и нажмите «Найти»";
    [ObservableProperty] private bool _showOnlyFreeForSearch;
    [ObservableProperty] private bool _searchActive;
    [ObservableProperty] private double _timelineWidthPixels = 640;
    [ObservableProperty] private string _timelineRangeLabel = "";

    public int FreeRoomsCount => FreeRooms.Count;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand SearchFreeCommand { get; }
    public IRelayCommand ClearSearchCommand { get; }
    public IRelayCommand ToggleShowOnlyFreeCommand { get; }
    public IRelayCommand<RoomBuildingGroup> ToggleBuildingGroupCommand { get; }

    public RoomsViewModel(
        RoomOccupancyService occupancy,
        BuildingRepository buildings,
        IAppDataChangeNotifier dataChangeNotifier)
    {
        _occupancy = occupancy;
        _buildings = buildings;
        _dataChangeNotifier = dataChangeNotifier;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        SearchFreeCommand = new AsyncRelayCommand(SearchFreeAsync);
        ClearSearchCommand = new RelayCommand(ClearSearch);
        ToggleShowOnlyFreeCommand = new RelayCommand(ToggleShowOnlyFree);
        ToggleBuildingGroupCommand = new RelayCommand<RoomBuildingGroup>(ToggleBuildingGroup);

        GradeBandOptions.Add(new RoomSearchGradeBandOption
        {
            Band = RoomSearchGradeBand.Grade1,
            DisplayName = "1 класс"
        });
        GradeBandOptions.Add(new RoomSearchGradeBandOption
        {
            Band = RoomSearchGradeBand.Senior2To11,
            DisplayName = "2–11 классы"
        });
        SelectedGradeBand = GradeBandOptions[0];

        for (var i = 1; i <= 12; i++)
            LessonNumbers.Add(i);

        _dataChangeNotifier.DataChanged += (_, _) => _ = ReloadAllAsync();
        _ = InitializeAsync();
    }

    public Task ActivateAsync() => ReloadAllAsync();

    partial void OnSelectedDateChanged(DateTime value) => _ = RefreshAsync();
    partial void OnSelectedBuildingFilterChanged(string? value) => _ = RefreshAsync();
    partial void OnShowOnlyFreeForSearchChanged(bool value) => ApplyRowVisibility();

    partial void OnSearchTimeFromTextChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            UseTimeRangeSearch = true;
    }

    partial void OnSearchTimeToTextChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            UseTimeRangeSearch = true;
    }

    partial void OnUseTimeRangeSearchChanged(bool value)
    {
        if (value && string.IsNullOrWhiteSpace(SearchTimeFromText) && string.IsNullOrWhiteSpace(SearchTimeToText))
            _ = PrefillTimeRangeFromLessonAsync();
    }

    private async Task PrefillTimeRangeFromLessonAsync()
    {
        var band = SelectedGradeBand?.Band ?? RoomSearchGradeBand.Senior2To11;
        var window = await _occupancy.ResolveLessonWindowAsync(SearchLesson, band);
        if (window is null)
            return;

        SearchTimeFromText = window.Value.Start.ToString(@"hh\:mm");
        SearchTimeToText = window.Value.End.ToString(@"hh\:mm");
    }

    private async Task InitializeAsync() => await ReloadAllAsync();

    private async Task ReloadAllAsync()
    {
        BuildingNames.Clear();
        BuildingNames.Add("Все здания");
        foreach (var b in await _buildings.GetAllAsync())
            BuildingNames.Add(b.Name);

        if (SelectedBuildingFilter is null || !BuildingNames.Contains(SelectedBuildingFilter))
            SelectedBuildingFilter = BuildingNames[0];
        if (SelectedSearchBuilding is null || !BuildingNames.Contains(SelectedSearchBuilding))
            SelectedSearchBuilding = BuildingNames[0];

        ClearSearch();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var date = DateOnly.FromDateTime(SelectedDate);
        var buildingFilter = NormalizeBuildingFilter(SelectedBuildingFilter);
        var (isSchoolDay, layout, groups) = await _occupancy.GetTimelineAsync(date, buildingFilter);
        IsSchoolDay = isSchoolDay;
        _timelineLayout = layout;
        TimelineWidthPixels = layout.TotalWidthPixels;
        TimelineRangeLabel = layout.DayRangeLabel;
        DayStatusText = isSchoolDay
            ? $"Школьный день · {layout.DayRangeLabel}"
            : "Выходной или каникулы — занятость может быть пустой";

        TimelineTicks.Clear();
        foreach (var tick in layout.Ticks)
            TimelineTicks.Add(tick);

        TimelineHourBands.Clear();
        foreach (var band in layout.HourBands)
            TimelineHourBands.Add(band);

        BuildingGroups.Clear();
        foreach (var group in groups)
            BuildingGroups.Add(group);

        ApplySearchHighlights();
        ApplyRowVisibility();
    }

    private async Task SearchFreeAsync()
    {
        if (!await TryResolveSearchWindowAsync())
            return;

        int? minCapacity = null;
        if (UseCapacityFilter)
        {
            if (!TryParseMinCapacity(out var parsed))
                return;
            minCapacity = parsed;
        }

        var date = DateOnly.FromDateTime(SelectedDate);
        var building = NormalizeBuildingFilter(SelectedSearchBuilding);
        var rooms = await _occupancy.FindFreeRoomsAsync(
            date,
            _searchWindowStart,
            _searchWindowEnd,
            minCapacity,
            building);

        FreeRooms.Clear();
        foreach (var r in rooms)
            FreeRooms.Add(r);

        _freeRoomIds = rooms.Select(r => r.Id).ToHashSet();
        SearchActive = true;
        SearchSummary = rooms.Count > 0
            ? $"Найдено {rooms.Count} свободных каб.\n{FormatSearchWindow()}"
            : $"Свободных кабинетов нет\n{FormatSearchWindow()}";

        ApplySearchHighlights();
        ApplyRowVisibility();
        OnPropertyChanged(nameof(FreeRoomsCount));
    }

    private async Task<bool> TryResolveSearchWindowAsync()
    {
        if (UseTimeRangeSearch)
        {
            if (!RoomTimelineBuilder.TryParseSearchTime(SearchTimeFromText, out var from))
            {
                SearchSummary = "Время «с»: укажите ЧЧ:ММ, например 09:15";
                return false;
            }

            if (!RoomTimelineBuilder.TryParseSearchTime(SearchTimeToText, out var to))
            {
                SearchSummary = "Время «по»: укажите ЧЧ:ММ, например 09:50";
                return false;
            }

            if (to <= from)
            {
                SearchSummary = "Время «по» должно быть позже времени «с»";
                return false;
            }

            _searchWindowStart = from;
            _searchWindowEnd = to;
            return true;
        }

        var bells = await _occupancy.GetBellPeriodsAsync();
        var band = SelectedGradeBand?.Band ?? RoomSearchGradeBand.Grade1;
        var window = RoomTimelineBuilder.ResolveLessonWindow(bells, SearchLesson, band);
        if (window is null)
        {
            SearchSummary = band == RoomSearchGradeBand.Grade1
                ? $"Не найден звонок для урока {SearchLesson} (1 класс)"
                : $"Не найден звонок для урока {SearchLesson} (2–11 классы)";
            return false;
        }

        _searchWindowStart = window.Value.Start;
        _searchWindowEnd = window.Value.End;
        return true;
    }

    private void ClearSearch()
    {
        _freeRoomIds.Clear();
        _searchWindowStart = null;
        _searchWindowEnd = null;
        SearchActive = false;
        ShowOnlyFreeForSearch = false;
        FreeRooms.Clear();
        SearchSummary = "Укажите урок или включите «Искать по времени» и задайте интервал";
        ApplySearchHighlights();
        ApplyRowVisibility();
        OnPropertyChanged(nameof(FreeRoomsCount));
    }

    private void ToggleShowOnlyFree()
    {
        if (!SearchActive)
            return;
        ShowOnlyFreeForSearch = !ShowOnlyFreeForSearch;
    }

    private static void ToggleBuildingGroup(RoomBuildingGroup? group)
    {
        if (group is null)
            return;
        group.IsCollapsed = !group.IsCollapsed;
    }

    private void ApplySearchHighlights()
    {
        var bandLeft = 0.0;
        var bandWidth = 0.0;
        if (SearchActive
            && _searchWindowStart is TimeSpan start
            && _searchWindowEnd is TimeSpan end)
        {
            (bandLeft, bandWidth) = RoomTimelineBuilder.SearchBandPixels(_timelineLayout, start, end);
        }

        foreach (var building in BuildingGroups)
        {
            foreach (var row in building.Rooms)
            {
                row.IsSearchResult = SearchActive && _freeRoomIds.Contains(row.RoomId);
                row.ShowSearchBand = SearchActive;
                row.SearchBandLeft = bandLeft;
                row.SearchBandWidth = bandWidth;

                foreach (var block in row.Blocks)
                {
                    var overlapsSearch = SearchActive
                                         && _searchWindowStart is TimeSpan ws
                                         && _searchWindowEnd is TimeSpan we
                                         && RoomTimelineBuilder.Overlaps(block.Start, block.End, ws, we);
                    block.IsSearchMatch = overlapsSearch && row.IsSearchResult;
                }
            }
        }
    }

    private void ApplyRowVisibility()
    {
        foreach (var building in BuildingGroups)
        {
            foreach (var row in building.Rooms)
                row.IsHidden = ShowOnlyFreeForSearch && SearchActive && !row.IsSearchResult;
        }
    }

    private bool TryParseMinCapacity(out int minCapacity)
    {
        if (int.TryParse(MinCapacityText.Trim(), out minCapacity) && minCapacity >= 0)
            return true;

        minCapacity = 0;
        SearchSummary = "«Мин. мест» — целое число от 0";
        return false;
    }

    private string FormatSearchWindow()
    {
        if (_searchWindowStart is not TimeSpan start || _searchWindowEnd is not TimeSpan end)
            return "выбранный интервал";

        if (!UseTimeRangeSearch)
        {
            var band = SelectedGradeBand?.DisplayName ?? "1 класс";
            return $"Урок {SearchLesson} · {band}\n{start:hh\\:mm}–{end:hh\\:mm}";
        }

        return $"{start:hh\\:mm}–{end:hh\\:mm}";
    }

    private static string? NormalizeBuildingFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "Все здания" ? null : value.Trim();
}
