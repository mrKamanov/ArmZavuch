using System.Collections.ObjectModel;
using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Data;
using ArmZavuch.Services.Dialog;
using ArmZavuch.Services.Export;
using ArmZavuch.Services.Save;
using ArmZavuch.Services.Settings;
using ArmZavuch.Services.Schedule;
using ArmZavuch.Services.Staff;
using ArmZavuch.Services.Navigation;
using ArmZavuch.Services.Scoring;
using ArmZavuch.Services.Validation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArmZavuch.ViewModels;

/// <summary>Диспетчерская: день, пресеты, замены (ТЗ §4 модуль 2, §5).</summary>
public partial class DispatcherViewModel : ObservableObject
{
    private readonly DayScheduleResolver _schedule;
    private readonly DayOverrideRepository _overrides;
    private readonly TeacherRepository _teachers;
    private readonly BellRepository _bells;
    private readonly BellTemplateAssignmentService _bellAssignment;
    private readonly SubstitutionScorer _scorer;
    private readonly SubstitutionExportService _export;
    private readonly AppSettingsService _settings;
    private readonly ISaveStateService _saveState;
    private readonly IAppDialogService _dialogs;
    private readonly IModuleNavigationService _navigation;
    private readonly BuildingRepository _buildings;
    private readonly RoomRepository _rooms;
    private readonly BuildingTransitionChecker _transitionChecker;
    private readonly IAppDataRevisionService _revision;
    private readonly SchoolClassRepository _classes;
    private readonly TeacherUnavailabilityRepository _unavailability;
    private readonly TeacherAbsenceService _absences;
    private readonly SubstitutionRecordRepository _substitutionRecords;
    private readonly SubstitutionReportService _substitutionReport;
    private readonly ScheduleConflictDetector _conflictDetector;
    private readonly CalendarCountdownService _calendarCountdown;
    private readonly PeriodGradeReminderService _periodGradeReminder;

    private bool _isInitialized;
    private long _loadedReferenceRevision = -1;

    private List<BellPeriod> _bellPeriods = [];
    private List<SchoolClass> _classCache = [];
    private BellTemplateAssignmentSnapshot _bellAssignmentSnapshot = BellTemplateAssignmentSnapshot.Fallback;
    private IReadOnlyDictionary<(string From, string To), int> _routeMap =
        new Dictionary<(string From, string To), int>();

    private List<LessonSlot> _dayLessons = [];
    private List<Room> _allRooms = [];
    private List<TeacherUnavailability> _teacherUnavailabilities = [];
    private Dictionary<string, string> _buildingColors = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private int _activeSection;
    [ObservableProperty] private string _monitorMode = DispatcherMonitorModes.Teachers;
    [ObservableProperty] private string _dayEditorScope = "All";
    [ObservableProperty] private SchoolClass? _dayEditorClass;
    [ObservableProperty] private string? _dayEditorBuildingName;
    [ObservableProperty] private Teacher? _selectedTeacher;
    [ObservableProperty] private LessonSlot? _selectedLesson;
    [ObservableProperty] private SubstitutionCandidate? _selectedCandidate;
    [ObservableProperty] private string _absenceNote = "";
    [ObservableProperty] private string _absenceType = StaffStatusTypes.Sick;
    [ObservableProperty] private bool _absenceIsOfficial;
    [ObservableProperty] private bool _replacementIsOfficial = true;
    [ObservableProperty] private DateTime? _absencePeriodStart;
    [ObservableProperty] private DateTime? _absencePeriodEnd;
    [ObservableProperty] private bool _absencePeriodOpenEnded;
    [ObservableProperty] private DateTime? _reportFromDate;
    [ObservableProperty] private DateTime? _reportToDate;
    [ObservableProperty] private int _replacementSideTab;
    [ObservableProperty] private int _journalReportTab;
    [ObservableProperty] private TeacherAbsenceListItem? _selectedAbsentTeacher;
    [ObservableProperty] private int _cancelLessonsCount = 2;
    [ObservableProperty] private int _pendingReplacements;
    [ObservableProperty] private string _dayNoteText = "";
    [ObservableProperty] private int _assignedReplacements;
    [ObservableProperty] private int _activeLessonsCount;
    [ObservableProperty] private bool _isSchoolDay = true;
    [ObservableProperty] private string _dayStatusText = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _selectedBellTemplate = "Сокращённый";
    [ObservableProperty] private string _activeShortenedTemplateName = "";
    [ObservableProperty] private int _trimLessonMinutes = 5;
    [ObservableProperty] private int _trimBreakMinutes = 5;
    [ObservableProperty] private int _fixedLessonMinutes = 40;
    [ObservableProperty] private int _fixedBreakMinutes = 10;
    [ObservableProperty] private int _maxLessonsForDay = 6;
    [ObservableProperty] private int _moveToLessonNumber = 1;
    [ObservableProperty] private LessonSlot? _selectedSwapLesson;
    [ObservableProperty] private Teacher? _editDayTeacher;
    [ObservableProperty] private Room? _editDayRoom;
    [ObservableProperty] private string? _editDayBuildingFilter;
    [ObservableProperty] private bool _hasDayTransitionWarning;
    [ObservableProperty] private string _dayTransitionSummary = "";
    [ObservableProperty] private bool _hasDayShiftWarning;
    [ObservableProperty] private string _dayShiftSummary = "";
    [ObservableProperty] private bool _hasNearestCalendarEvent;
    [ObservableProperty] private string _nearestCalendarLabel = "";
    [ObservableProperty] private string _nearestCalendarValue = "";
    [ObservableProperty] private string _nearestCalendarHint = "";
    [ObservableProperty] private string _nearestCalendarAccentBackground = "#F0F9FF";
    [ObservableProperty] private string _nearestCalendarAccentForeground = "#0369A1";
    [ObservableProperty] private bool _hasPeriodGradeReminder;
    [ObservableProperty] private string _periodGradeReminderLabel = "";
    [ObservableProperty] private string _periodGradeReminderValue = "";
    [ObservableProperty] private string _periodGradeReminderHint = "";
    [ObservableProperty] private string _periodGradeReminderAccentBackground = "#FFF1F2";
    [ObservableProperty] private string _periodGradeReminderAccentForeground = "#BE123C";

    public ObservableCollection<string> DayTransitionMessages { get; } = [];
    public ObservableCollection<string> DayShiftMessages { get; } = [];

    private const int BellsDirectoryTabIndex = 6;
    private const int MaxTrimMinutes = 15;
    private const int MinFixedLessonMinutes = 15;
    private const int MaxFixedLessonMinutes = 60;
    private const int MinFixedBreakMinutes = 5;
    private const int MaxFixedBreakMinutes = 30;
    private const int MaxLessonsLimit = 8;
    public ObservableCollection<string> BellTemplateNames { get; } = [];
    public ObservableCollection<string> BellPreviewLines { get; } = [];
    public ObservableCollection<string> BuildingNames { get; } = [];
    public ObservableCollection<DayOverrideRecord> DayOverrides { get; } = [];
    public ObservableCollection<LessonSlot> Lessons { get; } = [];
    public ObservableCollection<LessonSlot> TeacherDayLessons { get; } = [];
    public ObservableCollection<Teacher> Teachers { get; } = [];
    public ObservableCollection<SubstitutionCandidate> Candidates { get; } = [];
    public ObservableCollection<SubstitutionCandidate> FinishedDayCandidates { get; } = [];
    public ObservableCollection<SchoolClass> Classes { get; } = [];
    public ObservableCollection<LessonSlot> ReplacementQueue { get; } = [];
    public ObservableCollection<LessonSlot> SwapLessonOptions { get; } = [];
    public ObservableCollection<Room> FilteredDayRooms { get; } = [];
    public ObservableCollection<DispatcherStatItem> StatItems { get; } = [];
    public ObservableCollection<string> PendingReplacementLines { get; } = [];
    public ObservableCollection<DispatcherMonitorSection> MonitorSections { get; } = [];
    public ObservableCollection<DispatcherMonitorSection> DayEditorSections { get; } = [];
    public ObservableCollection<TeacherAbsenceListItem> AbsentTeachersToday { get; } = [];
    public ObservableCollection<SubstitutionRecord> SubstitutionReportPreview { get; } = [];
    public ObservableCollection<AbsenceHistoryRow> AbsenceReportPreview { get; } = [];
    public ObservableCollection<StaffActivitySummaryRow> StaffActivitySummary { get; } = [];
    public ObservableCollection<StaffBarChartPoint> TopAbsenteesChart { get; } = [];
    public ObservableCollection<StaffBarChartPoint> TopSubstitutorsChart { get; } = [];

    public bool IsMonitorSection => ActiveSection == DispatcherSections.Monitor;
    public bool IsReplacementsSection => ActiveSection == DispatcherSections.Replacements;
    public bool IsDayEditorSection => ActiveSection == DispatcherSections.DayEditor;
    public bool IsDayEditorClassScope => DayEditorScope == "Class";
    public bool IsDayEditorBuildingScope => DayEditorScope == "Building";

    [ObservableProperty] private SchoolClass? _selectedClass;

    public bool HasSelectedLesson => SelectedLesson is not null;
    public bool CanCancelSelectedLesson =>
        SelectedLesson is not null && !SelectedLesson.IsCancelled && IsDayEditorSection;
    public bool HasSelectedCandidate => SelectedCandidate is not null;
    public bool HasCandidates => Candidates.Count > 0 || FinishedDayCandidates.Count > 0;
    public bool HasFinishedDayCandidates => FinishedDayCandidates.Count > 0;
    public bool CanAssignReplacement => SelectedLesson is not null && SelectedCandidate is not null;
    public bool CanRemoveReplacement => SelectedLesson?.HasAssignedReplacement == true;
    public bool CanClearAllSubstitutionsForDay => AssignedReplacements > 0 || _daySubstitutionRecordCount > 0;
    public bool CanCancelTeacherAbsence => SelectedTeacher is not null && HasTeacherAbsenceOverride;

    public bool CanCloseAbsenceEarly =>
        SelectedTeacher is not null && CanCloseAbsenceEarlyFor(_activeTeacherAbsence, DateOnly.FromDateTime(SelectedDate));

    public bool HasTeacherAbsenceOverride =>
        SelectedTeacher is not null && _activeTeacherAbsence is not null;

    private TeacherStatusPeriod? _activeTeacherAbsence;
    private StaffJournalReportBundle? _journalReportBundle;
    private DayBellAdjustment? _activeDayBellAdjustment;
    private int _daySubstitutionRecordCount;
    private bool _loadingDayNote;

    public string DaySummaryText
    {
        get
        {
            if (!IsSchoolDay)
                return DayStatusText;
            if (PendingReplacements > 0)
                return $"Без замены: {PendingReplacements}";
            if (AssignedReplacements > 0)
                return $"Замен назначено: {AssignedReplacements}";
            return "Замены не требуются";
        }
    }

    public string DayNoteHint =>
        SelectedDate.Date == DateTime.Today
            ? "Заметка на сегодня"
            : $"Заметка на {SelectedDate:dd.MM.yyyy}";

    public bool HasPendingReplacementDetails => PendingReplacementLines.Count > 0;

    public string ModuleHint => ActiveSection switch
    {
        DispatcherSections.Replacements =>
            "Выберите учителя — слева его уроки на день. Кликните урок без замены и назначьте кандидата справа.",
        DispatcherSections.DayEditor =>
            "Конструктор дня: фильтр по классу или зданию, перестановки, звонки, смена учителя или кабинета только на эту дату.",
        _ =>
            "Монитор дня: цвет — здание, красное — нужна замена (🤒 больничный, 📋 отгул), зелёное — замена назначена. Клик по ячейке откроет замены или редактор."
    };

    public string MonitorHint =>
        "Сетка разбита по звонкам: 1 класс, начальная 2–4 и 2–11 — отдельные блоки; при двухсменке — блоки по сменам. Цвет полоски — здание.";

    public string ReplacementsHint =>
        "Кликните отсутствующего сверху или выберите учителя. Справа — отсутствие и подбор замены. Журнал — отдельная подвкладка.";

    public string DayEditorGridHint =>
        "Клик — выбрать урок или класс; перетащите урок на другой для обмена. Клик по пустому месту — сброс.";

    public string DayEditorSelectionHint =>
        SelectedLesson is not null
            ? $"Урок {SelectedLesson.LessonNumber} · {SelectedLesson.ClassName} — перетащите на другой слот"
            : SelectedClass is not null
                ? $"Класс {SelectedClass.DisplayName} — отмена уроков и правки справа для этого класса"
                : "";

