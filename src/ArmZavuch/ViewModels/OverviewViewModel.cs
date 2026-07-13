using System.Collections.ObjectModel;
using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Data;
using ArmZavuch.Services.Dialog;
using ArmZavuch.Services.Export;
using ArmZavuch.Services.Schedule;
using ArmZavuch.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArmZavuch.ViewModels;

/// <summary>Сводная простыня расписания: школа, педагоги, классы, здания.</summary>
public partial class OverviewViewModel : ObservableObject
{
    private const int MaxLessons = 8;

    private readonly WeekTemplateRepository _templates;
    private readonly SchoolClassRepository _classes;
    private readonly TeacherRepository _teachers;
    private readonly RoomRepository _rooms;
    private readonly BellRepository _bells;
    private readonly BellTemplateAssignmentService _bellAssignment;
    private readonly AppSettingsService _settings;
    private readonly OverviewScheduleExportService _overviewExport;
    private readonly IAppDialogService _dialogs;
    private readonly IAppDataChangeNotifier _dataChangeNotifier;
    private readonly IAppDataRevisionService _revision;
    private readonly OverviewFilter _overviewFilter = new();

    private long _loadedReferenceRevision = -1;
    private long _loadedScheduleRevision = -1;

    private List<SchoolClass> _classCache = [];
    private List<Teacher> _teacherCache = [];
    private List<Room> _roomCache = [];
    private List<BellPeriod> _bellCache = [];
    private List<LessonSlot> _slotCache = [];
    private bool _referenceDataLoaded;
    private bool _suppressTemplateChanged;
    private int _rebuildGeneration;

    public ObservableCollection<WeekTemplateInfo> Templates { get; } = [];

    [ObservableProperty] private ObservableCollection<OverviewColumnHeader> _overviewColumns = [];
    [ObservableProperty] private ObservableCollection<OverviewMatrixRow> _overviewMatrix = [];

    public string[] DayNames { get; } = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб"];

    [ObservableProperty] private WeekTemplateInfo? _selectedTemplate;
    [ObservableProperty] private string _overviewCaption = "";
    [ObservableProperty] private string _overviewMode = OverviewViewModes.School;
    [ObservableProperty] private string _overviewRowHeader = "Класс";
    [ObservableProperty] private string _overviewFilterPath = "";
    [ObservableProperty] private bool _isOverviewEmpty;
    [ObservableProperty] private string _overviewEmptyHint = "";
    [ObservableProperty] private double _overviewZoom = 1.0;
    [ObservableProperty] private bool _isOverviewBusy;
    [ObservableProperty] private bool _showBreaksInOverview;

    public bool IsSchoolOverviewMode => OverviewMode == OverviewViewModes.School;

    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand ExportExcelCommand { get; }
    public IRelayCommand ResetOverviewFilterCommand { get; }
    public IRelayCommand<OverviewMatrixRow> DrillOverviewRowCommand { get; }
    public IRelayCommand<OverviewTimelinePart> DrillOverviewPartCommand { get; }

    public OverviewViewModel(
        WeekTemplateRepository templates,
        SchoolClassRepository classes,
        TeacherRepository teachers,
        RoomRepository rooms,
        BellRepository bells,
        BellTemplateAssignmentService bellAssignment,
        AppSettingsService settings,
        OverviewScheduleExportService overviewExport,
        IAppDialogService dialogs,
        IAppDataChangeNotifier dataChangeNotifier,
        IAppDataRevisionService revision)
    {
        _templates = templates;
        _classes = classes;
        _teachers = teachers;
        _rooms = rooms;
        _bells = bells;
        _bellAssignment = bellAssignment;
        _settings = settings;
        _overviewExport = overviewExport;
        _dialogs = dialogs;
        _dataChangeNotifier = dataChangeNotifier;
        _revision = revision;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        ExportExcelCommand = new AsyncRelayCommand(ExportExcelAsync);
        ResetOverviewFilterCommand = new RelayCommand(ResetOverviewFilter);
        DrillOverviewRowCommand = new RelayCommand<OverviewMatrixRow>(DrillOverviewRow);
        DrillOverviewPartCommand = new RelayCommand<OverviewTimelinePart>(DrillOverviewPart);

        _dataChangeNotifier.DataChanged += OnAppDataChanged;
    }