    public string FooterHint =>
        "F5 — обновить · Enter — назначить замену · Ctrl+P — Excel · Ctrl+Shift+C — копировать схему";

    public string AbsenceFormHint =>
        "Быстрая отметка — одна дата. Период — диапазон или открытый больничный. " +
        "Досрочное закрытие сохраняет прошедшие дни; «Снять с даты» — если отметка ошибочна или учитель уже на месте.";

    public string ActiveAbsencePeriodHint =>
        _activeTeacherAbsence is null
            ? ""
            : CanCloseAbsenceEarly
                ? $"Период: {_activeTeacherAbsence.DateRangeDisplay}. Выберите в календаре дня последний день отсутствия и нажмите «Закрыть досрочно»."
                : $"Период: {_activeTeacherAbsence.DateRangeDisplay}.";

    public string JournalHint =>
        "Замены попадают в журнал при «Назначить замену». Отсутствия — из отметок в диспетчерской и анкетах. " +
        "Excel: листы «Замены», «Отсутствия», «Сводка» с блоками для диаграмм.";

    public string JournalSubstitutionTotalsLine =>
        _journalReportBundle?.SubstitutionTotalsLine ?? "Нажмите «Показать» для загрузки журнала";

    public int JournalSubstitutionTotal => _journalReportBundle?.Substitutions.Count ?? 0;

    public int JournalOfficialSubstitutionTotal => _journalReportBundle?.OfficialSubstitutionTotal ?? 0;

    public int JournalUnofficialSubstitutionTotal => _journalReportBundle?.UnofficialSubstitutionTotal ?? 0;

    public int JournalAbsencePeriodCount => _journalReportBundle?.Absences.Count ?? 0;

    public int JournalTeacherActivityCount => _journalReportBundle?.Summary.Count ?? 0;

    public string AbsentTeachersCaption =>
        AbsentTeachersToday.Count == 0
            ? "На эту дату отсутствующих не отмечено"
            : $"Отсутствуют сегодня: {AbsentTeachersToday.Count}";

    public string DayChangesHint =>
        "«Сократить на» — вычесть минуты из шаблона. «Задать по» — все уроки или перемены одной длины. Можно комбинировать; время пересчитывается цепочкой.";

    public string FixedDurationHint =>
        "Старт первого урока сохраняется; длительности выравниваются, затем сдвигаются последующие слоты.";

    public string BellsHint =>
        "Быстрые кнопки — для типовых случаев. Шаблон целиком — если заранее настроен в Справочниках → Звонки.";

    public string DayEditHint =>
        "Перестановки действуют только на выбранный день. Выберите урок в таблице слева, затем поменяйте местами с другим или перенесите на другой номер урока.";

    public string ShortenedDayStatus => _activeDayBellAdjustment?.DisplayLine
        ?? (string.IsNullOrWhiteSpace(ActiveShortenedTemplateName)
            ? "Обычные звонки"
            : $"Шаблон «{ActiveShortenedTemplateName}»");

    public bool IsShortenedDayActive =>
        _activeDayBellAdjustment is not null || !string.IsNullOrWhiteSpace(ActiveShortenedTemplateName);

    public bool IsDynamicPauseSkipped => _activeDayBellAdjustment?.SkipDynamicPause == true;

    public bool CanSkipDynamicPause => !IsDynamicPauseSkipped;

    public bool CanUndoLastDayChange => ScheduleOverrideCount > 0;

    public bool CanResetDayChanges => ScheduleOverrideCount > 0 || _daySubstitutionRecordCount > 0;

    public int ScheduleOverrideCount =>
        DayOverrides.Count(r => r.OverrideType != "DayNote");

    public string LastDayChangeHint => DayOverrides.LastOrDefault()?.DisplayLine is { } line
        ? $"Последнее: {line}"
        : "Ручных правок расписания нет";

    public string DayResetHint =>
        "«Отменить последнее» — шаг назад. «Вернуть как было» — сброс всех правок дня и замен в журнале. Отсутствия не затрагиваются.";

    public string SelectedLessonTitle => SelectedLesson is null
        ? "Урок не выбран"
        : SubjectScheduleRules.IsDynamicPause(SelectedLesson.SubjectName)
            ? $"Дин. пауза · {SelectedLesson.ClassName} · {SelectedLesson.TimeDisplay}"
            : $"{SelectedLesson.DisplayLessonLabel} урок · {SelectedLesson.ClassName} · {SelectedLesson.TimeDisplay}";

    public string SelectedLessonDetail => SelectedLesson is null
        ? "Кликните строку в расписании слева."
        : SubjectScheduleRules.IsDynamicPause(SelectedLesson.SubjectName)
            ? $"{SelectedLesson.SubjectName} · {SelectedLesson.TeacherName} · без кабинета"
            : SelectedLesson.RoomId > 0
                ? $"{SelectedLesson.SubjectName} · {SelectedLesson.TeacherName} · каб. {SelectedLesson.RoomNumber}"
                : $"{SelectedLesson.SubjectName} · {SelectedLesson.TeacherName} · кабинет не указан";

    public bool IsDayRoomRequired => SelectedLesson is not null
        && SubjectScheduleRules.RequiresRoom(SelectedLesson.SubjectName);

    public string CandidatesHint => SelectedLesson is null
        ? ""
        : HasCandidates
            ? HasFinishedDayCandidates
                ? "Сверху — учителя, которые ещё в школе. Ниже — уже отработали день."
                : "Кандидаты отсортированы по удобству. ⚠ — риск опоздания."
            : "Нажмите «Подобрать замену» или выберите другой урок.";

    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand ExportExcelCommand { get; }
    public IRelayCommand CopyImageCommand { get; }
    public IRelayCommand SaveImageCommand { get; }
    public IAsyncRelayCommand TeacherAbsentCommand { get; }
    public IAsyncRelayCommand MarkPeriodAbsenceCommand { get; }
    public IAsyncRelayCommand CloseAbsenceEarlyCommand { get; }
    public IAsyncRelayCommand LoadSubstitutionReportCommand { get; }
    public IRelayCommand ExportSubstitutionReportCommand { get; }
    public IAsyncRelayCommand<SubstitutionRecord?> DeleteSubstitutionRecordCommand { get; }
    public IAsyncRelayCommand ShortenedDayCommand { get; }
    public IAsyncRelayCommand ApplyShortLessonsCommand { get; }
    public IAsyncRelayCommand ApplyShortBreaksCommand { get; }
    public IAsyncRelayCommand ApplyFixedLessonsCommand { get; }
    public IAsyncRelayCommand ApplyFixedBreaksCommand { get; }
    public IAsyncRelayCommand ApplySkipDynamicPauseCommand { get; }
    public IAsyncRelayCommand SaveDayNoteCommand { get; }
    public IAsyncRelayCommand ApplyMaxLessonsCommand { get; }
    public IAsyncRelayCommand CancelSelectedLessonCommand { get; }
    public IAsyncRelayCommand CancelLastLessonsCommand { get; }
    public IAsyncRelayCommand AssignReplacementCommand { get; }
    public IAsyncRelayCommand LoadCandidatesCommand { get; }
    public IAsyncRelayCommand CancelTeacherAbsenceCommand { get; }
    public IAsyncRelayCommand RemoveReplacementCommand { get; }
    public IAsyncRelayCommand ClearAllSubstitutionsForDayCommand { get; }
    public IAsyncRelayCommand SwapLessonsCommand { get; }
    public IAsyncRelayCommand MoveLessonCommand { get; }
    public IRelayCommand OpenBellsDirectoryCommand { get; }
    public IRelayCommand<DispatcherMonitorCell?> SelectMonitorCellCommand { get; }
    public IRelayCommand<DispatcherMonitorCell?> SelectDayEditorCellCommand { get; }
    public IRelayCommand<DispatcherMonitorRow?> SelectDayEditorClassCommand { get; }
    public IRelayCommand ClearDayEditorSelectionCommand { get; }
    public IRelayCommand<TeacherAbsenceListItem?> SelectAbsentTeacherCommand { get; }
    public IAsyncRelayCommand ApplyDaySlotEditCommand { get; }
    public IAsyncRelayCommand UndoLastDayChangeCommand { get; }
    public IAsyncRelayCommand ResetDayChangesCommand { get; }
    public IRelayCommand IncreaseTrimLessonMinutesCommand { get; }
    public IRelayCommand DecreaseTrimLessonMinutesCommand { get; }
    public IRelayCommand IncreaseTrimBreakMinutesCommand { get; }
    public IRelayCommand DecreaseTrimBreakMinutesCommand { get; }
    public IRelayCommand IncreaseMaxLessonsForDayCommand { get; }
    public IRelayCommand DecreaseMaxLessonsForDayCommand { get; }
    public IRelayCommand IncreaseFixedLessonMinutesCommand { get; }
    public IRelayCommand DecreaseFixedLessonMinutesCommand { get; }
    public IRelayCommand IncreaseFixedBreakMinutesCommand { get; }
    public IRelayCommand DecreaseFixedBreakMinutesCommand { get; }

    public DispatcherViewModel(
        DayScheduleResolver schedule, DayOverrideRepository overrides,
        TeacherRepository teachers, BellRepository bells,
        BellTemplateAssignmentService bellAssignment,
        SubstitutionScorer scorer,
        SubstitutionExportService export, AppSettingsService settings,
        SchoolClassRepository classes, ISaveStateService saveState, IAppDialogService dialogs,
        IModuleNavigationService navigation, BuildingRepository buildings, RoomRepository rooms,
        BuildingTransitionChecker transitionChecker, IAppDataRevisionService revision,
        TeacherUnavailabilityRepository unavailability, TeacherAbsenceService absences,
        SubstitutionRecordRepository substitutionRecords, SubstitutionReportService substitutionReport,
        ScheduleConflictDetector conflictDetector, CalendarCountdownService calendarCountdown,
        PeriodGradeReminderService periodGradeReminder)
    {
        _schedule = schedule;
        _overrides = overrides;
        _teachers = teachers;
        _bells = bells;
        _bellAssignment = bellAssignment;
        _scorer = scorer;
        _export = export;
        _settings = settings;
        _saveState = saveState;
        _dialogs = dialogs;
        _navigation = navigation;
        _buildings = buildings;
        _rooms = rooms;
        _transitionChecker = transitionChecker;
        _revision = revision;
        _classes = classes;
        _unavailability = unavailability;
        _absences = absences;
        _substitutionRecords = substitutionRecords;
        _substitutionReport = substitutionReport;
        _conflictDetector = conflictDetector;
        _calendarCountdown = calendarCountdown;
        _periodGradeReminder = periodGradeReminder;

        var today = DateTime.Today;
        _reportFromDate = new DateTime(today.Year, today.Month, 1);
        _reportToDate = today;
        _absencePeriodStart = today;
        _absencePeriodEnd = today;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ExportExcelCommand = new RelayCommand(ExportExcel);
        CopyImageCommand = new RelayCommand(CopyImage);
        SaveImageCommand = new RelayCommand(SaveImage);
        TeacherAbsentCommand = new AsyncRelayCommand(TeacherAbsentAsync);
        MarkPeriodAbsenceCommand = new AsyncRelayCommand(MarkPeriodAbsenceAsync);
        CloseAbsenceEarlyCommand = new AsyncRelayCommand(CloseAbsenceEarlyAsync, () => CanCloseAbsenceEarly);
        LoadSubstitutionReportCommand = new AsyncRelayCommand(LoadSubstitutionReportAsync);
        ExportSubstitutionReportCommand = new RelayCommand(ExportSubstitutionReport);
        DeleteSubstitutionRecordCommand = new AsyncRelayCommand<SubstitutionRecord?>(DeleteSubstitutionRecordAsync);
        ShortenedDayCommand = new AsyncRelayCommand(ShortenedDayAsync);
        ApplyShortLessonsCommand = new AsyncRelayCommand(ApplyShortLessonsAsync);
        ApplyShortBreaksCommand = new AsyncRelayCommand(ApplyShortBreaksAsync);
        ApplyFixedLessonsCommand = new AsyncRelayCommand(ApplyFixedLessonsAsync);
        ApplyFixedBreaksCommand = new AsyncRelayCommand(ApplyFixedBreaksAsync);
        ApplySkipDynamicPauseCommand = new AsyncRelayCommand(ApplySkipDynamicPauseAsync);
        SaveDayNoteCommand = new AsyncRelayCommand(SaveDayNoteAsync);
        ApplyMaxLessonsCommand = new AsyncRelayCommand(ApplyMaxLessonsAsync);
        CancelSelectedLessonCommand = new AsyncRelayCommand(CancelSelectedLessonAsync, () => CanCancelSelectedLesson);
        CancelLastLessonsCommand = new AsyncRelayCommand(CancelLastLessonsAsync);
        AssignReplacementCommand = new AsyncRelayCommand(AssignReplacementAsync, () => CanAssignReplacement);
        LoadCandidatesCommand = new AsyncRelayCommand(LoadCandidatesAsync, () => SelectedLesson is not null && !SelectedLesson.IsCancelled);
        CancelTeacherAbsenceCommand = new AsyncRelayCommand(CancelTeacherAbsenceAsync, () => CanCancelTeacherAbsence);
        RemoveReplacementCommand = new AsyncRelayCommand(RemoveReplacementAsync, () => CanRemoveReplacement);
        ClearAllSubstitutionsForDayCommand = new AsyncRelayCommand(
            ClearAllSubstitutionsForDayAsync,
            () => CanClearAllSubstitutionsForDay);
        SwapLessonsCommand = new AsyncRelayCommand(SwapLessonsAsync, () => SelectedLesson is not null && SelectedSwapLesson is not null);
        MoveLessonCommand = new AsyncRelayCommand(MoveLessonAsync, () => SelectedLesson is not null);
        OpenBellsDirectoryCommand = new RelayCommand(() => _navigation.GoTo("Directories", BellsDirectoryTabIndex));
        SelectMonitorCellCommand = new RelayCommand<DispatcherMonitorCell?>(SelectMonitorCell);
        SelectDayEditorCellCommand = new RelayCommand<DispatcherMonitorCell?>(SelectDayEditorCell);
        SelectDayEditorClassCommand = new RelayCommand<DispatcherMonitorRow?>(SelectDayEditorClass);
        ClearDayEditorSelectionCommand = new RelayCommand(ClearDayEditorSelection);
        SelectAbsentTeacherCommand = new RelayCommand<TeacherAbsenceListItem?>(SelectAbsentTeacher);
        ApplyDaySlotEditCommand = new AsyncRelayCommand(ApplyDaySlotEditAsync, () => SelectedLesson is not null);
        UndoLastDayChangeCommand = new AsyncRelayCommand(UndoLastDayChangeAsync, () => CanUndoLastDayChange);
        ResetDayChangesCommand = new AsyncRelayCommand(ResetDayChangesAsync, () => CanResetDayChanges);
        IncreaseTrimLessonMinutesCommand = new RelayCommand(
            () => TrimLessonMinutes = Math.Min(TrimLessonMinutes + 1, MaxTrimMinutes),
            () => TrimLessonMinutes < MaxTrimMinutes);
        DecreaseTrimLessonMinutesCommand = new RelayCommand(
            () => TrimLessonMinutes = Math.Max(TrimLessonMinutes - 1, 1),
            () => TrimLessonMinutes > 1);
        IncreaseTrimBreakMinutesCommand = new RelayCommand(
            () => TrimBreakMinutes = Math.Min(TrimBreakMinutes + 1, MaxTrimMinutes),
            () => TrimBreakMinutes < MaxTrimMinutes);
        DecreaseTrimBreakMinutesCommand = new RelayCommand(
            () => TrimBreakMinutes = Math.Max(TrimBreakMinutes - 1, 1),
            () => TrimBreakMinutes > 1);
        IncreaseMaxLessonsForDayCommand = new RelayCommand(
            () => MaxLessonsForDay = Math.Min(MaxLessonsForDay + 1, MaxLessonsLimit),
            () => MaxLessonsForDay < MaxLessonsLimit);
        DecreaseMaxLessonsForDayCommand = new RelayCommand(
            () => MaxLessonsForDay = Math.Max(MaxLessonsForDay - 1, 1),
            () => MaxLessonsForDay > 1);
        IncreaseFixedLessonMinutesCommand = new RelayCommand(
            () => FixedLessonMinutes = Math.Min(FixedLessonMinutes + 1, MaxFixedLessonMinutes),
            () => FixedLessonMinutes < MaxFixedLessonMinutes);
        DecreaseFixedLessonMinutesCommand = new RelayCommand(
            () => FixedLessonMinutes = Math.Max(FixedLessonMinutes - 1, MinFixedLessonMinutes),
            () => FixedLessonMinutes > MinFixedLessonMinutes);
        IncreaseFixedBreakMinutesCommand = new RelayCommand(
            () => FixedBreakMinutes = Math.Min(FixedBreakMinutes + 1, MaxFixedBreakMinutes),
            () => FixedBreakMinutes < MaxFixedBreakMinutes);
        DecreaseFixedBreakMinutesCommand = new RelayCommand(
            () => FixedBreakMinutes = Math.Max(FixedBreakMinutes - 1, MinFixedBreakMinutes),
            () => FixedBreakMinutes > MinFixedBreakMinutes);

        var monitorClock = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        monitorClock.Tick += (_, _) =>
        {
            if (DateOnly.FromDateTime(SelectedDate) == DateOnly.FromDateTime(DateTime.Now))
                RebuildMonitor();
        };
        monitorClock.Start();
    }

    public async Task ActivateAsync()
    {
        if (!_isInitialized)
        {
            await LoadReferenceDataAsync();
            _isInitialized = true;
            _loadedReferenceRevision = _revision.ReferenceDataRevision;
        }
        else if (_loadedReferenceRevision != _revision.ReferenceDataRevision)
        {
            await LoadReferenceDataAsync();
            _loadedReferenceRevision = _revision.ReferenceDataRevision;
        }

        await RefreshAsync();
    }

    private void MarkScheduleChanged() => _revision.NotifyScheduleChanged();

    partial void OnTrimLessonMinutesChanged(int value) => NotifyStepperCommands();

    partial void OnTrimBreakMinutesChanged(int value) => NotifyStepperCommands();

    partial void OnFixedLessonMinutesChanged(int value) => NotifyStepperCommands();

    partial void OnFixedBreakMinutesChanged(int value) => NotifyStepperCommands();

    partial void OnMaxLessonsForDayChanged(int value) => NotifyStepperCommands();

    private void NotifyStepperCommands()
    {
        IncreaseTrimLessonMinutesCommand.NotifyCanExecuteChanged();
        DecreaseTrimLessonMinutesCommand.NotifyCanExecuteChanged();
        IncreaseTrimBreakMinutesCommand.NotifyCanExecuteChanged();
        DecreaseTrimBreakMinutesCommand.NotifyCanExecuteChanged();
        IncreaseMaxLessonsForDayCommand.NotifyCanExecuteChanged();
        DecreaseMaxLessonsForDayCommand.NotifyCanExecuteChanged();
        IncreaseFixedLessonMinutesCommand.NotifyCanExecuteChanged();
        DecreaseFixedLessonMinutesCommand.NotifyCanExecuteChanged();
        IncreaseFixedBreakMinutesCommand.NotifyCanExecuteChanged();
        DecreaseFixedBreakMinutesCommand.NotifyCanExecuteChanged();
    }

    partial void OnActiveSectionChanged(int value)
    {
        OnPropertyChanged(nameof(IsMonitorSection));
        OnPropertyChanged(nameof(IsReplacementsSection));
        OnPropertyChanged(nameof(IsDayEditorSection));
        OnPropertyChanged(nameof(ModuleHint));
        RefreshDayShiftHints();
        _ = RefreshDayTransitionHintsAsync();
    }

    partial void OnMonitorModeChanged(string value) => RebuildMonitor();

    partial void OnDayEditorScopeChanged(string value)
    {
        OnPropertyChanged(nameof(IsDayEditorClassScope));
        OnPropertyChanged(nameof(IsDayEditorBuildingScope));
        RebuildDayEditor();
    }

    partial void OnDayEditorClassChanged(SchoolClass? value) => RebuildDayEditor();

    partial void OnDayEditorBuildingNameChanged(string? value) => RebuildDayEditor();

    partial void OnEditDayBuildingFilterChanged(string? value)
    {
        RefreshFilteredDayRooms();
        _ = RefreshDayTransitionHintsAsync();
    }

    partial void OnEditDayTeacherChanged(Teacher? value) => _ = RefreshDayTransitionHintsAsync();
    partial void OnEditDayRoomChanged(Room? value) => _ = RefreshDayTransitionHintsAsync();

    partial void OnSelectedTeacherChanged(Teacher? value)
    {
        ResetAbsenceForm();
        SelectedLesson = null;
        Candidates.Clear();
        FinishedDayCandidates.Clear();
        SelectedCandidate = null;
        RefreshTeacherDayLessons();
        _ = OnSelectedTeacherChangedAsync();
        NotifyReplacementCommands();
    }

    private async Task OnSelectedTeacherChangedAsync()
    {
        await RefreshActiveTeacherAbsenceAsync();
        SyncReplacementOfficialDefault();
    }

    partial void OnSelectedBellTemplateChanged(string value) => _ = RefreshBellPreviewAsync();

    private async Task LoadReferenceDataAsync()
    {
        Teachers.Clear();
        Classes.Clear();
        BuildingNames.Clear();
        _buildingColors.Clear();
        BellTemplateNames.Clear();

        var teachersTask = _teachers.GetAllAsync();
        var classesTask = _classes.GetAllAsync();
        var roomsTask = _rooms.GetAllAsync();
        var buildingsTask = _buildings.GetAllAsync();
        var bellNamesTask = _bells.GetTemplateNamesAsync();
        var unavailabilityTask = _unavailability.GetAllAsync();

        await Task.WhenAll(teachersTask, classesTask, roomsTask, buildingsTask, bellNamesTask, unavailabilityTask);

        foreach (var t in teachersTask.Result)
            Teachers.Add(t);

        _classCache = classesTask.Result;
        foreach (var c in _classCache)
            Classes.Add(c);

        _bellAssignmentSnapshot = _bellAssignment.CreateSnapshot(
            _classCache,
            DateOnly.FromDateTime(SelectedDate));
        _allRooms = roomsTask.Result;
        _teacherUnavailabilities = unavailabilityTask.Result;

        foreach (var b in buildingsTask.Result)
        {
            BuildingNames.Add(b.Name);
            _buildingColors[b.Name] = BuildingColors.Normalize(b.ColorHex);
        }

        RefreshFilteredDayRooms();

        foreach (var name in bellNamesTask.Result)
            BellTemplateNames.Add(name);

        if (BellTemplateNames.Count > 0 && !BellTemplateNames.Contains(SelectedBellTemplate))
            SelectedBellTemplate = BellTemplateNames.FirstOrDefault(n =>
                n.Contains("Сокращ", StringComparison.OrdinalIgnoreCase)) ?? BellTemplateNames[0];

        await RefreshBellPreviewAsync();
    }

    partial void OnSelectedClassChanged(SchoolClass? value)
    {
        ApplyDayEditorSelectionVisuals();
        OnPropertyChanged(nameof(DayEditorSelectionHint));
    }