    public async Task ActivateAsync()
    {
        if (!_referenceDataLoaded
            || _loadedReferenceRevision != _revision.ReferenceDataRevision)
        {
            if (LoadCommand.CanExecute(null))
                await LoadCommand.ExecuteAsync(null);
            _loadedReferenceRevision = _revision.ReferenceDataRevision;
            _loadedScheduleRevision = _revision.ScheduleRevision;
            return;
        }

        if (_loadedScheduleRevision != _revision.ScheduleRevision)
        {
            await ReloadSlotsAndRebuildAsync(SelectedTemplate?.Id);
            _loadedScheduleRevision = _revision.ScheduleRevision;
        }
    }

    private async void OnAppDataChanged(object? sender, EventArgs e)
    {
        if (LoadCommand.CanExecute(null))
            await LoadCommand.ExecuteAsync(null);
    }

    partial void OnOverviewModeChanged(string value)
    {
        if (value == OverviewViewModes.School)
            _overviewFilter.Clear();
        OnPropertyChanged(nameof(IsSchoolOverviewMode));
        _ = RebuildOverviewAsync();
    }

    partial void OnShowBreaksInOverviewChanged(bool value) => _ = RebuildOverviewAsync();

    partial void OnSelectedTemplateChanged(WeekTemplateInfo? value)
    {
        if (_suppressTemplateChanged)
            return;
        _ = ReloadSlotsAndRebuildAsync(value?.Id);
    }

    private async Task LoadAsync()
    {
        IsOverviewBusy = true;
        try
        {
            _suppressTemplateChanged = true;
            await ReloadReferenceDataAsync(forceTemplates: true);
            await RebuildOverviewAsync();
        }
        finally
        {
            _suppressTemplateChanged = false;
            IsOverviewBusy = false;
            _loadedReferenceRevision = _revision.ReferenceDataRevision;
            _loadedScheduleRevision = _revision.ScheduleRevision;
        }
    }

    private async Task ReloadSlotsAndRebuildAsync(int? templateId)
    {
        IsOverviewBusy = true;
        try
        {
            _slotCache = templateId is int id
                ? await _templates.GetAllSlotsForTemplateAsync(id)
                : [];
            await RebuildOverviewAsync();
        }
        finally
        {
            IsOverviewBusy = false;
            _loadedScheduleRevision = _revision.ScheduleRevision;
        }
    }

    private async Task ReloadReferenceDataAsync(bool forceTemplates = false)
    {
        var classesTask = _classes.GetAllAsync();
        var teachersTask = _teachers.GetAllAsync();
        var roomsTask = _rooms.GetAllAsync();
        var bellsTask = _bells.GetAllPeriodsAsync();
        Task<List<WeekTemplateInfo>>? templatesTask = forceTemplates || Templates.Count == 0
            ? _templates.GetTemplatesAsync()
            : null;

        await Task.WhenAll(
            classesTask,
            teachersTask,
            roomsTask,
            bellsTask,
            templatesTask ?? Task.FromResult<List<WeekTemplateInfo>>([]));

        _classCache = classesTask.Result;
        _teacherCache = teachersTask.Result;
        _roomCache = roomsTask.Result;
        _bellCache = bellsTask.Result;

        if (templatesTask is not null)
        {
            Templates.Clear();
            foreach (var template in templatesTask.Result)
                Templates.Add(template);

            var selectedId = SelectedTemplate?.Id;
            SelectedTemplate = selectedId is int id
                ? Templates.FirstOrDefault(t => t.Id == id) ?? Templates.FirstOrDefault()
                : Templates.FirstOrDefault();
        }

        _slotCache = SelectedTemplate is not null
            ? await _templates.GetAllSlotsForTemplateAsync(SelectedTemplate.Id)
            : [];

        _referenceDataLoaded = true;
    }

    private async Task RebuildOverviewAsync()
    {
        if (!_referenceDataLoaded)
            return;

        var generation = ++_rebuildGeneration;
        var mode = OverviewMode == OverviewViewModes.School ? OverviewViewModes.Classes : OverviewMode;
        var filterSnapshot = _overviewFilter.Clone();

        var buildResult = await Task.Run(() =>
        {
            var assignment = _bellAssignment.CreateSnapshot(_classCache, DateOnly.FromDateTime(DateTime.Today));
            return OverviewScheduleBuilder.Build(
                mode,
                _slotCache,
                _classCache,
                _teacherCache,
                _roomCache,
                _bellCache,
                DayNames,
                filterSnapshot,
                MaxLessons,
                showBreaks: OverviewMode == OverviewViewModes.School && ShowBreaksInOverview,
                assignment: assignment);
        }).ConfigureAwait(false);

        if (generation != _rebuildGeneration)
            return;

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (generation != _rebuildGeneration)
                return;

            ApplyBuildResult(buildResult, mode);
        });
    }

    private void ApplyBuildResult(
        (List<OverviewColumnHeader> DayHeaders, List<OverviewMatrixRow> Rows, string RowHeader) buildResult,
        string mode)
    {
        var (columns, rows, rowHeader) = buildResult;

        OverviewRowHeader = rowHeader;
        OverviewColumns = new ObservableCollection<OverviewColumnHeader>(columns);
        OverviewMatrix = new ObservableCollection<OverviewMatrixRow>(rows);

        var dataRows = rows.Count(r => !r.IsSectionHeader && !r.IsDayHeaderRow);
        IsOverviewEmpty = dataRows == 0;

        if (SelectedTemplate is null)
        {
            OverviewEmptyHint = _classCache.Count == 0
                ? "Нет классов. Добавьте их в Справочниках или импортируйте из Excel."
                : "Сетка расписания пока пуста — создайте шаблон недели в Конструкторе и расставьте уроки.\n\n" +
                  "После импорта из старого Excel здесь появятся классы, но уроки нужно собрать вручную или из нагрузки.";
            OverviewCaption = _classCache.Count == 0
                ? "Нет данных для сводки"
                : $"Нет шаблона недели · {_classCache.Count} классов · уроков в сетке: 0";
        }
        else if (_slotCache.Count == 0)
        {
            OverviewEmptyHint = mode switch
            {
                OverviewViewModes.Teachers =>
                    "В шаблоне нет уроков — переключитесь на «Вся школа» / «Классы» или заполните сетку в Конструкторе.",
                OverviewViewModes.Buildings =>
                    "В шаблоне нет уроков — переключитесь на «Классы» или заполните сетку в Конструкторе.",
                _ => "В шаблоне пока нет уроков. Откройте Конструктор, выберите этот шаблон и расставьте предметы по ячейкам."
            };
            var parity = SelectedTemplate.WeekParity != WeekTemplateParity.Any
                ? $" ({WeekTemplateParity.ToDisplay(SelectedTemplate.WeekParity)})"
                : "";
            OverviewCaption =
                $"{OverviewViewModes.ToDisplay(OverviewMode)} · {SelectedTemplate.Name}{parity} · {dataRows} строк · 0 уроков";
        }
        else
        {
            OverviewEmptyHint = "";
            var parity = SelectedTemplate!.WeekParity != WeekTemplateParity.Any
                ? $" ({WeekTemplateParity.ToDisplay(SelectedTemplate.WeekParity)})"
                : "";
            OverviewCaption =
                $"{OverviewViewModes.ToDisplay(OverviewMode)} · {SelectedTemplate.Name}{parity} · {dataRows} строк · {_slotCache.Count} уроков";
        }

        UpdateOverviewFilterPath();
    }

    private void ResetOverviewFilter()
    {
        _overviewFilter.Clear();
        OverviewMode = OverviewViewModes.School;
        _ = RebuildOverviewAsync();
    }

    private void DrillOverviewRow(OverviewMatrixRow? row)
    {
        if (row is null)
            return;

        _overviewFilter.Clear();
        switch (row.RowKind)
        {
            case OverviewRowKinds.Teacher:
                OverviewMode = OverviewViewModes.Teachers;
                _overviewFilter.TeacherId = row.EntityId;
                break;
            case OverviewRowKinds.Room:
                OverviewMode = OverviewViewModes.Buildings;
                _overviewFilter.RoomId = row.EntityId;
                _overviewFilter.BuildingName = row.BuildingName;
                break;
            default:
                OverviewMode = OverviewViewModes.Classes;
                _overviewFilter.ClassId = row.EntityId;
                break;
        }
        _ = RebuildOverviewAsync();
    }

    private void DrillOverviewPart(OverviewTimelinePart? part)
    {
        if (part is null)
            return;

        if (part.TeacherId is int teacherId
            && (OverviewMode != OverviewViewModes.Teachers || _overviewFilter.TeacherId != teacherId))
        {
            _overviewFilter.Clear();
            _overviewFilter.TeacherId = teacherId;
            OverviewMode = OverviewViewModes.Teachers;
            _ = RebuildOverviewAsync();
            return;
        }

        if (part.ClassId is int classId
            && (OverviewMode != OverviewViewModes.Classes || _overviewFilter.ClassId != classId))
        {
            _overviewFilter.Clear();
            _overviewFilter.ClassId = classId;
            OverviewMode = OverviewViewModes.Classes;
            _ = RebuildOverviewAsync();
            return;
        }

        if (part.RoomId is int roomId
            && (OverviewMode != OverviewViewModes.Buildings || _overviewFilter.RoomId != roomId))
        {
            _overviewFilter.Clear();
            _overviewFilter.RoomId = roomId;
            _overviewFilter.BuildingName = part.BuildingName;
            OverviewMode = OverviewViewModes.Buildings;
            _ = RebuildOverviewAsync();
        }
    }

    private void UpdateOverviewFilterPath()
    {
        if (!_overviewFilter.IsActive)
        {
            OverviewFilterPath = "";
            return;
        }

        var parts = new List<string> { OverviewViewModes.ToDisplay(OverviewMode) };
        if (_overviewFilter.ClassId is int classId)
            parts.Add(_classCache.FirstOrDefault(c => c.Id == classId)?.DisplayName ?? "класс");
        if (_overviewFilter.TeacherId is int teacherId)
            parts.Add(_teacherCache.FirstOrDefault(t => t.Id == teacherId)?.FullName ?? "педагог");
        if (_overviewFilter.RoomId is int roomId)
        {
            var room = _roomCache.FirstOrDefault(r => r.Id == roomId);
            parts.Add(room is null ? "кабинет" : $"{room.BuildingName} · каб.{room.Number}");
        }
        else if (!string.IsNullOrWhiteSpace(_overviewFilter.BuildingName))
            parts.Add(_overviewFilter.BuildingName);

        OverviewFilterPath = string.Join(" › ", parts);
    }

    private async Task ExportExcelAsync()
    {
        if (SelectedTemplate is null)
        {
            _dialogs.ShowWarning("Сводка", "Выберите шаблон недели для экспорта.");
            return;
        }

        if (_slotCache.Count == 0)
        {
            _dialogs.ShowWarning("Сводка", "В шаблоне нет уроков — нечего выгружать.");
            return;
        }

        IsOverviewBusy = true;
        try
        {
            var templateLabel = BuildTemplateExportLabel(SelectedTemplate);
            var schoolName = _settings.SchoolName;
            var sheets = await Task.Run(BuildSheets).ConfigureAwait(true);
            if (!_overviewExport.TryExport(schoolName, templateLabel, sheets))
                return;

            _dialogs.ShowInfo("Сводка", "Файл Excel сохранён: 4 листа (школа, педагоги, классы, здания).");
        }
        finally
        {
            IsOverviewBusy = false;
        }

        List<OverviewScheduleExportSheet> BuildSheets()
        {
            var assignment = _bellAssignment.CreateSnapshot(_classCache, DateOnly.FromDateTime(DateTime.Today));
            var emptyFilter = new OverviewFilter();
            return
            [
                BuildExportSheet(
                    OverviewViewModes.ToDisplay(OverviewViewModes.School),
                    OverviewViewModes.Classes,
                    showBreaks: true,
                    assignment,
                    emptyFilter),
                BuildExportSheet(
                    OverviewViewModes.ToDisplay(OverviewViewModes.Teachers),
                    OverviewViewModes.Teachers,
                    showBreaks: false,
                    assignment,
                    emptyFilter),
                BuildExportSheet(
                    OverviewViewModes.ToDisplay(OverviewViewModes.Classes),
                    OverviewViewModes.Classes,
                    showBreaks: false,
                    assignment,
                    emptyFilter),
                BuildExportSheet(
                    OverviewViewModes.ToDisplay(OverviewViewModes.Buildings),
                    OverviewViewModes.Buildings,
                    showBreaks: false,
                    assignment,
                    emptyFilter)
            ];
        }
    }

    private OverviewScheduleExportSheet BuildExportSheet(
        string sheetTitle,
        string builderMode,
        bool showBreaks,
        BellTemplateAssignmentSnapshot assignment,
        OverviewFilter filter)
    {
        var (columns, rows, rowHeader) = OverviewScheduleBuilder.Build(
            builderMode,
            _slotCache,
            _classCache,
            _teacherCache,
            _roomCache,
            _bellCache,
            DayNames,
            filter,
            MaxLessons,
            showBreaks: showBreaks,
            assignment: assignment);

        return new OverviewScheduleExportSheet(sheetTitle, rowHeader, columns, rows);
    }

    private static string BuildTemplateExportLabel(WeekTemplateInfo template)
    {
        var parity = template.WeekParity != WeekTemplateParity.Any
            ? $" ({WeekTemplateParity.ToDisplay(template.WeekParity)})"
            : "";
        return $"{template.DisplayName}{parity}";
    }
}