    partial void OnSelectedSwapLessonChanged(LessonSlot? value)
    {
        SwapLessonsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedDateChanged(DateTime value) => _ = RefreshAsync();

    partial void OnSelectedLessonChanged(LessonSlot? value)
    {
        NotifySelectionProperties();
        Candidates.Clear();
        FinishedDayCandidates.Clear();
        SelectedCandidate = null;
        RefreshSwapOptions();
        if (value is not null)
            MoveToLessonNumber = value.LessonNumber;
        if (value is not null && !value.IsCancelled && ActiveSection == DispatcherSections.Replacements)
        {
            ReplacementSideTab = 1;
            SyncReplacementOfficialDefault();
            _ = LoadCandidatesAsync();
        }
        if (value is not null && ActiveSection == DispatcherSections.DayEditor)
            LoadDayEditForm();
        if (ActiveSection == DispatcherSections.DayEditor)
            ApplyDayEditorSelectionVisuals();
        CancelSelectedLessonCommand.NotifyCanExecuteChanged();
        ApplyDaySlotEditCommand.NotifyCanExecuteChanged();
        RefreshDayShiftHints();
        _ = RefreshDayTransitionHintsAsync();
    }

    partial void OnSelectedCandidateChanged(SubstitutionCandidate? value)
    {
        OnPropertyChanged(nameof(HasSelectedCandidate));
        OnPropertyChanged(nameof(CanAssignReplacement));
        AssignReplacementCommand.NotifyCanExecuteChanged();
    }

    private void NotifySelectionProperties()
    {
        OnPropertyChanged(nameof(HasSelectedLesson));
        OnPropertyChanged(nameof(CanCancelSelectedLesson));
        OnPropertyChanged(nameof(SelectedLessonTitle));
        OnPropertyChanged(nameof(SelectedLessonDetail));
        OnPropertyChanged(nameof(IsDayRoomRequired));
        OnPropertyChanged(nameof(CandidatesHint));
        OnPropertyChanged(nameof(DayEditorSelectionHint));
        OnPropertyChanged(nameof(CanAssignReplacement));
        LoadCandidatesCommand.NotifyCanExecuteChanged();
        AssignReplacementCommand.NotifyCanExecuteChanged();
        MoveLessonCommand.NotifyCanExecuteChanged();
        SwapLessonsCommand.NotifyCanExecuteChanged();
        NotifyReplacementCommands();
    }

    private void NotifyReplacementCommands()
    {
        OnPropertyChanged(nameof(HasTeacherAbsenceOverride));
        OnPropertyChanged(nameof(CanCancelTeacherAbsence));
        OnPropertyChanged(nameof(CanCloseAbsenceEarly));
        OnPropertyChanged(nameof(ActiveAbsencePeriodHint));
        CancelTeacherAbsenceCommand.NotifyCanExecuteChanged();
        CloseAbsenceEarlyCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRemoveReplacement));
        RemoveReplacementCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanClearAllSubstitutionsForDay));
        ClearAllSubstitutionsForDayCommand.NotifyCanExecuteChanged();
    }

    private void ReselectLesson(List<LessonSlot> lessons, LessonSlot previous)
    {
        var match = lessons.FirstOrDefault(l =>
            l.SlotId == previous.SlotId && l.SlotId > 0
            && l.LessonNumber == previous.LessonNumber
            && l.ClassId == previous.ClassId
            && l.TeacherId == previous.TeacherId
            && l.SubgroupIndex == previous.SubgroupIndex);

        match ??= lessons.FirstOrDefault(l =>
            l.ClassId == previous.ClassId
            && l.LessonNumber == previous.LessonNumber
            && l.TeacherId == previous.TeacherId
            && l.SubgroupIndex == previous.SubgroupIndex);

        SelectedLesson = match;
    }

    private void RefreshSwapOptions()
    {
        SwapLessonOptions.Clear();
        SelectedSwapLesson = null;
        if (SelectedLesson is null)
            return;

        foreach (var lesson in Lessons.Where(l =>
                     !l.IsCancelled &&
                     !(l.ClassId == SelectedLesson.ClassId && l.LessonNumber == SelectedLesson.LessonNumber)))
            SwapLessonOptions.Add(lesson);
    }

    private void NotifyDaySummary()
    {
        OnPropertyChanged(nameof(DaySummaryText));
    }

    private async Task ApplyNearestCalendarAsync(DateOnly date)
    {
        var countdown = await _calendarCountdown.ResolveNearestAsync(date);
        HasNearestCalendarEvent = countdown is not null;
        if (countdown is null)
        {
            NearestCalendarLabel = "";
            NearestCalendarValue = "";
            NearestCalendarHint = "";
            return;
        }

        NearestCalendarLabel = countdown.ChipLabel;
        NearestCalendarValue = countdown.ChipValue;
        NearestCalendarHint = countdown.ChipHint;
        NearestCalendarAccentBackground = countdown.AccentBackground;
        NearestCalendarAccentForeground = countdown.AccentForeground;
    }

    private async Task ApplyPeriodGradeReminderAsync(DateOnly date)
    {
        var reminder = await _periodGradeReminder.ResolveAsync(date);
        HasPeriodGradeReminder = reminder is not null;
        if (reminder is null)
        {
            PeriodGradeReminderLabel = "";
            PeriodGradeReminderValue = "";
            PeriodGradeReminderHint = "";
            return;
        }

        PeriodGradeReminderLabel = reminder.ChipLabel;
        PeriodGradeReminderValue = reminder.ChipValue;
        PeriodGradeReminderHint = reminder.ChipHint;
        PeriodGradeReminderAccentBackground = reminder.AccentBackground;
        PeriodGradeReminderAccentForeground = reminder.AccentForeground;
    }

    private async Task RefreshAsync()
    {
        var date = DateOnly.FromDateTime(SelectedDate);
        var (isSchoolDay, lessons) = await _schedule.ResolveAsync(date);
        _dayLessons = lessons;
        IsSchoolDay = isSchoolDay;
        DayStatusText = isSchoolDay
            ? ""
            : "Выходной или каникулы — уроков нет, замены не требуются";

        Lessons.Clear();
        foreach (var l in lessons)
            Lessons.Add(l);

        ActiveLessonsCount = lessons.Count(l => !l.IsCancelled);
        PendingReplacements = lessons.Count(l => l.NeedsReplacement);
        AssignedReplacements = lessons.Count(l => l.HasAssignedReplacement);

        ReplacementQueue.Clear();
        foreach (var l in lessons.Where(l => l.NeedsReplacement))
            ReplacementQueue.Add(l);

        NotifyDaySummary();
        await ApplyNearestCalendarAsync(date);
        await ApplyPeriodGradeReminderAsync(date);

        var dateStr = SelectedDate.ToString("yyyy-MM-dd");
        var overrideRecords = await _overrides.GetRecordsForDateAsync(dateStr);
        DayOverrides.Clear();
        foreach (var record in overrideRecords)
            DayOverrides.Add(record);

        ActiveShortenedTemplateName = overrideRecords
            .LastOrDefault(r => r.OverrideType == "ShortenedDay"
                                && r.BellTemplateId is not null
                                && DayBellAdjustment.TryParse(r.Note) is null)?.Note ?? "";
        _activeDayBellAdjustment = overrideRecords
            .Where(r => r.OverrideType == "ShortenedDay")
            .Select(r => DayBellAdjustment.TryParse(r.Note))
            .LastOrDefault(a => a is not null);
        if (_activeDayBellAdjustment?.ShortLessonsMinutes is int lessonMinutes)
            TrimLessonMinutes = lessonMinutes;
        if (_activeDayBellAdjustment?.ShortBreaksMinutes is int breakMinutes)
            TrimBreakMinutes = breakMinutes;
        if (_activeDayBellAdjustment?.FixedLessonsMinutes is int fixedLessons)
            FixedLessonMinutes = fixedLessons;
        if (_activeDayBellAdjustment?.FixedBreaksMinutes is int fixedBreaks)
            FixedBreakMinutes = fixedBreaks;
        if (_activeDayBellAdjustment?.MaxLessons is int maxLessons)
            MaxLessonsForDay = maxLessons;
        NotifyStepperCommands();
        _daySubstitutionRecordCount = await _substitutionRecords.CountForDateAsync(dateStr);
        await LoadDayNoteAsync(dateStr);
        OnPropertyChanged(nameof(ShortenedDayStatus));
        OnPropertyChanged(nameof(IsShortenedDayActive));
        OnPropertyChanged(nameof(IsDynamicPauseSkipped));
        OnPropertyChanged(nameof(CanSkipDynamicPause));
        NotifyDayChangeCommands();

        if (SelectedLesson is not null)
            ReselectLesson(lessons, SelectedLesson);

        RefreshSwapOptions();
        await RefreshAbsentTeachersAsync();
        await RefreshActiveTeacherAbsenceAsync();
        SyncReplacementOfficialDefault();
        RebuildMonitor();
        RebuildDayEditor();
        RebuildStats();
        _bellPeriods = await _bells.GetAllPeriodsAsync();
        _routeMap = await _transitionChecker.LoadRouteMapAsync();
        RefreshTeacherDayLessons();
        RefreshDayShiftHints();
        NotifyReplacementCommands();
        await RefreshDayTransitionHintsAsync();
    }

    private void RebuildStats()
    {
        StatItems.Clear();
        PendingReplacementLines.Clear();

        foreach (var line in _dayLessons
                     .Where(l => l.NeedsReplacement)
                     .OrderBy(l => ParseLessonStartMinutes(l.StartTime))
                     .ThenBy(l => l.LessonNumber)
                     .Select(FormatPendingReplacementLine))
            PendingReplacementLines.Add(line);

        OnPropertyChanged(nameof(HasPendingReplacementDetails));

        StatItems.Add(new DispatcherStatItem
        {
            Label = "Без замены",
            Value = PendingReplacements.ToString(),
            Hint = PendingReplacements > 0 ? "нужно назначить" : "всё закрыто",
            MinWidth = 96,
            AccentBackground = PendingReplacements > 0 ? "#FEF2F2" : "#F0FDF4",
            AccentForeground = PendingReplacements > 0 ? "#DC2626" : "#15803D"
        });
        StatItems.Add(new DispatcherStatItem
        {
            Label = "Нет в школе",
            Value = AbsentTeachersToday.Count.ToString(),
            Hint = AbsentTeachersToday.Count > 0 ? "отмечены" : "все на месте",
            MinWidth = 96,
            AccentBackground = AbsentTeachersToday.Count > 0 ? "#FFF7ED" : "#F0FDF4",
            AccentForeground = AbsentTeachersToday.Count > 0 ? "#EA580C" : "#15803D"
        });
        StatItems.Add(new DispatcherStatItem
        {
            Label = "Замен",
            Value = AssignedReplacements.ToString(),
            Hint = "назначено",
            MinWidth = 80,
            AccentBackground = "#F0FDF4",
            AccentForeground = "#15803D"
        });
        var teachersBusy = _dayLessons.Where(l => !l.IsCancelled).Select(l => l.TeacherId).Distinct().Count();
        StatItems.Add(new DispatcherStatItem
        {
            Label = "Педагогов",
            Value = teachersBusy.ToString(),
            Hint = "ведут уроки",
            MinWidth = 96,
            AccentBackground = "#F5F3FF",
            AccentForeground = "#7C3AED"
        });
    }

    private static string FormatPendingReplacementLine(LessonSlot lesson)
    {
        var lessonLabel = lesson.DisplayLessonNumber > 0
            ? $"урок {lesson.DisplayLessonNumber}"
            : $"урок {lesson.LessonNumber}";
        var time = lesson.TimeDisplay != "—" ? $" · {lesson.TimeDisplay}" : "";
        var status = !string.IsNullOrWhiteSpace(lesson.AbsenceNote)
            ? $" · {StaffStatusTypes.ToDisplay(lesson.AbsenceNote)}"
            : "";
        return $"{lesson.TeacherName} · {lesson.ClassName} · {lessonLabel}{time}{status}";
    }

    private static int ParseLessonStartMinutes(string startTime)
    {
        if (string.IsNullOrWhiteSpace(startTime))
            return int.MaxValue;
        if (TimeSpan.TryParse(startTime, out var ts))
            return (int)ts.TotalMinutes;
        return TimeOnly.TryParse(startTime, out var clock)
            ? (int)clock.ToTimeSpan().TotalMinutes
            : int.MaxValue;
    }

    private async Task LoadDayNoteAsync(string dateStr)
    {
        var note = await _overrides.GetDayNoteAsync(dateStr);
        _loadingDayNote = true;
        DayNoteText = note?.Note ?? "";
        _loadingDayNote = false;
        OnPropertyChanged(nameof(DayNoteHint));
    }

    private async Task SaveDayNoteAsync()
    {
        if (_loadingDayNote)
            return;

        var dateStr = SelectedDate.ToString("yyyy-MM-dd");
        await _overrides.UpsertDayNoteAsync(dateStr, DayNoteText);
        _saveState.MarkDirty();

        var records = await _overrides.GetRecordsForDateAsync(dateStr);
        DayOverrides.Clear();
        foreach (var record in records)
            DayOverrides.Add(record);

        NotifyDayChangeCommands();
        StatusMessage = string.IsNullOrWhiteSpace(DayNoteText)
            ? "Заметка на день удалена"
            : "Заметка на день сохранена";
    }

    private void RebuildMonitor()
    {
        var sections = DispatcherMonitorBuilder.Build(
            MonitorMode,
            _dayLessons,
            Teachers.ToList(),
            _allRooms,
            _buildingColors,
            _bellPeriods,
            DateOnly.FromDateTime(SelectedDate),
            assignment: _bellAssignmentSnapshot,
            teacherUnavailabilities: _teacherUnavailabilities,
            dayBellAdjustment: _activeDayBellAdjustment);

        AnnotateScheduleConflicts(sections);

        MonitorSections.Clear();
        foreach (var section in sections)
            MonitorSections.Add(section);
    }

    private void RebuildDayEditor()
    {
        int? classId = DayEditorScope == "Class" ? DayEditorClass?.Id : null;
        string? building = DayEditorScope == "Building" ? DayEditorBuildingName : null;
        var mode = DayEditorScope == "Building"
            ? DispatcherMonitorModes.Buildings
            : DayEditorScope == "Class"
                ? DispatcherMonitorModes.Classes
                : DispatcherMonitorModes.Classes;

        var sections = DispatcherMonitorBuilder.Build(
            mode,
            _dayLessons,
            Teachers.ToList(),
            _allRooms,
            _buildingColors,
            _bellPeriods,
            DateOnly.FromDateTime(SelectedDate),
            filterClassId: classId,
            filterBuilding: building,
            assignment: _bellAssignmentSnapshot,
            dayBellAdjustment: _activeDayBellAdjustment);

        AnnotateScheduleConflicts(sections);

        DayEditorSections.Clear();
        foreach (var section in sections)
            DayEditorSections.Add(section);
        ApplyDayEditorSelectionVisuals();
        RefreshDayShiftHints();
    }

    private void AnnotateScheduleConflicts(IList<DispatcherMonitorSection> sections)
    {
        if (sections.Count == 0)
            return;

        var roomsById = _allRooms.ToDictionary(r => r.Id);
        DayEditorConflictAnnotator.AnnotateSections(
            sections,
            _dayLessons,
            _conflictDetector,
            _bellPeriods,
            roomsById,
            _bellAssignmentSnapshot);
    }

    private void ApplyDayEditorSelectionVisuals()
    {
        foreach (var section in DayEditorSections)
        {
            foreach (var row in section.Rows)
            {
                row.IsSelected = row.ClassId > 0 && SelectedClass?.Id == row.ClassId && SelectedLesson is null;
                foreach (var cell in row.Cells)
                {
                    cell.IsSelected = SelectedLesson is not null
                        && cell.ClassId == SelectedLesson.ClassId
                        && cell.LessonNumber == SelectedLesson.LessonNumber;
                }
            }
        }
    }

    private void RefreshDayShiftHints()
    {
        DayShiftMessages.Clear();
        HasDayShiftWarning = false;
        DayShiftSummary = "";

        if (ActiveSection != DispatcherSections.DayEditor || !IsSchoolDay)
            return;

        var classById = Classes.ToDictionary(c => c.Id);
        var violating = _dayLessons
            .Where(l => !l.IsCancelled)
            .Select(l => classById.GetValueOrDefault(l.ClassId))
            .Where(c => c is not null && ClassShiftCompliance.ViolatesSecondShiftRule(c))
            .GroupBy(c => c!.Id)
            .Select(g => g.First()!)
            .OrderBy(c => c.Grade)
            .ThenBy(c => c.Letter, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (violating.Count == 0)
            return;

        HasDayShiftWarning = true;
        DayShiftSummary = violating.Count switch
        {
            1 => "1 класс — только 1-я смена (СанПиН)",
            >= 2 and <= 4 => $"{violating.Count} класса — только 1-я смена (СанПиН)",
            _ => $"{violating.Count} классов — только 1-я смена (СанПиН)"
        };
        foreach (var cls in violating)
            DayShiftMessages.Add(ClassShiftCompliance.FormatShiftViolation(cls));
    }

    private void RefreshTeacherDayLessons()
    {
        TeacherDayLessons.Clear();
        if (SelectedTeacher is null)
            return;

        foreach (var lesson in TeacherTimelineBuilder.BuildTeacherDayDisplay(
                     SelectedTeacher.Id, _dayLessons, _bellPeriods))
            TeacherDayLessons.Add(lesson);
    }

    private void LoadDayEditForm()
    {
        if (SelectedLesson is null)
            return;

        EditDayTeacher = Teachers.FirstOrDefault(t => t.Id == SelectedLesson.TeacherId);
        if (SelectedLesson.RoomId > 0)
            EditDayRoom = _allRooms.FirstOrDefault(r => r.Id == SelectedLesson.RoomId);
        else if (SubjectScheduleRules.IsDynamicPause(SelectedLesson.SubjectName))
            EditDayRoom = null;
        else
        {
            var cls = Classes.FirstOrDefault(c => c.Id == SelectedLesson.ClassId);
            EditDayRoom = cls?.DefaultRoomId is int rid
                ? _allRooms.FirstOrDefault(r => r.Id == rid)
                : null;
        }

        EditDayBuildingFilter = EditDayRoom?.BuildingName ?? SelectedLesson.BuildingName;
        RefreshFilteredDayRooms();
    }

    private void RefreshFilteredDayRooms()
    {
        FilteredDayRooms.Clear();
        var query = _allRooms.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(EditDayBuildingFilter))
            query = query.Where(r => r.BuildingName.Equals(EditDayBuildingFilter, StringComparison.OrdinalIgnoreCase));
        foreach (var room in query.OrderBy(r => r.BuildingName).ThenBy(r => r.Number))
            FilteredDayRooms.Add(room);

        if (EditDayRoom is not null && !FilteredDayRooms.Contains(EditDayRoom))
            EditDayRoom = FilteredDayRooms.FirstOrDefault();
    }

    private async Task RefreshDayTransitionHintsAsync()
    {
        DayTransitionMessages.Clear();
        HasDayTransitionWarning = false;
        DayTransitionSummary = "";

        if (ActiveSection != DispatcherSections.DayEditor || SelectedLesson is null
            || EditDayTeacher is null)
            return;

        if (EditDayRoom is null && IsDayRoomRequired)
            return;

        var warnings = await GetTransitionWarningsForProposedAsync(BuildDayEditProposal());
        if (warnings.Count == 0)
            return;

        HasDayTransitionWarning = true;
        DayTransitionSummary = warnings.Count switch
        {
            1 => "1 переход между зданиями",
            >= 2 and <= 4 => $"{warnings.Count} перехода",
            _ => $"{warnings.Count} переходов"
        };
        foreach (var warning in warnings)
            DayTransitionMessages.Add(warning.Message);
    }

    private LessonSlot BuildDayEditProposal()
    {
        if (SelectedLesson is null || EditDayTeacher is null)
            throw new InvalidOperationException();

        return new LessonSlot
        {
            SlotId = SelectedLesson.SlotId,
            Date = SelectedLesson.Date,
            DayOfWeek = SelectedLesson.DayOfWeek,
            LessonNumber = SelectedLesson.LessonNumber,
            ClassId = SelectedLesson.ClassId,
            ClassName = SelectedLesson.ClassName,
            ClassGrade = SelectedLesson.ClassGrade,
            ClassShift = SelectedLesson.ClassShift,
            SubjectId = SelectedLesson.SubjectId,
            SubjectName = SelectedLesson.SubjectName,
            TeacherId = EditDayTeacher.Id,
            TeacherName = EditDayTeacher.FullName,
            RoomId = EditDayRoom?.Id ?? 0,
            RoomNumber = EditDayRoom?.Number ?? "",
            BuildingName = EditDayRoom?.BuildingName ?? "",
            SubgroupIndex = SelectedLesson.SubgroupIndex,
            StartTime = SelectedLesson.StartTime,
            EndTime = SelectedLesson.EndTime
        };
    }

    private Task<List<BuildingTransitionWarning>> GetTransitionWarningsForProposedAsync(LessonSlot proposed)
    {
        if (_bellPeriods.Count == 0)
            return Task.FromResult<List<BuildingTransitionWarning>>([]);

        var warnings = _transitionChecker.CheckProposedSlot(_dayLessons, proposed, _bellPeriods, _routeMap);
        return Task.FromResult(warnings.ToList());
    }

    private async Task<bool> ConfirmDayTransitionAsync(LessonSlot proposed)
    {
        var warnings = await GetTransitionWarningsForProposedAsync(proposed);
        if (warnings.Count == 0)
            return true;

        var message = string.Join("\n\n", warnings.Select(w => w.Message).Distinct());
        return _dialogs.ConfirmProceed("Переход между зданиями", message);
    }

    private async Task<bool> ConfirmDayTransitionForCandidateAsync(int teacherId, LessonSlot slot)
    {
        var teacher = Teachers.FirstOrDefault(t => t.Id == teacherId);
        if (teacher is null)
            return true;

        var proposed = new LessonSlot
        {
            Date = slot.Date,
            DayOfWeek = slot.DayOfWeek,
            LessonNumber = slot.LessonNumber,
            ClassId = slot.ClassId,
            ClassName = slot.ClassName,
            ClassGrade = slot.ClassGrade,
            ClassShift = slot.ClassShift,
            TeacherId = teacher.Id,
            TeacherName = teacher.FullName,
            RoomId = slot.RoomId,
            RoomNumber = slot.RoomNumber,
            BuildingName = slot.BuildingName,
            SubgroupIndex = slot.SubgroupIndex,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime
        };
        return await ConfirmDayTransitionAsync(proposed);
    }

    private Task<bool> ConfirmSwapTransitionsAsync()
    {
        if (SelectedLesson is null || SelectedSwapLesson is null || _bellPeriods.Count == 0)
            return Task.FromResult(true);

        var copy = _dayLessons.Select(CloneDayLesson).ToList();
        var a = copy.FirstOrDefault(l =>
            l.ClassId == SelectedLesson.ClassId && l.LessonNumber == SelectedLesson.LessonNumber && !l.IsCancelled);
        var b = copy.FirstOrDefault(l =>
            l.ClassId == SelectedSwapLesson.ClassId && l.LessonNumber == SelectedSwapLesson.LessonNumber && !l.IsCancelled);
        if (a is null || b is null)
            return Task.FromResult(true);

        DayScheduleResolver.SwapLessonContent(a, b);
        var teacherIds = new HashSet<int> { a.TeacherId, b.TeacherId };
        var warnings = _transitionChecker.CheckTeacherDay(copy, _bellPeriods, _routeMap)
            .Where(w => teacherIds.Contains(w.TeacherId))
            .ToList();
        if (warnings.Count == 0)
            return Task.FromResult(true);

        var message = string.Join("\n\n", warnings.Select(w => w.Message).Distinct());
        return Task.FromResult(_dialogs.ConfirmProceed("Переход между зданиями", message));
    }

    private async Task<bool> ConfirmMoveTransitionAsync(int targetLessonNumber)
    {
        if (SelectedLesson is null)
            return true;

        var proposed = CloneDayLesson(SelectedLesson);
        proposed.LessonNumber = targetLessonNumber;
        return await ConfirmDayTransitionAsync(proposed);
    }

    private Task<bool> ConfirmLessonReorderTransitionAsync(int targetLessonNumber)
    {
        if (SelectedLesson is null || _bellPeriods.Count == 0)
            return Task.FromResult(true);

        var copy = _dayLessons.Select(CloneDayLesson).ToList();
        var moving = copy.FirstOrDefault(l =>
            l.ClassId == SelectedLesson.ClassId && l.LessonNumber == SelectedLesson.LessonNumber && !l.IsCancelled);
        if (moving is null)
            return Task.FromResult(true);

        var target = copy.FirstOrDefault(l =>
            l.ClassId == SelectedLesson.ClassId && l.LessonNumber == targetLessonNumber && !l.IsCancelled);
        if (target is not null)
            DayScheduleResolver.SwapLessonContent(moving, target);
        else
            moving.LessonNumber = targetLessonNumber;

        var teacherIds = new HashSet<int> { moving.TeacherId };
        if (target is not null)
            teacherIds.Add(target.TeacherId);

        var warnings = _transitionChecker.CheckTeacherDay(copy, _bellPeriods, _routeMap)
            .Where(w => teacherIds.Contains(w.TeacherId))
            .ToList();
        if (warnings.Count == 0)
            return Task.FromResult(true);

        var message = string.Join("\n\n", warnings.Select(w => w.Message).Distinct());
        return Task.FromResult(_dialogs.ConfirmProceed("Переход между зданиями", message));
    }

    private static LessonSlot CloneDayLesson(LessonSlot s) => new()
    {
        SlotId = s.SlotId,
        Date = s.Date,
        DayOfWeek = s.DayOfWeek,
        LessonNumber = s.LessonNumber,
        StartTime = s.StartTime,
        EndTime = s.EndTime,
        ClassId = s.ClassId,
        ClassName = s.ClassName,
        ClassGrade = s.ClassGrade,
        ClassShift = s.ClassShift,
        SubjectId = s.SubjectId,
        SubjectName = s.SubjectName,
        TeacherId = s.TeacherId,
        TeacherName = s.TeacherName,
        RoomId = s.RoomId,
        RoomNumber = s.RoomNumber,
        BuildingName = s.BuildingName,
        SubgroupIndex = s.SubgroupIndex,
        IsCancelled = s.IsCancelled
    };

    private void SelectMonitorCell(DispatcherMonitorCell? cell)
    {
        if (cell is null || !cell.HasLesson || cell.Lesson is null)
            return;

        SelectedLesson = cell.Lesson;
        if (ActiveSection == DispatcherSections.DayEditor)
        {
            SelectedClass = Classes.FirstOrDefault(c => c.Id == cell.Lesson!.ClassId);
            LoadDayEditForm();
            return;
        }

        if (cell.Lesson.NeedsReplacement)
        {
            SelectedTeacher = Teachers.FirstOrDefault(t => t.Id == cell.Lesson.TeacherId);
            ActiveSection = DispatcherSections.Replacements;
        }
        else
        {
            ActiveSection = DispatcherSections.DayEditor;
            SelectedClass = Classes.FirstOrDefault(c => c.Id == cell.Lesson!.ClassId);
            LoadDayEditForm();
        }
    }

    private void SelectDayEditorCell(DispatcherMonitorCell? cell)
    {
        if (cell is null)
            return;

        if (cell.ColumnKind is DispatcherMonitorCell.KindBreak or DispatcherMonitorCell.KindDynamicPause)
        {
            if (cell.ClassId > 0)
                SelectDayEditorClassById(cell.ClassId);
            return;
        }

        if (cell.HasLesson && cell.Lesson is not null)
        {
            SelectedLesson = cell.Lesson;
            SelectedClass = Classes.FirstOrDefault(c => c.Id == cell.Lesson.ClassId);
            LoadDayEditForm();
        }
        else if (cell.ClassId > 0)
        {
            SelectedLesson = null;
            SelectedClass = Classes.FirstOrDefault(c => c.Id == cell.ClassId);
            if (cell.LessonNumber > 0)
                MoveToLessonNumber = cell.LessonNumber;
        }

        ApplyDayEditorSelectionVisuals();
    }

    private void SelectDayEditorClass(DispatcherMonitorRow? row)
    {
        if (row is null || row.ClassId <= 0)
            return;

        SelectedLesson = null;
        SelectedClass = Classes.FirstOrDefault(c => c.Id == row.ClassId);
        ApplyDayEditorSelectionVisuals();
    }

    private void SelectDayEditorClassById(int classId)
    {
        SelectedLesson = null;
        SelectedClass = Classes.FirstOrDefault(c => c.Id == classId);
        ApplyDayEditorSelectionVisuals();
    }

    private void ClearDayEditorSelection()
    {
        SelectedLesson = null;
        SelectedClass = null;
        ApplyDayEditorSelectionVisuals();
    }

    public async Task ApplyDayEditorDropAsync(DayEditorCellDragData source, DispatcherMonitorCell target)
    {
        if (target.ClassId <= 0 || target.LessonNumber <= 0)
            return;

        if (source.ClassId == target.ClassId && source.LessonNumber == target.LessonNumber)
            return;

        if (source.ClassId != target.ClassId && !target.HasLesson)
            return;

        var sourceLesson = _dayLessons.FirstOrDefault(l =>
            l.ClassId == source.ClassId && l.LessonNumber == source.LessonNumber && !l.IsCancelled);
        if (sourceLesson is null)
            return;

        SelectedLesson = sourceLesson;
        var dateStr = SelectedDate.ToString("yyyy-MM-dd");

        if (target.HasLesson && target.Lesson is not null)
        {
            SelectedSwapLesson = target.Lesson;
            if (!await ConfirmSwapTransitionsAsync())
                return;

            await _overrides.InsertAsync(
                dateStr, "SwapSlots",
                classId: source.ClassId,
                lessonNumber: source.LessonNumber,
                targetClassId: target.Lesson.ClassId,
                targetLessonNumber: target.Lesson.LessonNumber);
            StatusMessage =
                $"Поменяли местами: {sourceLesson.ClassName} урок {source.LessonNumber} ↔ {target.Lesson.ClassName} урок {target.Lesson.LessonNumber}";
        }
        else
        {
            if (source.ClassId != target.ClassId)
                return;

            MoveToLessonNumber = target.LessonNumber;
            if (!await ConfirmLessonReorderTransitionAsync(target.LessonNumber))
                return;

            await _overrides.InsertAsync(
                dateStr, "MoveLesson",
                classId: source.ClassId,
                lessonNumber: source.LessonNumber,
                targetLessonNumber: target.LessonNumber);
            StatusMessage =
                $"Урок {source.LessonNumber} перенесён на {target.LessonNumber} ({sourceLesson.ClassName}) — только на этот день";
        }

        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();
        SelectedLesson = _dayLessons.FirstOrDefault(l =>
            l.ClassId == target.ClassId && l.LessonNumber == target.LessonNumber && !l.IsCancelled);
        if (SelectedLesson is not null)
            SelectedClass = Classes.FirstOrDefault(c => c.Id == SelectedLesson.ClassId);
    }

    private async Task ApplyDaySlotEditAsync()
    {
        if (SelectedLesson is null)
            return;

        var proposed = BuildDayEditProposal();
        var teacherChanged = EditDayTeacher!.Id != SelectedLesson.TeacherId;
        var newRoomId = EditDayRoom?.Id ?? 0;
        var roomChanged = newRoomId != SelectedLesson.RoomId;
        if (!teacherChanged && !roomChanged)
        {
            _dialogs.ShowInfo("Конструктор дня", "Измените учителя или кабинет — сейчас значения как в расписании.");
            return;
        }

        if (IsDayRoomRequired && EditDayRoom is null)
        {
            _dialogs.ShowInfo("Конструктор дня", "Укажите кабинет для этого урока.");
            return;
        }

        if (!await ConfirmDayTransitionAsync(proposed))
            return;

        var noteParts = new List<string>();
        if (teacherChanged)
            noteParts.Add(EditDayTeacher!.FullName);
        if (roomChanged)
        {
            if (EditDayRoom is not null)
                noteParts.Add($"каб. {EditDayRoom.Number}, {EditDayRoom.BuildingName}");
            else
                noteParts.Add("без кабинета");
        }

        await _overrides.InsertAsync(
            SelectedDate.ToString("yyyy-MM-dd"), "ChangeSlot",
            classId: SelectedLesson.ClassId,
            lessonNumber: SelectedLesson.LessonNumber,
            teacherId: teacherChanged ? EditDayTeacher!.Id : null,
            roomId: roomChanged && EditDayRoom is not null ? EditDayRoom.Id : null,
            clearRoom: roomChanged && EditDayRoom is null && SelectedLesson.RoomId > 0,
            note: string.Join(" · ", noteParts));
        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();
        StatusMessage = $"Урок {SelectedLesson?.LessonNumber} ({SelectedLesson?.ClassName}) обновлён на этот день";
    }

    private async Task RefreshBellPreviewAsync()
    {
        BellPreviewLines.Clear();
        if (string.IsNullOrWhiteSpace(SelectedBellTemplate))
        {
            BellPreviewLines.Add("Выберите шаблон");
            return;
        }

        var periods = await _bells.GetAllPeriodsAsync();
        var lines = periods
            .Where(p => BellPeriodKinds.IsLesson(p.PeriodKind))
            .Where(p => p.TemplateName.Equals(SelectedBellTemplate, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.TemplateGradeFrom)
            .ThenBy(p => p.Shift)
            .ThenBy(p => p.LessonNumber)
            .Select(p => $"{p.GradeRangeDisplay}, смена {p.Shift}: урок {p.LessonNumber} — {p.StartTime}–{p.EndTime}")
            .Distinct()
            .Take(14)
            .ToList();

        if (lines.Count == 0)
            BellPreviewLines.Add("В шаблоне нет уроков — настройте в Справочниках → Звонки");
        else
            foreach (var line in lines)
                BellPreviewLines.Add(line);
    }

    private List<SubstitutionLine> GetExportLines() =>
        SubstitutionExportService.BuildLines(_dayLessons);

    private void ExportExcel()
    {
        var date = DateOnly.FromDateTime(SelectedDate);
        _export.ExportExcel(date, _settings.SchoolName, GetExportLines());
        StatusMessage = "Лист замен сохранён в Excel";
    }

    private void CopyImage()
    {
        var date = DateOnly.FromDateTime(SelectedDate);
        var pageCount = _export.CopyToClipboard(date, _settings.SchoolName, GetExportLines());
        StatusMessage = pageCount > 1
            ? $"В буфере: стр. 1/{pageCount} (картинка) + полный текст. Все PNG — кнопка «PNG»"
            : "Лист замен в буфере — картинка для чата, текст для письма";
    }

    private void SaveImage()
    {
        var date = DateOnly.FromDateTime(SelectedDate);
        var pageCount = _export.SaveImage(date, _settings.SchoolName, GetExportLines());
        StatusMessage = pageCount switch
        {
            0 => StatusMessage,
            1 => "Лист замен сохранён в PNG",
            _ => $"Сохранено {pageCount} PNG: …_1.png …_{pageCount}.png"
        };
    }

    private async Task TeacherAbsentAsync()
    {
        if (SelectedTeacher is null)
        {
            _dialogs.ShowInfo("Диспетчерская", "Выберите учителя в списке.");
            return;
        }

        var date = DateOnly.FromDateTime(SelectedDate);
        await _absences.MarkQuickAsync(
            SelectedTeacher.Id, date, AbsenceType,
            string.IsNullOrWhiteSpace(AbsenceNote) ? null : AbsenceNote.Trim(),
            AbsenceIsOfficial);
        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();

        var count = Lessons.Count(l => l.TeacherId == SelectedTeacher.Id && !l.IsCancelled);
        StatusMessage = count > 0
            ? $"{SelectedTeacher.FullName}: {count} урок(ов) — назначьте замены"
            : $"{SelectedTeacher.FullName}: отсутствие отмечено";
    }

    private async Task MarkPeriodAbsenceAsync()
    {
        if (SelectedTeacher is null)
        {
            _dialogs.ShowInfo("Диспетчерская", "Выберите учителя в списке.");
            return;
        }

        if (AbsencePeriodStart is null)
        {
            _dialogs.ShowInfo("Диспетчерская", "Укажите дату начала отсутствия.");
            return;
        }

        var start = DateOnly.FromDateTime(AbsencePeriodStart.Value);
        DateOnly? end = null;
        if (!AbsencePeriodOpenEnded)
        {
            if (AbsencePeriodEnd is null)
            {
                _dialogs.ShowInfo("Диспетчерская", "Укажите дату окончания или отметьте «без даты окончания».");
                return;
            }

            end = DateOnly.FromDateTime(AbsencePeriodEnd.Value);
            if (end < start)
            {
                _dialogs.ShowInfo("Диспетчерская", "Дата окончания не может быть раньше начала.");
                return;
            }
        }

        await _absences.MarkPeriodAsync(
            SelectedTeacher.Id, start, end, AbsenceType,
            string.IsNullOrWhiteSpace(AbsenceNote) ? null : AbsenceNote.Trim(),
            AbsenceIsOfficial, AbsenceSources.Dispatcher);
        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();

        StatusMessage = AbsencePeriodOpenEnded
            ? $"{SelectedTeacher.FullName}: открытый период отсутствия с {start:dd.MM.yyyy}"
            : $"{SelectedTeacher.FullName}: отсутствие {start:dd.MM.yyyy} — {end:dd.MM.yyyy}";
    }

    private async Task CloseAbsenceEarlyAsync()
    {
        if (SelectedTeacher is null || _activeTeacherAbsence is null)
            return;

        var lastAbsentDay = DateOnly.FromDateTime(SelectedDate);
        if (!CanCloseAbsenceEarlyFor(_activeTeacherAbsence, lastAbsentDay))
        {
            _dialogs.ShowInfo(
                "Диспетчерская",
                "Выберите последний день отсутствия — не раньше начала периода и раньше запланированного окончания.");
            return;
        }

        var start = DateOnly.Parse(_activeTeacherAbsence.StartDate);
        if (lastAbsentDay < start)
        {
            _dialogs.ShowInfo("Диспетчерская", "Дата не может быть раньше начала отсутствия.");
            return;
        }

        await _absences.ClosePeriodAsync(_activeTeacherAbsence.Id, lastAbsentDay);
        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();
        StatusMessage = $"Период закрыт досрочно: {SelectedTeacher.FullName} до {lastAbsentDay:dd.MM.yyyy}";
    }

    private static bool CanCloseAbsenceEarlyFor(TeacherStatusPeriod? period, DateOnly selectedDate)
    {
        if (period is null)
            return false;

        var start = DateOnly.Parse(period.StartDate);
        if (selectedDate < start)
            return false;

        if (period.IsOpen)
            return true;

        var end = DateOnly.Parse(period.EndDate!);
        return selectedDate < end;
    }

    private async Task ShortenedDayAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedBellTemplate))
        {
            _dialogs.ShowInfo("Звонки", "Выберите шаблон звонков.");
            return;
        }

        var templateId = await _bells.EnsureTemplateAsync(SelectedBellTemplate.Trim());
        var templateName = SelectedBellTemplate.Trim();
        await _overrides.InsertAsync(
            SelectedDate.ToString("yyyy-MM-dd"), "ShortenedDay",
            bellTemplateId: templateId, note: templateName);
        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();
        StatusMessage = $"На этот день применён шаблон «{templateName}» — лишние уроки отменены";
    }

    private async Task ApplyShortLessonsAsync()
    {
        if (TrimLessonMinutes < 1)
        {
            _dialogs.ShowInfo("Звонки", "Укажите, на сколько минут сократить уроки.");
            return;
        }

        var adjustment = (_activeDayBellAdjustment ?? new DayBellAdjustment()).WithShortLessons(TrimLessonMinutes);
        await ApplyDayBellAdjustmentAsync(adjustment);
    }

    private async Task ApplyShortBreaksAsync()
    {
        if (TrimBreakMinutes < 1)
        {
            _dialogs.ShowInfo("Звонки", "Укажите, на сколько минут сократить перемены.");
            return;
        }

        var adjustment = (_activeDayBellAdjustment ?? new DayBellAdjustment()).WithShortBreaks(TrimBreakMinutes);
        await ApplyDayBellAdjustmentAsync(adjustment);
    }

    private async Task ApplyFixedLessonsAsync()
    {
        if (FixedLessonMinutes < MinFixedLessonMinutes)
        {
            _dialogs.ShowInfo("Звонки", $"Длительность урока — не менее {MinFixedLessonMinutes} мин.");
            return;
        }

        var adjustment = (_activeDayBellAdjustment ?? new DayBellAdjustment()).WithFixedLessons(FixedLessonMinutes);
        await ApplyDayBellAdjustmentAsync(adjustment);
    }

    private async Task ApplyFixedBreaksAsync()
    {
        if (FixedBreakMinutes < MinFixedBreakMinutes)
        {
            _dialogs.ShowInfo("Звонки", $"Длительность перемены — не менее {MinFixedBreakMinutes} мин.");
            return;
        }

        var adjustment = (_activeDayBellAdjustment ?? new DayBellAdjustment()).WithFixedBreaks(FixedBreakMinutes);
        await ApplyDayBellAdjustmentAsync(adjustment);
    }

    private async Task ApplySkipDynamicPauseAsync()
    {
        var adjustment = (_activeDayBellAdjustment ?? new DayBellAdjustment()).WithSkipDynamicPause();
        await ApplyDayBellAdjustmentAsync(adjustment);
    }

    private async Task CancelSelectedLessonAsync()
    {
        if (SelectedLesson is null || SelectedLesson.IsCancelled)
            return;

        var className = SelectedLesson.ClassName;
        var lessonNumber = SelectedLesson.LessonNumber;
        var label = SubjectScheduleRules.IsDynamicPause(SelectedLesson.SubjectName)
            ? $"дин. паузу ({className})"
            : $"{lessonNumber} урок ({className})";

        if (!_dialogs.ConfirmProceed("Отмена на день", $"Отменить {label} только на эту дату?"))
            return;

        await _overrides.InsertAsync(
            SelectedDate.ToString("yyyy-MM-dd"), "CancelLesson",
            classId: SelectedLesson.ClassId,
            lessonNumber: SelectedLesson.LessonNumber);
        _saveState.MarkDirty();
        MarkScheduleChanged();
        SelectedLesson = null;
        await RefreshAsync();
        StatusMessage = $"Отменено на этот день: {label}";
    }

    private async Task ApplyMaxLessonsAsync()
    {
        if (MaxLessonsForDay < 1)
        {
            _dialogs.ShowInfo("Звонки", "Укажите максимальное число уроков.");
            return;
        }

        var adjustment = (_activeDayBellAdjustment ?? new DayBellAdjustment()).WithMaxLessons(MaxLessonsForDay);
        await ApplyDayBellAdjustmentAsync(adjustment);
    }

    private async Task ApplyDayBellAdjustmentAsync(DayBellAdjustment adjustment)
    {
        if (adjustment.IsEmpty)
            return;

        var dateStr = SelectedDate.ToString("yyyy-MM-dd");
        foreach (var record in await _overrides.GetRecordsForDateAsync(dateStr))
        {
            if (record.OverrideType == "ShortenedDay"
                && record.BellTemplateId is null
                && DayBellAdjustment.TryParse(record.Note) is not null)
                await _overrides.DeleteAsync(record.Id);
        }

        await _overrides.InsertAsync(dateStr, "ShortenedDay", note: adjustment.StorageNote);
        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();
        StatusMessage = $"На этот день: {adjustment.DisplayLine.ToLowerInvariant()}";
    }

    private async Task CancelLastLessonsAsync()
    {
        if (SelectedClass is null)
        {
            _dialogs.ShowInfo("Диспетчерская", "Выберите класс.");
            return;
        }

        if (CancelLessonsCount < 1)
        {
            _dialogs.ShowInfo("Диспетчерская", "Укажите, сколько последних уроков отменить.");
            return;
        }

        var maxLesson = Lessons.Where(l => l.ClassId == SelectedClass.Id).MaxBy(l => l.LessonNumber)?.LessonNumber ?? 0;
        if (maxLesson == 0)
        {
            _dialogs.ShowInfo("Диспетчерская", "У выбранного класса нет уроков в этот день.");
            return;
        }

        var cancelled = 0;
        for (var n = maxLesson; n > maxLesson - CancelLessonsCount && n > 0; n--)
        {
            await _overrides.InsertAsync(
                SelectedDate.ToString("yyyy-MM-dd"), "CancelLesson",
                classId: SelectedClass.Id, lessonNumber: n);
            cancelled++;
        }

        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();
        StatusMessage = $"У {SelectedClass.DisplayName} отменено уроков: {cancelled}";
    }

    private async Task UndoLastDayChangeAsync()
    {
        var dateStr = SelectedDate.ToString("yyyy-MM-dd");
        var records = await _overrides.GetRecordsForDateAsync(dateStr);
        var last = records.LastOrDefault(r => r.OverrideType != "DayNote");
        if (last is null)
            return;

        if (!_dialogs.ConfirmProceed("Отмена последнего действия", $"Отменить: {last.DisplayLine}?"))
            return;

        await _substitutionRecords.DeleteByDayOverrideIdAsync(last.Id);
        await _overrides.DeleteAsync(last.Id);
        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();
        StatusMessage = $"Отменено: {last.DisplayLine}";
    }

    private async Task ResetDayChangesAsync()
    {
        var dateStr = SelectedDate.ToString("yyyy-MM-dd");
        var overrideCount = await _overrides.CountScheduleOverridesForDateAsync(dateStr);
        var substitutionCount = await _substitutionRecords.CountForDateAsync(dateStr);
        var hasDayNote = !string.IsNullOrWhiteSpace(DayNoteText);
        if (overrideCount == 0 && substitutionCount == 0)
        {
            _dialogs.ShowInfo("Конструктор дня", "На эту дату нет правок для отмены.");
            return;
        }

        var parts = new List<string>();
        if (overrideCount > 0)
            parts.Add($"{overrideCount} правок расписания");
        if (substitutionCount > 0)
            parts.Add($"{substitutionCount} записей замен в журнале");

        var message =
            $"Вернуть расписание на {SelectedDate:dd.MM.yyyy} к недельному шаблону?\n\n" +
            $"Будут удалены: {string.Join(" и ", parts)}.\n\n" +
            "Отметки об отсутствии учителей сохранятся — их можно снять на вкладке «Замены»." +
            (hasDayNote ? "\n\nЗаметка на день останется." : "");

        if (!_dialogs.ConfirmProceed("Вернуть как было", message))
            return;

        await _substitutionRecords.DeleteForDateAsync(dateStr);
        await _overrides.DeleteScheduleOverridesForDateAsync(dateStr);
        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();
        StatusMessage = "Правки дня сброшены — расписание как в недельном шаблоне";
    }

    private void NotifyDayChangeCommands()
    {
        OnPropertyChanged(nameof(ScheduleOverrideCount));
        OnPropertyChanged(nameof(CanUndoLastDayChange));
        OnPropertyChanged(nameof(CanResetDayChanges));
        OnPropertyChanged(nameof(LastDayChangeHint));
        UndoLastDayChangeCommand.NotifyCanExecuteChanged();
        ResetDayChangesCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadCandidatesAsync()
    {
        if (SelectedLesson is null || SelectedLesson.IsCancelled)
            return;

        Candidates.Clear();
        FinishedDayCandidates.Clear();
        SelectedCandidate = null;
        var teachers = await _teachers.GetAllAsync();
        var ranked = await _scorer.RankAsync(SelectedLesson, _dayLessons, teachers);
        const int maxActiveShown = 50;
        const int maxFinishedShown = 20;

        foreach (var candidate in ranked.Where(c => !c.IsDayFinished).Take(maxActiveShown))
            Candidates.Add(candidate);
        foreach (var candidate in ranked.Where(c => c.IsDayFinished).Take(maxFinishedShown))
            FinishedDayCandidates.Add(candidate);

        SelectedCandidate = Candidates.FirstOrDefault() ?? FinishedDayCandidates.FirstOrDefault();
        OnPropertyChanged(nameof(HasCandidates));
        OnPropertyChanged(nameof(HasFinishedDayCandidates));
        OnPropertyChanged(nameof(CandidatesHint));
        OnPropertyChanged(nameof(CanAssignReplacement));
        AssignReplacementCommand.NotifyCanExecuteChanged();

        var activeTotal = ranked.Count(c => !c.IsDayFinished);
        var finishedTotal = ranked.Count(c => c.IsDayFinished);
        StatusMessage = ranked.Count == 0
            ? "Свободных кандидатов не найдено"
            : BuildCandidatesStatusMessage(activeTotal, finishedTotal, Candidates.Count, FinishedDayCandidates.Count);
    }

    private static string BuildCandidatesStatusMessage(
        int activeTotal,
        int finishedTotal,
        int activeShown,
        int finishedShown)
    {
        if (activeTotal == 0 && finishedTotal == 0)
            return "Свободных кандидатов не найдено";

        var parts = new List<string>();
        if (activeTotal > 0)
        {
            parts.Add(activeShown < activeTotal
                ? $"{activeShown} из {activeTotal} в школе"
                : $"{activeShown} в школе");
        }

        if (finishedTotal > 0)
        {
            parts.Add(finishedShown < finishedTotal
                ? $"{finishedShown} из {finishedTotal} уже отработали"
                : $"{finishedTotal} уже отработали");
        }

        return $"Подобрано: {string.Join(" · ", parts)}";
    }

    private async Task CancelTeacherAbsenceAsync()
    {
        if (SelectedTeacher is null)
            return;

        var teacherName = SelectedTeacher.FullName;
        var date = DateOnly.FromDateTime(SelectedDate);
        var cancelled = await _absences.CancelForDateAsync(SelectedTeacher.Id, date);
        if (!cancelled)
        {
            _dialogs.ShowInfo("Диспетчерская", "Для этого учителя нет отметки об отсутствии на выбранную дату.");
            return;
        }

        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();
        StatusMessage = $"Отсутствие снято: {teacherName}";
    }

    private async Task RemoveReplacementAsync()
    {
        if (SelectedLesson is null || !SelectedLesson.HasAssignedReplacement)
            return;

        var lesson = SelectedLesson;
        var statusText = $"{lesson.ClassName}, {lesson.DisplayLessonLabel}";
        var dateStr = SelectedDate.ToString("yyyy-MM-dd");
        var records = (await _overrides.GetRecordsForDateAsync(dateStr))
            .Where(r => r.OverrideType == "Substitution"
                        && r.TeacherId == lesson.TeacherId
                        && r.LessonNumber == lesson.LessonNumber
                        && (r.ClassId is null || r.ClassId == lesson.ClassId)
                        && (lesson.ReplacementTeacherId is null
                            || r.ReplacementTeacherId == lesson.ReplacementTeacherId))
            .ToList();
        if (records.Count == 0)
        {
            _dialogs.ShowInfo("Диспетчерская", "Запись о замене для этого урока не найдена.");
            return;
        }

        foreach (var record in records)
        {
            await _overrides.DeleteAsync(record.Id);
            await _substitutionRecords.DeleteByDayOverrideIdAsync(record.Id);
        }

        await _substitutionRecords.DeleteForLessonAsync(
            dateStr, lesson.TeacherId, lesson.LessonNumber, lesson.ClassId);

        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();
        ReselectLesson(_dayLessons, lesson);
        StatusMessage = $"Замена снята: {statusText}";
    }

    private async Task ClearAllSubstitutionsForDayAsync()
    {
        var dateStr = SelectedDate.ToString("yyyy-MM-dd");
        if (!CanClearAllSubstitutionsForDay)
        {
            _dialogs.ShowInfo("Диспетчерская", "На эту дату нет назначенных замен.");
            return;
        }

        var message =
            $"Снять все назначенные замены на {SelectedDate:dd.MM.yyyy}?\n\n" +
            $"Сейчас назначено: {AssignedReplacements}.\n\n" +
            "Отметки об отсутствии и уроки без замены сохранятся — можно будет подобрать заново.";

        if (!_dialogs.ConfirmProceed("Сбросить все замены", message))
            return;

        await _substitutionRecords.DeleteForDateAsync(dateStr);
        await _overrides.DeleteSubstitutionsForDateAsync(dateStr);
        _saveState.MarkDirty();
        MarkScheduleChanged();
        Candidates.Clear();
        FinishedDayCandidates.Clear();
        SelectedCandidate = null;
        await RefreshAsync();
        StatusMessage = "Все замены на день сброшены";
    }

    private async Task AssignReplacementAsync()
    {
        if (SelectedLesson is null || SelectedCandidate is null)
            return;

        if (!await ConfirmDayTransitionForCandidateAsync(SelectedCandidate.TeacherId, SelectedLesson))
            return;

        var className = SelectedLesson.ClassName;
        var lessonNumber = SelectedLesson.LessonNumber;
        var candidateName = SelectedCandidate.TeacherName;

        var overrideId = await _overrides.InsertAsync(
            SelectedDate.ToString("yyyy-MM-dd"), "Substitution",
            teacherId: SelectedLesson.TeacherId,
            classId: SelectedLesson.ClassId,
            lessonNumber: SelectedLesson.LessonNumber,
            replacementTeacherId: SelectedCandidate.TeacherId);

        await _substitutionRecords.InsertAsync(new SubstitutionRecord
        {
            Date = SelectedDate.ToString("yyyy-MM-dd"),
            LessonNumber = SelectedLesson.LessonNumber,
            ClassId = SelectedLesson.ClassId,
            ClassName = SelectedLesson.ClassName,
            ClassShift = SelectedLesson.ClassShift,
            SubjectId = SelectedLesson.SubjectId,
            SubjectName = SelectedLesson.SubjectName,
            AbsentTeacherId = SelectedLesson.TeacherId,
            AbsentTeacherName = SelectedLesson.TeacherName,
            ReplacementTeacherId = SelectedCandidate.TeacherId,
            ReplacementTeacherName = candidateName,
            StartTime = SelectedLesson.StartTime,
            EndTime = SelectedLesson.EndTime,
            IsOfficial = ReplacementIsOfficial,
            Source = AbsenceSources.Dispatcher,
            DayOverrideId = overrideId
        });
        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();
        StatusMessage = $"Замена назначена: {candidateName} (запись в журнале)";
        _dialogs.ShowSuccess(
            "Готово",
            $"{className}, {lessonNumber} урок\n→ {candidateName}\n\nЗапись добавлена во вкладку «Журнал и отчёт».");
    }

    private async Task SwapLessonsAsync()
    {
        if (SelectedLesson is null || SelectedSwapLesson is null)
            return;

        if (!await ConfirmSwapTransitionsAsync())
            return;

        var dateStr = SelectedDate.ToString("yyyy-MM-dd");
        var aClass = SelectedLesson.ClassName;
        var aNum = SelectedLesson.LessonNumber;
        var bClass = SelectedSwapLesson.ClassName;
        var bNum = SelectedSwapLesson.LessonNumber;
        await _overrides.InsertAsync(
            dateStr, "SwapSlots",
            classId: SelectedLesson.ClassId,
            lessonNumber: SelectedLesson.LessonNumber,
            targetClassId: SelectedSwapLesson.ClassId,
            targetLessonNumber: SelectedSwapLesson.LessonNumber);
        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();
        StatusMessage = $"Поменяли местами: {aClass} урок {aNum} ↔ {bClass} урок {bNum}";
    }

    private async Task MoveLessonAsync()
    {
        if (SelectedLesson is null)
            return;

        if (MoveToLessonNumber < 1)
        {
            _dialogs.ShowInfo("Перестановка", "Укажите номер урока больше 0.");
            return;
        }

        if (MoveToLessonNumber == SelectedLesson.LessonNumber)
        {
            _dialogs.ShowInfo("Перестановка", "Урок уже на этом номере.");
            return;
        }

        var dateStr = SelectedDate.ToString("yyyy-MM-dd");
        var target = Lessons.FirstOrDefault(l =>
            l.ClassId == SelectedLesson.ClassId && l.LessonNumber == MoveToLessonNumber && !l.IsCancelled);

        if (target is not null)
        {
            if (!await ConfirmLessonReorderTransitionAsync(MoveToLessonNumber))
                return;
            await _overrides.InsertAsync(
                dateStr, "SwapSlots",
                classId: SelectedLesson.ClassId,
                lessonNumber: SelectedLesson.LessonNumber,
                targetClassId: target.ClassId,
                targetLessonNumber: target.LessonNumber);
            StatusMessage =
                $"Поменяли местами уроки {SelectedLesson.LessonNumber} и {MoveToLessonNumber} ({SelectedLesson.ClassName})";
        }
        else
        {
            if (!await ConfirmLessonReorderTransitionAsync(MoveToLessonNumber))
                return;
            await _overrides.InsertAsync(
                dateStr, "MoveLesson",
                classId: SelectedLesson.ClassId,
                lessonNumber: SelectedLesson.LessonNumber,
                targetLessonNumber: MoveToLessonNumber);
            StatusMessage =
                $"Урок {SelectedLesson.LessonNumber} перенесён на {MoveToLessonNumber} ({SelectedLesson.ClassName}) — только на этот день";
        }

        _saveState.MarkDirty();
        MarkScheduleChanged();
        await RefreshAsync();
    }

    partial void OnSelectedAbsentTeacherChanged(TeacherAbsenceListItem? value)
    {
        if (value is not null)
            ApplyAbsentTeacherSelection(value);
    }

    private void SelectAbsentTeacher(TeacherAbsenceListItem? item)
    {
        if (item is null)
            return;

        if (ReferenceEquals(SelectedAbsentTeacher, item))
            ApplyAbsentTeacherSelection(item);
        else
            SelectedAbsentTeacher = item;
    }

    private void ApplyAbsentTeacherSelection(TeacherAbsenceListItem item)
    {
        var teacher = Teachers.FirstOrDefault(t => t.Id == item.TeacherId);
        if (teacher is null)
            return;

        SelectedTeacher = teacher;
        SelectedLesson = null;
        Candidates.Clear();
        FinishedDayCandidates.Clear();
        SelectedCandidate = null;
        StatusMessage = $"Выбран: {teacher.FullName}";
    }

    private async Task RefreshAbsentTeachersAsync()
    {
        var date = DateOnly.FromDateTime(SelectedDate);
        var items = await _absences.GetAbsentForDateAsync(date);
        AbsentTeachersToday.Clear();
        SelectedAbsentTeacher = null;
        foreach (var item in items)
            AbsentTeachersToday.Add(item);
        OnPropertyChanged(nameof(AbsentTeachersCaption));
    }

    private async Task RefreshActiveTeacherAbsenceAsync()
    {
        if (SelectedTeacher is null)
        {
            _activeTeacherAbsence = null;
            NotifyReplacementCommands();
            return;
        }

        var date = DateOnly.FromDateTime(SelectedDate);
        _activeTeacherAbsence = await _absences.GetActiveForTeacherOnDateAsync(SelectedTeacher.Id, date);
        NotifyReplacementCommands();
    }

    private void ResetAbsenceForm()
    {
        AbsenceType = StaffStatusTypes.Sick;
        AbsenceNote = "";
        AbsenceIsOfficial = false;
        AbsencePeriodStart = SelectedDate;
        AbsencePeriodEnd = SelectedDate;
        AbsencePeriodOpenEnded = false;
    }

    private void SyncReplacementOfficialDefault()
    {
        ReplacementIsOfficial = _activeTeacherAbsence?.IsOfficial ?? true;
    }

    private async Task LoadSubstitutionReportAsync()
    {
        if (ReportFromDate is null || ReportToDate is null)
            return;

        var from = DateOnly.FromDateTime(ReportFromDate.Value);
        var to = DateOnly.FromDateTime(ReportToDate.Value);
        if (to < from)
        {
            StatusMessage = "Конец периода раньше начала";
            return;
        }

        var bundle = await _substitutionReport.LoadAsync(from, to);
        _journalReportBundle = bundle;

        SubstitutionReportPreview.Clear();
        foreach (var line in bundle.Substitutions)
            SubstitutionReportPreview.Add(line);

        AbsenceReportPreview.Clear();
        foreach (var line in bundle.Absences)
            AbsenceReportPreview.Add(line);

        StaffActivitySummary.Clear();
        foreach (var line in bundle.Summary)
            StaffActivitySummary.Add(line);

        TopAbsenteesChart.Clear();
        foreach (var point in bundle.TopAbsenteesChart)
            TopAbsenteesChart.Add(point);

        TopSubstitutorsChart.Clear();
        foreach (var point in bundle.TopSubstitutorsChart)
            TopSubstitutorsChart.Add(point);

        OnPropertyChanged(nameof(JournalSubstitutionTotalsLine));
        OnPropertyChanged(nameof(JournalSubstitutionTotal));
        OnPropertyChanged(nameof(JournalOfficialSubstitutionTotal));
        OnPropertyChanged(nameof(JournalUnofficialSubstitutionTotal));
        OnPropertyChanged(nameof(JournalAbsencePeriodCount));
        OnPropertyChanged(nameof(JournalTeacherActivityCount));
        StatusMessage =
            $"Журнал: {bundle.SubstitutionTotalsLine}, отсутствий: {bundle.Absences.Count} периодов";
    }

    private void ExportSubstitutionReport()
    {
        if (ReportFromDate is null || ReportToDate is null)
            return;

        var from = DateOnly.FromDateTime(ReportFromDate.Value);
        var to = DateOnly.FromDateTime(ReportToDate.Value);
        if (to < from)
        {
            _dialogs.ShowInfo("Отчёт", "Конец периода раньше начала.");
            return;
        }

        var bundle = _journalReportBundle ?? _substitutionReport.LoadAsync(from, to).GetAwaiter().GetResult();
        _substitutionReport.ExportExcel(from, to, _settings.SchoolName, bundle);
        StatusMessage =
            $"Отчёт выгружен: {bundle.SubstitutionTotalsLine}, отсутствий: {bundle.Absences.Count}";
    }

    private async Task DeleteSubstitutionRecordAsync(SubstitutionRecord? record)
    {
        if (record is null || record.Id <= 0)
            return;

        if (!_dialogs.ConfirmProceed(
                "Журнал замен",
                $"Удалить запись из журнала?\n\n{record.JournalRowLine}\n\nЗамена в расписании на этот день не отменяется."))
            return;

        await _substitutionRecords.DeleteAsync(record.Id);
        _saveState.MarkDirty();
        if (ReportFromDate is not null && ReportToDate is not null)
            await LoadSubstitutionReportAsync();
        else
            StatusMessage = "Запись удалена из журнала";
    }
}
