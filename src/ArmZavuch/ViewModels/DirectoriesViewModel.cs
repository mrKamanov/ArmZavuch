using System.Collections.ObjectModel;
using System.Windows;
using ArmZavuch.Data;
using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Catalog;
using ArmZavuch.Services.Data;
using ArmZavuch.Services.Dialog;
using ArmZavuch.Services.Navigation;
using ArmZavuch.Services.Save;
using ArmZavuch.Services.Settings;
using ArmZavuch.Services.Staff;
using ArmZavuch.Services.Text;
using ArmZavuch.Services.Undo;
using ArmZavuch.Services.Rooms;
using ArmZavuch.Services.Schedule;
using ArmZavuch.Services.Validation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace ArmZavuch.ViewModels;

/// <summary>CRUD справочников и перенос данных (ТЗ §3).</summary>
public partial class DirectoriesViewModel : ObservableObject
{
    private readonly BuildingRepository _buildings;
    private readonly SubjectRepository _subjects;
    private readonly SchoolClassRepository _classes;
    private readonly TeacherRepository _teachers;
    private readonly RoomRepository _rooms;
    private readonly CurriculumRepository _curriculum;
    private readonly CurriculumTemplateRepository _curriculumTemplates;
    private readonly CurriculumTemplateApplyService _curriculumTemplateApply;
    private readonly CurriculumTemplateManageService _curriculumTemplateManage;
    private readonly BellRepository _bells;
    private readonly BellTemplateAssignmentService _bellAssignment;
    private readonly AppSettingsService _settings;
    private readonly DatabaseClearService _databaseClear;
    private readonly AppDataTransferService _appDataTransfer;
    private readonly TeacherStatusRepository _statuses;
    private readonly TeacherAbsenceService _absences;
    private readonly TeacherUnavailabilityRepository _unavailability;
    private readonly TeacherBuildingDayRepository _teacherBuildingDays;
    private readonly SubjectCatalogService _subjectCatalog;
    private readonly FopWorkloadService _fopWorkload;
    private readonly TextSuggestionService _textSuggestions;
    private readonly ISaveStateService _saveState;
    private readonly CrudUndoService _undo;
    private readonly IAppDialogService _dialogs;
    private readonly IAppDataRevisionService _revision;
    private readonly CurriculumTeacherAssignmentService _curriculumTeacherAssignment;

    private const int TeachersTabIndex = 3;
    private const int CurriculumTabIndex = 5;

    private long _loadedReferenceRevision = -1;
    private bool _directoriesInitialLoadDone;
    private int _previousActiveTabIndex = -1;
    private readonly SemaphoreSlim _curriculumGridSaveGate = new(1, 1);
    private bool _suppressCurriculumGridSave;

    public ObservableCollection<Building> BuildingList { get; } = [];
    public ObservableCollection<Subject> SubjectList { get; } = [];
    public ObservableCollection<Subject> FilteredSubjectList { get; } = [];
    public ObservableCollection<CatalogSubjectEntry> CatalogEntries { get; } = [];
    public ObservableCollection<SchoolClass> ClassList { get; } = [];
    public ObservableCollection<Teacher> TeacherList { get; } = [];
    public ObservableCollection<Teacher> FilteredTeacherList { get; } = [];
    public ObservableCollection<Room> RoomList { get; } = [];
    public ObservableCollection<CurriculumItem> CurriculumList { get; } = [];
    public ObservableCollection<CurriculumClassGroup> CurriculumGroups { get; } = [];
    public ObservableCollection<CurriculumCopyClassOption> CurriculumCopyTargetClasses { get; } = [];
    public ObservableCollection<CurriculumTemplate> CurriculumTemplateList { get; } = [];
    public ObservableCollection<CurriculumCopyClassOption> CurriculumTemplateTargetClasses { get; } = [];
    public ObservableCollection<BellPeriod> BellList { get; } = [];
    public ObservableCollection<BellPeriod> FilteredBellList { get; } = [];
    public ObservableCollection<string> BellTemplateNames { get; } = [];
    public ObservableCollection<TeacherStatusPeriod> StatusPeriods { get; } = [];
    public ObservableCollection<TeacherUnavailability> Unavailabilities { get; } = [];
    public ObservableCollection<TeacherBuildingDay> TeacherBuildingDays { get; } = [];
    public ObservableCollection<ClassPreferenceItem> ClassPreferenceOptions { get; } = [];
    public ObservableCollection<SubjectPreferenceItem> SecondarySubjectOptions { get; } = [];
    public ObservableCollection<CurriculumPreferenceItem> CurriculumAssignmentOptions { get; } = [];
    public ObservableCollection<CurriculumAssignmentSection> CurriculumAssignmentSections { get; } = [];
    public ObservableCollection<BuildingRouteMatrixRow> BuildingRouteMatrix { get; } = [];

    private List<BuildingRoute> _buildingRoutes = [];

    public bool CanEditBuildingRoutes => BuildingList.Count >= 2;

    public string BuildingRoutesHint => BuildingList.Count < 2
        ? "Добавьте минимум два здания — тогда здесь появится матрица времени переходов."
        : $"По умолчанию между зданиями — {BuildingRouteDefaults.Minutes} мин. Кнопки ± — шаг 5 мин, «40» — сброс, «×» — переход не задан.";

    public string[] TeacherTypeOptions { get; } = TeacherTypes.All;
    public string[] StatusTypeOptions { get; } = StaffStatusTypes.All;
    public string[] UnavailRecurrenceOptions { get; } = UnavailabilityRecurrence.All;

    [ObservableProperty] private Building? _selectedBuilding;
    [ObservableProperty] private Subject? _selectedSubject;
    [ObservableProperty] private SchoolClass? _selectedClass;
    [ObservableProperty] private CurriculumItem? _selectedCurriculumItem;
    [ObservableProperty] private CurriculumGridRow? _selectedCurriculumGridRow;
    [ObservableProperty] private Room? _selectedRoom;
    [ObservableProperty] private BellPeriod? _selectedBell;

    [ObservableProperty] private string _newBuildingName = "";
    [ObservableProperty] private string _newBuildingColor = "#2563EB";
    [ObservableProperty] private string _newSubjectName = "";
    [ObservableProperty] private string _newSubjectDifficulty = OfficialSubjectDifficultyReference.FormatScore(OfficialSubjectDifficultyReference.DefaultFallback);

    [ObservableProperty] private string _newSubjectDifficultyHint = "";
    private bool _subjectDifficultyManuallyEdited;
    private bool _suppressSubjectDifficultyManualFlag;
    [ObservableProperty] private string _subjectFilterText = "";
    [ObservableProperty] private string _teacherFilterText = "";
    [ObservableProperty] private int _catalogGradeIndex;
    [ObservableProperty] private string _catalogSearchText = "";
    [ObservableProperty] private CatalogSubjectEntry? _selectedCatalogEntry;
    [ObservableProperty] private string _newSubjectSuggestionHint = "";
    [ObservableProperty] private string _newBuildingSuggestionHint = "";
    [ObservableProperty] private string _newTeacherSuggestionHint = "";
    [ObservableProperty] private string _newBuildingDuplicateHint = "";
    [ObservableProperty] private string _newSubjectDuplicateHint = "";
    [ObservableProperty] private string _newClassDuplicateHint = "";
    [ObservableProperty] private string _newRoomDuplicateHint = "";
    [ObservableProperty] private string _newCurriculumDuplicateHint = "";
    [ObservableProperty] private string _newTeacherDuplicateHint = "";
    [ObservableProperty] private string _newBellDuplicateHint = "";
    [ObservableProperty] private bool _isNewBuildingDuplicate;
    [ObservableProperty] private bool _isNewSubjectDuplicate;
    [ObservableProperty] private bool _isNewClassDuplicate;
    [ObservableProperty] private bool _isNewRoomDuplicate;
    [ObservableProperty] private bool _isNewCurriculumDuplicate;
    [ObservableProperty] private bool _isNewTeacherDuplicate;
    [ObservableProperty] private bool _isNewBellDuplicate;

    public string[] CatalogGradeLabels { get; } =
    [
        "Все", "1 класс", "2 класс", "3 класс", "4 класс", "5 класс",
        "6 класс", "7 класс", "8 класс", "9 класс", "10 класс", "11 класс"
    ];
    [ObservableProperty] private string _newClassGrade = "";
    [ObservableProperty] private string _newClassLetter = "";
    [ObservableProperty] private string _newClassShift = "1";
    [ObservableProperty] private string _newClassStudentCount = "25";
    [ObservableProperty] private bool _newClassIsCorrectional;
    [ObservableProperty] private Building? _newClassBuilding;
    [ObservableProperty] private Room? _newClassDefaultRoom;
    [ObservableProperty] private Room? _newClassDefaultPeRoom;
    [ObservableProperty] private string _newClassShiftSanPinHint = "";
    [ObservableProperty] private string _newRoomNumber = "";
    [ObservableProperty] private string _newRoomKind = "";
    [ObservableProperty] private bool _newRoomAllowsParallelGroups;
    [ObservableProperty] private Building? _selectedRoomBuilding;
    [ObservableProperty] private string _newRoomCapacity = "30";
    [ObservableProperty] private string _newTeacherName = "";
    [ObservableProperty] private string _newTeacherType = TeacherTypes.Subject;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _schoolName = "";

    [ObservableProperty] private SchoolClass? _newCurriculumClass;
    [ObservableProperty] private Subject? _newCurriculumSubject;
    [ObservableProperty] private string _newCurriculumHours = "1";
    [ObservableProperty] private string _newCurriculumDifficulty = OfficialSubjectDifficultyReference.FormatScore(OfficialSubjectDifficultyReference.DefaultFallback);
    [ObservableProperty] private bool _newCurriculumHasSubgroups;
    [ObservableProperty] private string _newCurriculumWeekParity = CurriculumWeekParity.EveryWeek;
    [ObservableProperty] private SchoolClass? _curriculumCopySourceClass;
    [ObservableProperty] private CurriculumTemplate? _selectedCurriculumTemplate;

    [ObservableProperty] private string _newBellTemplate = BellTemplateNaming.Standard;
    [ObservableProperty] private string _newBellGradeFrom = "5";
    [ObservableProperty] private string _newBellGradeTo = "11";
    [ObservableProperty] private string _newBellPeriodKind = BellPeriodKinds.Lesson;
    [ObservableProperty] private string _newBellLessonNumber = "1";
    [ObservableProperty] private string _newBellShift = "1";
    [ObservableProperty] private string _newBellStartTime = "08:30";
    [ObservableProperty] private string _newBellEndTime = "09:15";
    [ObservableProperty] private int _newBellLessonDurationMinutes = 40;

    public int[] BellLessonDurationOptions { get; } = [35, 40, 45];

    public bool ShowBellDurationSelector =>
        BellPeriodKinds.IsLesson(NewBellPeriodKind)
        || NewBellPeriodKind == BellPeriodKinds.DynamicPause;
    [ObservableProperty] private string _filterBellTemplate = "";

    [ObservableProperty] private Teacher? _selectedTeacher;
    [ObservableProperty] private string _editTeacherName = "";
    [ObservableProperty] private string _editTeacherDuplicateHint = "";
    [ObservableProperty] private string _editTeacherSuggestionHint = "";
    [ObservableProperty] private bool _isEditTeacherDuplicate;
    [ObservableProperty] private string _editJobTitle = "";
    [ObservableProperty] private string _editTeacherType = TeacherTypes.Subject;
    [ObservableProperty] private string _editPhone = "";
    [ObservableProperty] private string _editContactUrl = "";
    [ObservableProperty] private string _editContactNote = "";
    [ObservableProperty] private Subject? _editPrimarySubjectItem;
    [ObservableProperty] private bool _editWorksWithFirstGrade;
    [ObservableProperty] private SchoolClass? _editHomeroomClass;

    [ObservableProperty] private string _newStatusType = StaffStatusTypes.Sick;
    [ObservableProperty] private DateTime? _newStatusStartDate = DateTime.Today;
    [ObservableProperty] private DateTime? _newStatusEndDate = DateTime.Today;
    [ObservableProperty] private bool _newStatusOpenEnded;
    [ObservableProperty] private bool _newStatusIsOfficial;
    [ObservableProperty] private string _newStatusNote = "";

    [ObservableProperty] private string _newUnavailRecurrence = UnavailabilityRecurrence.Weekly;
    [ObservableProperty] private int _newUnavailDayIndex = 3;
    [ObservableProperty] private DateTime? _newUnavailStartDate = DateTime.Today;
    [ObservableProperty] private DateTime? _newUnavailEndDate;
    [ObservableProperty] private bool _newUnavailAllDay = true;
    [ObservableProperty] private string _newUnavailLessonFrom = "";
    [ObservableProperty] private string _newUnavailLessonTo = "";
    [ObservableProperty] private string _newUnavailNote = "";

    [ObservableProperty] private int _newTeacherBuildingDayIndex;
    [ObservableProperty] private Building? _newTeacherBuildingDayBuilding;

    [ObservableProperty] private int _activeTabIndex;

    public bool CanUndo => _undo.CanUndo;

    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand SaveBuildingCommand { get; }
    public IAsyncRelayCommand SaveBuildingRoutesCommand { get; }
    public IRelayCommand<BuildingRouteMatrixRow> DecreaseRouteMinutesCommand { get; }
    public IRelayCommand<BuildingRouteMatrixRow> IncreaseRouteMinutesCommand { get; }
    public IRelayCommand<BuildingRouteMatrixRow> ResetRouteMinutesToDefaultCommand { get; }
    public IRelayCommand<BuildingRouteMatrixRow> ClearRouteMinutesCommand { get; }
    public IRelayCommand ApplyDefaultBuildingRoutesCommand { get; }
    public IAsyncRelayCommand DeleteBuildingCommand { get; }
    public IRelayCommand<Building> EditBuildingRowCommand { get; }
    public IAsyncRelayCommand<Building> DeleteBuildingRowCommand { get; }
    public IAsyncRelayCommand SaveSubjectCommand { get; }
    public IAsyncRelayCommand DeleteSubjectCommand { get; }
    public IRelayCommand<Subject> EditSubjectRowCommand { get; }
    public IAsyncRelayCommand<Subject> DeleteSubjectRowCommand { get; }
    public IAsyncRelayCommand SaveClassCommand { get; }
    public IAsyncRelayCommand DeleteClassCommand { get; }
    public IRelayCommand<SchoolClass> EditClassRowCommand { get; }
    public IAsyncRelayCommand<SchoolClass> DeleteClassRowCommand { get; }
    public IAsyncRelayCommand AddTeacherCommand { get; }
    public IAsyncRelayCommand DeleteTeacherCommand { get; }
    public IRelayCommand<Teacher> EditTeacherRowCommand { get; }
    public IAsyncRelayCommand<Teacher> DeleteTeacherRowCommand { get; }
    public IAsyncRelayCommand SaveRoomCommand { get; }
    public IAsyncRelayCommand DeleteRoomCommand { get; }
    public IRelayCommand<Room> EditRoomRowCommand { get; }
    public IAsyncRelayCommand<Room> DeleteRoomRowCommand { get; }
    public IAsyncRelayCommand ExportAppDataCommand { get; }
    public IAsyncRelayCommand ImportAppDataCommand { get; }
    public IAsyncRelayCommand SaveSettingsCommand { get; }
    public IAsyncRelayCommand ClearAllDataCommand { get; }
    public IAsyncRelayCommand<DirectoryClearSection> ClearDirectorySectionCommand { get; }
    public IReadOnlyList<DirectoryClearOption> DirectoryClearOptions { get; }
    public IAsyncRelayCommand SaveTeacherCommand { get; }
    public IRelayCommand ClearEditHomeroomClassCommand { get; }
    public IRelayCommand ClearNewClassDefaultRoomCommand { get; }
    public IRelayCommand ClearNewClassDefaultPeRoomCommand { get; }
    public IAsyncRelayCommand AddStatusPeriodCommand { get; }
    public IAsyncRelayCommand RemoveStatusPeriodCommand { get; }
    public IAsyncRelayCommand AddUnavailabilityCommand { get; }
    public IAsyncRelayCommand RemoveUnavailabilityCommand { get; }
    public IAsyncRelayCommand AddTeacherBuildingDayCommand { get; }
    public IAsyncRelayCommand<TeacherBuildingDay> RemoveTeacherBuildingDayCommand { get; }
    public IAsyncRelayCommand ImportCatalogEntryCommand { get; }
    public IAsyncRelayCommand ImportCatalogForGradeCommand { get; }
    public IAsyncRelayCommand RefreshSubjectDifficultiesCommand { get; }
    public IAsyncRelayCommand SaveCurriculumCommand { get; }
    public IAsyncRelayCommand CopyCurriculumToClassesCommand { get; }
    public IAsyncRelayCommand ApplyCurriculumTemplateCommand { get; }
    public IRelayCommand ToggleCurriculumGroupsExpandedCommand { get; }
    public IAsyncRelayCommand<CurriculumClassGroup> DeleteCurriculumClassCommand { get; }

    public string CurriculumGroupsToggleIcon => AreCurriculumGroupsExpanded ? "▴" : "▾";
    public string CurriculumGroupsToggleToolTip => AreCurriculumGroupsExpanded
        ? "Свернуть все классы"
        : "Развернуть все классы";
    public bool AreCurriculumGroupsExpanded =>
        CurriculumGroups.Count == 0 || CurriculumGroups.All(g => g.IsExpanded);

    public IAsyncRelayCommand DeleteCurriculumCommand { get; }
    public IRelayCommand<object?> EditCurriculumRowCommand { get; }
    public IAsyncRelayCommand<CurriculumGridRow?> DeleteCurriculumRowCommand { get; }
    public IAsyncRelayCommand SaveBellCommand { get; }
    public IAsyncRelayCommand DeleteBellCommand { get; }
    public IRelayCommand<BellPeriod> EditBellRowCommand { get; }
    public IAsyncRelayCommand<BellPeriod> DeleteBellRowCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IRelayCommand CancelBellEditCommand { get; }
    public IRelayCommand BeginNewCurriculumCommand { get; }
    public IRelayCommand ApplyNewSubjectSuggestionCommand { get; }
    public IReadOnlyList<BuildingColors.Choice> BuildingColorChoices => BuildingColors.Palette;

    public IReadOnlyList<string> BlockedBuildingColorHexes =>
        BuildingList
            .Where(b => SelectedBuilding is null || b.Id != SelectedBuilding.Id)
            .Select(b => BuildingColors.Normalize(b.ColorHex))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public IRelayCommand<string?> SelectBuildingColorCommand { get; }
    public IRelayCommand ApplyNewBuildingSuggestionCommand { get; }
    public IRelayCommand ApplyNewTeacherSuggestionCommand { get; }
    public IRelayCommand ApplyEditTeacherSuggestionCommand { get; }
    public IRelayCommand ClearTeacherFilterCommand { get; }
    public IAsyncRelayCommand UndoLastCommand { get; }
    public IRelayCommand ClearCheckboxSelectionCommand { get; }

    public string ClassPreferenceEmptyHint => ClassList.Count > 0
        ? ""
        : "Классы ещё не добавлены. Сначала создайте их на вкладке «Классы» — здесь появятся галочки.";

    public string CurriculumAssignmentEmptyHint => CurriculumList.Count > 0
        ? ""
        : "Нагрузка ещё не задана. Добавьте строки на вкладке «Нагрузка» — здесь появятся галочки.";

    public double TeacherCurriculumTotalHours => AllCurriculumAssignmentOptions
        .Where(o => o.IsSelected)
        .Sum(o => o.HoursPerWeek);

    public string TeacherCurriculumTotalDisplay =>
        !AllCurriculumAssignmentOptions.Any()
            ? ""
            : $"Итого по отмеченной нагрузке: {FormatCurriculumHours(TeacherCurriculumTotalHours)} ч/нед";

    private IEnumerable<CurriculumPreferenceItem> AllCurriculumAssignmentOptions =>
        CurriculumAssignmentSections.Count > 0
            ? CurriculumAssignmentSections.SelectMany(s => s.Items)
            : CurriculumAssignmentOptions;

    public string PrimarySubjectFormHint =>
        "Основной предмет педагога — выберите из справочника. Название должно совпадать с предметом в конструкторе.";

    public string SecondarySubjectFormHint =>
        "Дополнительные предметы, которые педагог может вести. Тоже только из справочника — отметьте галочками.";

    public string SubjectDirectoryHint =>
        "Предметы вашей школы — их названия используются в учебном плане, конструкторе расписания и диспетчерской. " +
        "Дважды щёлкните строку в таблице или нажмите «Изменить», чтобы отредактировать. Галочка слева — для массового удаления.";

    public string SubjectDifficultyFieldHint =>
        "Балл трудности по " + OfficialSubjectDifficultyReference.SourceLabelDativeVariants + ": у каждой параллели своё значение " +
        "(математика: 8 в 1–4 кл., 10 в 5 кл., 13 в 6 кл. и т.д.). " +
        "Выберите параллель справа в перечне ФГОС — подставится балл для неё. " +
        "В расписании и при проверке норм балл подставляется по параллели класса автоматически.";

    public string SubjectCatalogHint =>
        "Типовой перечень по ФГОС — справочная подсказка, не заменяет ваш список. Выберите параллель, найдите предмет и нажмите «Добавить выбранный» — " +
        "в левую таблицу попадёт название и балл Сивкова. «Все для класса» добавит сразу все предметы выбранной параллели (без дубликатов по названию).";

    public string SubjectCatalogBadgesHint =>
        "Строки перечня: [внеур.] — внеурочная деятельность, [фак.] — факультатив, [особ.] — особый предмет; без метки — основной учебный. " +
        "В скобках — параллели, для которых предмет предусмотрен (например, «5–9 кл.»).";

    public string StatusFormHint =>
        "Единый учёт отсутствия: анкета и диспетчерская синхронизированы. Можно указать период без даты окончания — пока не закроете.";

    public string UnavailabilitySectionHint =>
        "Можно добавить несколько записей — каждая кнопкой «+» ниже. " +
        "Надомник, работающий, например, только 3 дня в неделю: для каждого нерабочего дня добавьте «Еженедельно», «Весь день» " +
        "(Пн, Чт, Пт…). Частичная недоступность — снимите «Весь день» и укажите уроки. " +
        "В конструкторе и при проверке норм такие уроки подсветятся.";

    public string UnavailabilityFormHint => NewUnavailRecurrence switch
    {
        UnavailabilityRecurrence.Weekly =>
            "Еженедельно — повторяется каждую выбранную неделю (методический день, надомник в нерабочие дни). " +
            "Укажите день недели. «С» — с какой даты действует правило, «по» — до какой (можно не заполнять). " +
            "Несколько дней — отдельная запись на каждый.",
        UnavailabilityRecurrence.Once =>
            "Разово — один конкретный день (экзамен в вузе, командировка). Заполните только «С», «по» не нужно.",
        UnavailabilityRecurrence.DateRange =>
            "Период — недоступен каждый день между «С» и «по» (каникулы в другом месте, длительная занятость).",
        _ => ""
    };

    public string UnavailabilityLessonsHint =>
        NewUnavailAllDay
            ? "«Весь день» — педагог недоступен на всех уроках в этот день/дни."
            : "Снимите «Весь день» и укажите номера уроков, когда педагог занят (например, 1–3).";

    public string UnavailabilityListCaption =>
        Unavailabilities.Count == 0
            ? "Список пуст — добавьте одну или несколько записей."
            : Unavailabilities.Count == 1
                ? "1 запись нерабочего времени:"
                : $"{Unavailabilities.Count} записи нерабочего времени:";

    public string TeacherBuildingDayHint =>
        "Укажите, в каком здании педагог работает в каждый день недели. Можно добавить несколько дней — по одной записи. " +
        "При расхождении с уроком в конструкторе — предупреждение.";

    public string TeacherBuildingDayListCaption =>
        TeacherBuildingDays.Count == 0
            ? "Дни не заданы — ограничение по зданию не проверяется."
            : TeacherBuildingDays.Count == 1
                ? "1 день:"
                : $"{TeacherBuildingDays.Count} дня недели:";

    public string SelectionHint => ActiveTabIndex switch
    {
        0 when BuildingList.Count(b => b.IsSelected) > 0 => $"Отмечено: {BuildingList.Count(b => b.IsSelected)}",
        1 when FilteredSubjectList.Count(s => s.IsSelected) > 0 => $"Отмечено: {FilteredSubjectList.Count(s => s.IsSelected)}",
        2 when ClassList.Count(c => c.IsSelected) > 0 => $"Отмечено: {ClassList.Count(c => c.IsSelected)}",
        3 when TeacherList.Count(t => t.IsSelected) > 0 => $"Отмечено: {TeacherList.Count(t => t.IsSelected)}",
        4 when RoomList.Count(r => r.IsSelected) > 0 => $"Отмечено: {RoomList.Count(r => r.IsSelected)}",
        5 when CurriculumList.Count(c => c.IsSelected) > 0 => $"Отмечено: {CurriculumList.Count(c => c.IsSelected)}",
        6 when SelectedTemplateTimeline.Count(b => b.IsSelected) > 0 => $"Отмечено: {SelectedTemplateTimeline.Count(b => b.IsSelected)}",
        _ => ""
    };

    public DirectoriesViewModel(
        BuildingRepository buildings, SubjectRepository subjects, SchoolClassRepository classes,
        TeacherRepository teachers, TeacherStatusRepository statuses,
        TeacherAbsenceService absences,
        TeacherUnavailabilityRepository unavailability,
        TeacherBuildingDayRepository teacherBuildingDays,
        RoomRepository rooms, CurriculumRepository curriculum,
        CurriculumTemplateRepository curriculumTemplates,
        CurriculumTemplateApplyService curriculumTemplateApply,
        CurriculumTemplateManageService curriculumTemplateManage,
        BellRepository bells, BellTemplateAssignmentService bellAssignment,
        AppSettingsService settings,
        DatabaseClearService databaseClear, AppDataTransferService appDataTransfer,
        CurriculumTeacherAssignmentService curriculumTeacherAssignment,
        SubjectCatalogService subjectCatalog, FopWorkloadService fopWorkload,
        TextSuggestionService textSuggestions,
        ISaveStateService saveState, CrudUndoService undo, IAppDialogService dialogs,
        IModuleNavigationService navigation, IAppDataRevisionService revision)
    {
        _buildings = buildings;
        _subjects = subjects;
        _subjectCatalog = subjectCatalog;
        _fopWorkload = fopWorkload;
        _textSuggestions = textSuggestions;
        _classes = classes;
        _teachers = teachers;
        _curriculumTeacherAssignment = curriculumTeacherAssignment;
        _statuses = statuses;
        _absences = absences;
        _unavailability = unavailability;
        _teacherBuildingDays = teacherBuildingDays;
        _rooms = rooms;
        _curriculum = curriculum;
        _curriculumTemplates = curriculumTemplates;
        _curriculumTemplateApply = curriculumTemplateApply;
        _curriculumTemplateManage = curriculumTemplateManage;
        _bells = bells;
        _bellAssignment = bellAssignment;
        _settings = settings;
        _databaseClear = databaseClear;
        _appDataTransfer = appDataTransfer;
        _saveState = saveState;
        _undo = undo;
        _dialogs = dialogs;
        _revision = revision;
        _navigation = navigation;

        LoadCommand = new AsyncRelayCommand(ReloadAfterMutationAsync);
        ApplyNewTeacherSuggestionCommand = new RelayCommand(ApplyNewTeacherSuggestion);
        ApplyEditTeacherSuggestionCommand = new RelayCommand(ApplyEditTeacherSuggestion);
        ClearTeacherFilterCommand = new RelayCommand(ClearTeacherFilter);
        UndoLastCommand = new AsyncRelayCommand(UndoLastAsync, () => CanUndo);
        ClearCheckboxSelectionCommand = new RelayCommand(ClearCheckboxSelection);

        SaveBuildingCommand = new AsyncRelayCommand(SaveBuildingAsync);
        SaveBuildingRoutesCommand = new AsyncRelayCommand(SaveBuildingRoutesAsync);
        DecreaseRouteMinutesCommand = new RelayCommand<BuildingRouteMatrixRow>(r => AdjustRouteMinutes(r, -RouteMinutesStep));
        IncreaseRouteMinutesCommand = new RelayCommand<BuildingRouteMatrixRow>(r => AdjustRouteMinutes(r, RouteMinutesStep));
        ResetRouteMinutesToDefaultCommand = new RelayCommand<BuildingRouteMatrixRow>(ResetRouteMinutesToDefault);
        ClearRouteMinutesCommand = new RelayCommand<BuildingRouteMatrixRow>(ClearRouteMinutes);
        ApplyDefaultBuildingRoutesCommand = new RelayCommand(ApplyDefaultBuildingRoutes);
        DeleteBuildingCommand = new AsyncRelayCommand(DeleteBuildingAsync);
        EditBuildingRowCommand = new RelayCommand<Building>(b => { if (b is not null) SelectedBuilding = b; });
        DeleteBuildingRowCommand = new AsyncRelayCommand<Building>(DeleteBuildingRowAsync);
        SaveSubjectCommand = new AsyncRelayCommand(SaveSubjectAsync);
        DeleteSubjectCommand = new AsyncRelayCommand(DeleteSubjectAsync);
        EditSubjectRowCommand = new RelayCommand<Subject>(s => { if (s is not null) SelectedSubject = s; });
        DeleteSubjectRowCommand = new AsyncRelayCommand<Subject>(DeleteSubjectRowAsync);
        SaveClassCommand = new AsyncRelayCommand(SaveClassAsync);
        DeleteClassCommand = new AsyncRelayCommand(DeleteClassAsync);
        EditClassRowCommand = new RelayCommand<SchoolClass>(c => { if (c is not null) SelectedClass = c; });
        DeleteClassRowCommand = new AsyncRelayCommand<SchoolClass>(DeleteClassRowAsync);
        AddTeacherCommand = new AsyncRelayCommand(AddTeacherAsync);
        DeleteTeacherCommand = new AsyncRelayCommand(DeleteTeacherAsync);
        EditTeacherRowCommand = new RelayCommand<Teacher>(t => { if (t is not null) SelectedTeacher = t; });
        DeleteTeacherRowCommand = new AsyncRelayCommand<Teacher>(DeleteTeacherRowAsync);
        SaveRoomCommand = new AsyncRelayCommand(SaveRoomAsync);
        DeleteRoomCommand = new AsyncRelayCommand(DeleteRoomAsync);
        EditRoomRowCommand = new RelayCommand<Room>(r => { if (r is not null) SelectedRoom = r; });
        DeleteRoomRowCommand = new AsyncRelayCommand<Room>(DeleteRoomRowAsync);
        ExportAppDataCommand = new AsyncRelayCommand(ExportAppDataAsync);
        ImportAppDataCommand = new AsyncRelayCommand(ImportAppDataAsync);
        InitDataTransferCommands();
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ClearAllDataCommand = new AsyncRelayCommand(ClearAllDataAsync);
        ClearDirectorySectionCommand = new AsyncRelayCommand<DirectoryClearSection>(ClearDirectorySectionAsync);
        DirectoryClearOptions =
        [
            new DirectoryClearOption
            {
                Section = DirectoryClearSection.Buildings,
                TabName = "Здания",
                Hint = "Здания, маршруты и связанные кабинеты"
            },
            new DirectoryClearOption
            {
                Section = DirectoryClearSection.Subjects,
                TabName = "Предметы",
                Hint = "Все предметы и связанная нагрузка/расписание"
            },
            new DirectoryClearOption
            {
                Section = DirectoryClearSection.Classes,
                TabName = "Классы",
                Hint = "Классы, нагрузка по классам и уроки в сетке"
            },
            new DirectoryClearOption
            {
                Section = DirectoryClearSection.Teachers,
                TabName = "Сотрудники",
                Hint = "Учителя, статусы, недоступность и уроки"
            },
            new DirectoryClearOption
            {
                Section = DirectoryClearSection.Rooms,
                TabName = "Кабинеты",
                Hint = "Кабинеты и уроки, где они указаны"
            },
            new DirectoryClearOption
            {
                Section = DirectoryClearSection.Curriculum,
                TabName = "Нагрузка",
                Hint = "Вся нагрузка или только назначения педагогов"
            },
            new DirectoryClearOption
            {
                Section = DirectoryClearSection.Bells,
                TabName = "Звонки",
                Hint = "Шаблоны и периоды звонков"
            }
        ];
        SaveTeacherCommand = new AsyncRelayCommand(SaveTeacherAsync);
        ClearEditHomeroomClassCommand = new RelayCommand(() => EditHomeroomClass = null);
        ClearNewClassDefaultRoomCommand = new RelayCommand(() => NewClassDefaultRoom = null);
        ClearNewClassDefaultPeRoomCommand = new RelayCommand(() => NewClassDefaultPeRoom = null);
        AddStatusPeriodCommand = new AsyncRelayCommand(AddStatusPeriodAsync);
        RemoveStatusPeriodCommand = new AsyncRelayCommand<TeacherStatusPeriod>(RemoveStatusPeriodAsync);
        AddUnavailabilityCommand = new AsyncRelayCommand(AddUnavailabilityAsync);
        RemoveUnavailabilityCommand = new AsyncRelayCommand<TeacherUnavailability>(RemoveUnavailabilityAsync);
        AddTeacherBuildingDayCommand = new AsyncRelayCommand(AddTeacherBuildingDayAsync);
        RemoveTeacherBuildingDayCommand = new AsyncRelayCommand<TeacherBuildingDay>(RemoveTeacherBuildingDayAsync);
        ImportCatalogEntryCommand = new AsyncRelayCommand(ImportCatalogEntryAsync);
        ImportCatalogForGradeCommand = new AsyncRelayCommand(ImportCatalogForGradeAsync);
        RefreshSubjectDifficultiesCommand = new AsyncRelayCommand(RefreshSubjectDifficultiesAsync);
        SaveCurriculumCommand = new AsyncRelayCommand(SaveCurriculumAsync);
        CopyCurriculumToClassesCommand = new AsyncRelayCommand(CopyCurriculumToClassesAsync);
        ApplyCurriculumTemplateCommand = new AsyncRelayCommand(ApplyCurriculumTemplateAsync);
        InitializeCurriculumTemplateCommands();
        InitFopImportCommands();
        ToggleCurriculumGroupsExpandedCommand = new RelayCommand(ToggleCurriculumGroupsExpanded);
        DeleteCurriculumClassCommand = new AsyncRelayCommand<CurriculumClassGroup>(DeleteCurriculumClassAsync);
        DeleteCurriculumCommand = new AsyncRelayCommand(DeleteCurriculumAsync);
        EditCurriculumRowCommand = new RelayCommand<object?>(SelectCurriculumRowForEdit);
        DeleteCurriculumRowCommand = new AsyncRelayCommand<CurriculumGridRow?>(DeleteCurriculumGridRowAsync);
        SaveBellCommand = new AsyncRelayCommand(SaveBellAsync);
        SaveBellDefaultsCommand = new AsyncRelayCommand(SaveBellDefaultsAsync);
        DeleteBellCommand = new AsyncRelayCommand(DeleteBellAsync);
        EditBellRowCommand = new RelayCommand<BellPeriod>(b => { if (b is not null) SelectedBell = b; });
        DeleteBellRowCommand = new AsyncRelayCommand<BellPeriod>(DeleteBellRowAsync);
        CancelEditCommand = new RelayCommand(CancelEdit);
        CancelBellEditCommand = new RelayCommand(CancelBellEdit);
        BeginNewCurriculumCommand = new RelayCommand(BeginNewCurriculum);
        ApplyNewSubjectSuggestionCommand = new RelayCommand(ApplyNewSubjectSuggestion);
        ApplyNewBuildingSuggestionCommand = new RelayCommand(ApplyNewBuildingSuggestion);
        SelectBuildingColorCommand = new RelayCommand<string?>(hex =>
        {
            if (string.IsNullOrWhiteSpace(hex))
                return;
            var normalized = BuildingColors.Normalize(hex);
            if (BuildingColors.IsTaken(normalized, BuildingList, SelectedBuilding?.Id))
            {
                StatusMessage = $"Цвет «{BuildingColors.LabelFor(normalized)}» уже используется другим зданием";
                return;
            }
            NewBuildingColor = normalized;
        });

        _undo.Changed += () =>
        {
            OnPropertyChanged(nameof(CanUndo));
            UndoLastCommand.NotifyCanExecuteChanged();
        };
    }

    partial void OnSelectedTeacherChanged(Teacher? value) => _ = LoadTeacherDetailsAsync();
    partial void OnSelectedBuildingChanged(Building? value)
    {
        LoadBuildingForm(value);
        UpdateBuildingDuplicate();
        RefreshBlockedBuildingColors();
    }

    partial void OnSelectedSubjectChanged(Subject? value)
    {
        LoadSubjectForm(value);
        UpdateSubjectDuplicate();
    }

    partial void OnSelectedClassChanged(SchoolClass? value)
    {
        LoadClassForm(value);
        UpdateClassDuplicate();
    }

    private bool _suppressCurriculumGridSync;

    partial void OnSelectedCurriculumGridRowChanged(CurriculumGridRow? value)
    {
        if (_suppressCurriculumGridSync)
            return;

        if (value?.Item != SelectedCurriculumItem)
        {
            _suppressCurriculumGridSync = true;
            try
            {
                SelectedCurriculumItem = value?.Item;
            }
            finally
            {
                _suppressCurriculumGridSync = false;
            }
        }
    }

    partial void OnSelectedCurriculumItemChanged(CurriculumItem? value)
    {
        LoadCurriculumForm(value);
        UpdateCurriculumDuplicate();
        if (_suppressCurriculumGridSync)
            return;

        SyncCurriculumGridSelection(value);
    }

    partial void OnSelectedRoomChanged(Room? value)
    {
        LoadRoomForm(value);
        UpdateRoomDuplicate();
    }

    partial void OnSelectedBellChanged(BellPeriod? value)
    {
        if (_clearingBellSelection && value is not null)
            return;

        LoadBellForm(value);
        if (value is null)
            ApplySuggestedBellSlot();
        else
            SyncBellLessonDurationFromForm();
        UpdateBellDuplicate();
    }

    public string SaveBuildingLabel => SelectedBuilding is null ? "Добавить" : "Сохранить";
    public string SaveSubjectLabel => SelectedSubject is null ? "Добавить" : "Сохранить";
    public string SaveClassLabel => SelectedClass is null ? "Добавить" : "Сохранить";
    public string SaveCurriculumLabel => SelectedCurriculumItem is null ? "Добавить" : "Сохранить";
    public string SaveRoomLabel => SelectedRoom is null ? "Добавить" : "Сохранить";

    public IReadOnlyList<string> RoomKindOptions => RoomKinds.Options;

    public bool IsNewRoomSportsHall =>
        RoomPhysicalIdentity.IsSportsHall(NewRoomNumber, RoomKinds.FromDisplay(NewRoomKind));

    public string RoomFormHint =>
        "Обычный кабинет — для большинства уроков. «Спортзал» — для физкультуры и параллельных групп. " +
        "Номер «с/з» тоже считается спортзалом, даже если тип «Обычный».";

    public string SaveBellLabel => SelectedBell is null ? "Добавить" : "Сохранить";
    partial void OnSubjectFilterTextChanged(string value) => RefreshSubjectFilter();
    partial void OnTeacherFilterTextChanged(string value) => RefreshTeacherFilter();

    partial void OnEditPrimarySubjectItemChanged(Subject? value)
    {
        RefreshSecondarySubjectOptions();
        RefreshCurriculumAssignmentOptions();
    }
    partial void OnCatalogSearchTextChanged(string value) => RefreshCatalog();
    partial void OnCatalogGradeIndexChanged(int value)
    {
        RefreshCatalog();
        if (!_subjectDifficultyManuallyEdited && SelectedSubject is null)
            ApplySuggestedSubjectDifficulty();
    }
    partial void OnNewSubjectNameChanged(string value)
    {
        UpdateSubjectDuplicate();
        NewSubjectSuggestionHint = IsNewSubjectDuplicate
            ? ""
            : (_subjectCatalog.SuggestSubjectName(NewSubjectName, SubjectList)?.Hint ?? "");
        if (!_subjectDifficultyManuallyEdited)
            ApplySuggestedSubjectDifficulty();
    }

    partial void OnNewSubjectDifficultyChanged(string value)
    {
        if (!_suppressSubjectDifficultyManualFlag)
            _subjectDifficultyManuallyEdited = true;
        UpdateSubjectDuplicate();
    }

    partial void OnNewBuildingNameChanged(string value)
    {
        UpdateBuildingDuplicate();
        if (IsNewBuildingDuplicate)
        {
            NewBuildingSuggestionHint = "";
            return;
        }

        var formatted = ProperNameFormatter.FormatBuildingOrAddress(NewBuildingName);
        NewBuildingSuggestionHint = !string.IsNullOrWhiteSpace(NewBuildingName)
                                    && !NewBuildingName.Trim().Equals(formatted, StringComparison.Ordinal)
            ? $"Оформить: «{formatted}»?"
            : "";
    }

    partial void OnNewTeacherNameChanged(string value)
    {
        UpdateTeacherDuplicate();
        NewTeacherSuggestionHint = IsNewTeacherDuplicate
            ? ""
            : (_textSuggestions.SuggestPersonName(NewTeacherName, TeacherList.Select(t => t.FullName))?.Hint ?? "");
    }

    partial void OnEditTeacherNameChanged(string value)
    {
        UpdateEditTeacherDuplicate();
        if (SelectedTeacher is null || IsEditTeacherDuplicate)
        {
            EditTeacherSuggestionHint = "";
            return;
        }

        var others = TeacherList.Where(t => t.Id != SelectedTeacher.Id).Select(t => t.FullName);
        EditTeacherSuggestionHint = _textSuggestions.SuggestPersonName(value, others)?.Hint ?? "";
    }

    partial void OnNewClassGradeChanged(string value)
    {
        UpdateClassDuplicate();
        UpdateClassShiftSanPinHint();
    }

    partial void OnNewClassLetterChanged(string value) => UpdateClassDuplicate();

    partial void OnNewClassShiftChanged(string value) => UpdateClassShiftSanPinHint();

    partial void OnNewClassIsCorrectionalChanged(bool value) => UpdateClassShiftSanPinHint();

    partial void OnNewClassDefaultRoomChanged(Room? value)
    {
        if (value is null || NewClassBuilding is not null)
            return;
        NewClassBuilding = BuildingList.FirstOrDefault(b => b.Id == value.BuildingId);
    }
    partial void OnNewRoomNumberChanged(string value)
    {
        UpdateRoomDuplicate();
        OnPropertyChanged(nameof(IsNewRoomSportsHall));
    }
    partial void OnSelectedRoomBuildingChanged(Building? value) => UpdateRoomDuplicate();
    partial void OnNewRoomKindChanged(string value) => OnPropertyChanged(nameof(IsNewRoomSportsHall));
    partial void OnNewCurriculumClassChanged(SchoolClass? value) => UpdateCurriculumDuplicate();
    partial void OnNewCurriculumSubjectChanged(Subject? value)
    {
        UpdateCurriculumDuplicate();
        if (value is not null && SelectedCurriculumItem is null)
            NewCurriculumDifficulty = OfficialSubjectDifficultyReference.FormatScore(value.DifficultyScore);
    }
    partial void OnNewCurriculumWeekParityChanged(string value) => UpdateCurriculumDuplicate();
    partial void OnCurriculumCopySourceClassChanged(SchoolClass? value) => RebuildCurriculumCopyTargets(value);

    private async Task ReloadAfterMutationAsync()
    {
        await WaitPendingCurriculumGridSavesAsync();
        _revision.NotifyReferenceDataChanged();
        await LoadAsync();
        _loadedReferenceRevision = _revision.ReferenceDataRevision;
    }

    public async Task ActivateAsync()
    {
        if (!_directoriesInitialLoadDone)
        {
            await LoadAsync();
            _directoriesInitialLoadDone = true;
            _loadedReferenceRevision = _revision.ReferenceDataRevision;
            ApplyPendingNavigationContext();
            return;
        }

        if (_loadedReferenceRevision == _revision.ReferenceDataRevision)
        {
            ApplyPendingNavigationContext();
            return;
        }

        await WaitPendingCurriculumGridSavesAsync();
        await LoadAsync();
        _loadedReferenceRevision = _revision.ReferenceDataRevision;
        ApplyPendingNavigationContext();
    }

    public Task PrepareForDeactivateAsync() => WaitPendingCurriculumGridSavesAsync();

    private async Task LoadAsync()
    {
        await WaitPendingCurriculumGridSavesAsync();
        _suppressCurriculumGridSave = true;
        try
        {
            await LoadReferenceDataCoreAsync();
        }
        finally
        {
            _suppressCurriculumGridSave = false;
        }
    }

    private async Task LoadReferenceDataCoreAsync()
    {
        var selectedTeacherId = SelectedTeacher?.Id;
        var selectedClassId = SelectedClass?.Id;
        var selectedRoomId = SelectedRoom?.Id;
        var selectedBuildingId = SelectedBuilding?.Id;
        var selectedSubjectId = SelectedSubject?.Id;
        var selectedCurriculumId = SelectedCurriculumItem?.Id ?? SelectedCurriculumGridRow?.Item.Id;
        SelectedCurriculumGridRow = null;
        TeardownCurriculumGridRows();
        BuildingList.Clear();
        foreach (var b in await _buildings.GetAllAsync())
        {
            WireSelection(b);
            BuildingList.Add(b);
        }
        _buildingRoutes = await _buildings.GetRoutesAsync();
        LoadBuildingRouteMatrix();
        OnPropertyChanged(nameof(CanEditBuildingRoutes));
        OnPropertyChanged(nameof(BuildingRoutesHint));
        SubjectList.Clear();
        foreach (var s in await _subjects.GetAllAsync())
        {
            WireSelection(s);
            SubjectList.Add(s);
        }
        RefreshSubjectFilter();
        RefreshCatalog();
        ClassList.Clear();
        foreach (var c in await _classes.GetAllAsync())
        {
            WireSelection(c);
            ClassList.Add(c);
        }
        FopImportClass ??= ClassList
            .OrderBy(c => c.Grade)
            .ThenBy(c => c.Letter, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        TeacherList.Clear();
        foreach (var t in await _teachers.GetAllAsync())
        {
            WireSelection(t);
            TeacherList.Add(t);
        }
        RefreshTeacherFilter();
        RefreshTeacherPickList();
        RoomList.Clear();
        foreach (var r in await _rooms.GetAllAsync())
        {
            WireSelection(r);
            RoomList.Add(r);
        }
        CurriculumList.Clear();
        foreach (var c in await _curriculum.GetAllAsync())
        {
            WireSelection(c);
            CurriculumList.Add(c);
        }
        _curriculumAssigneeMap = await _teachers.GetExplicitAssigneesByCurriculumAsync();
        RefreshCurriculumGroups();
        ResyncCurriculumSelectionAfterReferenceLoad(selectedCurriculumId);
        BellList.Clear();
        foreach (var b in await _bells.GetAllPeriodsAsync())
        {
            WireSelection(b);
            BellList.Add(b);
        }
        await LoadBellDefaultsAsync();
        _bellTemplateRows = await _bells.GetAllTemplatesAsync();
        await RefreshBellTemplateNamesAsync();
        RefreshBellTemplateCards();
        SchoolName = _settings.SchoolName;
        SelectedRoomBuilding ??= BuildingList.FirstOrDefault();
        UpdateBuildingDuplicate();
        UpdateSubjectDuplicate();
        UpdateClassDuplicate();
        UpdateRoomDuplicate();
        UpdateCurriculumDuplicate();
        UpdateTeacherDuplicate();
        UpdateBellDuplicate();
        RebuildCurriculumCopyTargets(CurriculumCopySourceClass);
        await LoadCurriculumTemplatesAsync();
        if (SelectedCurriculumTemplate is not null && CurriculumTemplateTargetClasses.Count == 0)
            RebuildCurriculumTemplateTargets(SelectedCurriculumTemplate);
        OnPropertyChanged(nameof(ClassPreferenceEmptyHint));
        OnPropertyChanged(nameof(CurriculumAssignmentEmptyHint));
        if (SelectedTeacher is not null)
            RefreshCurriculumAssignmentOptions();
        RefreshBlockedBuildingColors();
        if (selectedBuildingId is int buildingId)
            SelectedBuilding = BuildingList.FirstOrDefault(b => b.Id == buildingId);
        if (selectedSubjectId is int subjectId)
            SelectedSubject = SubjectList.FirstOrDefault(s => s.Id == subjectId);
        if (selectedClassId is int classId)
            SelectedClass = ClassList.FirstOrDefault(c => c.Id == classId);
        if (selectedRoomId is int roomId)
            SelectedRoom = RoomList.FirstOrDefault(r => r.Id == roomId);
        if (selectedTeacherId is int teacherId)
            SelectedTeacher = TeacherList.FirstOrDefault(t => t.Id == teacherId);
    }

    partial void OnFilterBellTemplateChanged(string value) => RefreshBellTimeline();

    partial void OnNewBellTemplateChanged(string value) => UpdateBellDuplicate();
    partial void OnNewUnavailRecurrenceChanged(string value)
    {
        OnPropertyChanged(nameof(UnavailabilityFormHint));
    }

    partial void OnNewUnavailAllDayChanged(bool value) =>
        OnPropertyChanged(nameof(UnavailabilityLessonsHint));

    partial void OnNewBellLessonNumberChanged(string value) => UpdateBellDuplicate();
    partial void OnNewBellShiftChanged(string value) => UpdateBellDuplicate();
    partial void OnNewBellPeriodKindChanged(string value)
    {
        OnPropertyChanged(nameof(ShowBellDurationSelector));
        RecalculateBellEndFromDuration();
        UpdateBellDuplicate();
    }

    partial void OnNewBellStartTimeChanged(string value)
    {
        if (BellTime.TryAutoFormat(value, out var formatted) && formatted != value)
        {
            NewBellStartTime = formatted;
            return;
        }

        RecalculateBellEndFromDuration();
        UpdateBellDuplicate();
    }

    partial void OnNewBellEndTimeChanged(string value)
    {
        if (BellTime.TryAutoFormat(value, out var formatted) && formatted != value)
            NewBellEndTime = formatted;
        UpdateBellDuplicate();
    }

    partial void OnNewBellLessonDurationMinutesChanged(int value) => RecalculateBellEndFromDuration();

    partial void OnActiveTabIndexChanged(int value) => _ = HandleActiveTabIndexChangedAsync(value);

    private async Task HandleActiveTabIndexChangedAsync(int value)
    {
        var previous = _previousActiveTabIndex;
        if (previous == CurriculumTabIndex && value != CurriculumTabIndex)
            await WaitPendingCurriculumGridSavesAsync();

        if (value == TeachersTabIndex && previous != TeachersTabIndex)
            await RefreshTeachersForStaffTabAsync();

        _previousActiveTabIndex = value;
        OnPropertyChanged(nameof(SelectionHint));
    }

    private void WireSelection(SelectableEntity item) =>
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectableEntity.IsSelected))
                OnPropertyChanged(nameof(SelectionHint));
        };

    private void ClearCheckboxSelection()
    {
        UncheckAll(BuildingList);
        UncheckAll(SubjectList);
        UncheckAll(ClassList);
        UncheckAll(TeacherList);
        UncheckAll(RoomList);
        UncheckAll(CurriculumList);
        UncheckAll(BellList);
        CancelEdit();
        OnPropertyChanged(nameof(SelectionHint));
    }

    private static void UncheckAll<T>(IEnumerable<T> items) where T : SelectableEntity
    {
        foreach (var item in items)
            item.IsSelected = false;
    }

    private static List<T> GetDeleteTargets<T>(IEnumerable<T> all, T? single) where T : SelectableEntity
    {
        var checkedItems = all.Where(x => x.IsSelected).ToList();
        if (checkedItems.Count > 0)
            return checkedItems;
        return single is not null ? [single] : [];
    }

    private bool ConfirmDeleteTargets<T>(List<T> targets, Func<T, string> getLabel, DeleteEntityKind kind)
    {
        if (targets.Count == 0)
            return false;
        return targets.Count == 1
            ? ConfirmDelete(getLabel(targets[0]), kind)
            : ConfirmDeleteMany(targets.Count, kind);
    }

    private async Task UndoLastAsync()
    {
        await _undo.UndoAsync();
        OnPropertyChanged(nameof(CanUndo));
        StatusMessage = "Действие отменено";
    }

    private bool ConfirmDeleteMany(int count, DeleteEntityKind kind) =>
        _dialogs.ConfirmDeleteMany(count, kind);

    private async Task DeleteBuildingRowAsync(Building? item)
    {
        if (item is null || !ConfirmDelete(item.Name, DeleteEntityKind.Building))
            return;
        try { await DeleteBuildingCoreAsync(item); }
        catch (Exception ex) { ShowDeleteError(ex); }
    }

    private async Task DeleteSubjectRowAsync(Subject? item)
    {
        if (item is null || !ConfirmDelete(item.Name, DeleteEntityKind.Subject))
            return;
        try { await DeleteSubjectCoreAsync(item); }
        catch (Exception ex) { ShowDeleteError(ex); }
    }

    private async Task DeleteClassRowAsync(SchoolClass? item)
    {
        if (item is null || !ConfirmDelete(item.DisplayName, DeleteEntityKind.SchoolClass))
            return;
        try { await DeleteClassCoreAsync(item); }
        catch (Exception ex) { ShowDeleteError(ex); }
    }

    private async Task DeleteTeacherRowAsync(Teacher? item)
    {
        if (item is null || !ConfirmDelete(item.FullName, DeleteEntityKind.Teacher))
            return;
        await DeleteTeacherCoreAsync(item);
    }

    private async Task DeleteRoomRowAsync(Room? item)
    {
        if (item is null || !ConfirmDelete($"{item.BuildingName} · каб.{item.Number}", DeleteEntityKind.Room))
            return;
        await DeleteRoomCoreAsync(item);
    }

    private async Task DeleteCurriculumGridRowAsync(CurriculumGridRow? row)
    {
        if (row?.Item is not { } item)
        {
            StatusMessage = "Выберите строку нагрузки";
            return;
        }

        var target = CurriculumList.FirstOrDefault(c => c.Id == item.Id) ?? item;
        if (!ConfirmDelete($"{target.ClassName} · {target.SubjectName}", DeleteEntityKind.Curriculum))
            return;

        try
        {
            await DeleteCurriculumCoreAsync(target);
        }
        catch (Exception ex)
        {
            ShowDeleteError(ex);
        }
    }

    private void CancelEdit()
    {
        SelectedBuilding = null;
        SelectedSubject = null;
        SelectedClass = null;
        SelectedCurriculumGridRow = null;
        SelectedCurriculumItem = null;
        SelectedRoom = null;
        SelectedBell = null;
        StatusMessage = "";
        RefreshBlockedBuildingColors();
    }

    private void RefreshBlockedBuildingColors() =>
        OnPropertyChanged(nameof(BlockedBuildingColorHexes));

    private void BeginNewBuilding()
    {
        SelectedBuilding = null;
        NewBuildingName = "";
        NewBuildingColor = BuildingColors.SuggestNext(BuildingList);
        NewBuildingSuggestionHint = "";
        NewBuildingDuplicateHint = "";
        IsNewBuildingDuplicate = false;
        ClearDuplicateHighlights(BuildingList);
        OnPropertyChanged(nameof(SaveBuildingLabel));
        RefreshBlockedBuildingColors();
    }

    private void BeginNewSubject()
    {
        SelectedSubject = null;
        NewSubjectDuplicateHint = "";
        IsNewSubjectDuplicate = false;
        ClearDuplicateHighlights(SubjectList);
        OnPropertyChanged(nameof(SaveSubjectLabel));
    }

    private void BeginNewClass()
    {
        SelectedClass = null;
        NewClassDuplicateHint = "";
        IsNewClassDuplicate = false;
        ClearDuplicateHighlights(ClassList);
        OnPropertyChanged(nameof(SaveClassLabel));
    }

    private void BeginNewRoom()
    {
        SelectedRoom = null;
        NewRoomDuplicateHint = "";
        IsNewRoomDuplicate = false;
        ClearDuplicateHighlights(RoomList);
        OnPropertyChanged(nameof(SaveRoomLabel));
    }

    private void BeginNewCurriculum()
    {
        SelectedCurriculumGridRow = null;
        SelectedCurriculumItem = null;
        NewCurriculumSubject = null;
        NewCurriculumHours = "1";
        NewCurriculumDifficulty = OfficialSubjectDifficultyReference.FormatScore(
            OfficialSubjectDifficultyReference.DefaultFallback);
        NewCurriculumHasSubgroups = false;
        NewCurriculumWeekParity = CurriculumWeekParity.EveryWeek;
        NewCurriculumDuplicateHint = "";
        IsNewCurriculumDuplicate = false;
        ClearDuplicateHighlights(CurriculumList);
        OnPropertyChanged(nameof(SaveCurriculumLabel));
    }

    private void BeginNewBell()
    {
        _clearingBellSelection = true;
        SelectedBell = null;
        NewBellDuplicateHint = "";
        IsNewBellDuplicate = false;
        ClearDuplicateHighlights(BellList);
        ApplySuggestedBellSlot();
        SyncBellLessonDurationFromForm();
        OnPropertyChanged(nameof(SaveBellLabel));
        OnPropertyChanged(nameof(ShowBellDurationSelector));
        Application.Current.Dispatcher.BeginInvoke(
            () => _clearingBellSelection = false,
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void CancelBellEdit() => BeginNewBell();

    private void ResetActiveTabForm()
    {
        switch (ActiveTabIndex)
        {
            case 0: BeginNewBuilding(); break;
            case 1: BeginNewSubject(); break;
            case 2: BeginNewClass(); break;
            case 4: BeginNewRoom(); break;
            case 5: BeginNewCurriculum(); break;
            case 6: BeginNewBell(); break;
        }
    }

    private void LoadBuildingForm(Building? item)
    {
        OnPropertyChanged(nameof(SaveBuildingLabel));
        if (item is null)
        {
            NewBuildingName = "";
            NewBuildingColor = BuildingColors.SuggestNext(BuildingList);
            return;
        }
        NewBuildingName = item.Name;
        NewBuildingColor = BuildingColors.Normalize(item.ColorHex);
    }

    private void LoadSubjectForm(Subject? item)
    {
        OnPropertyChanged(nameof(SaveSubjectLabel));
        if (item is null)
        {
            ResetSubjectDifficultyAutoFill();
            NewSubjectName = "";
            return;
        }

        _subjectDifficultyManuallyEdited = true;
        NewSubjectName = item.Name;
        NewSubjectDifficulty = item.DifficultyScore.ToString(System.Globalization.CultureInfo.InvariantCulture);
        NewSubjectDifficultyHint = "";
    }

    private void ResetSubjectDifficultyAutoFill()
    {
        _subjectDifficultyManuallyEdited = false;
        ApplySuggestedSubjectDifficulty();
    }

    private void ApplySuggestedSubjectDifficulty()
    {
        var grade = CatalogGradeIndex > 0 ? CatalogGradeIndex : (int?)null;
        var match = _subjectCatalog.MatchDifficulty(NewSubjectName, grade);
        var score = match?.Score ?? OfficialSubjectDifficultyReference.DefaultFallback;
        _suppressSubjectDifficultyManualFlag = true;
        NewSubjectDifficulty = OfficialSubjectDifficultyReference.FormatScore(score);
        _suppressSubjectDifficultyManualFlag = false;
        NewSubjectDifficultyHint = match is not null
                                     && !string.IsNullOrWhiteSpace(NewSubjectName)
                                     && !NewSubjectName.Trim().Equals(match.MatchedName, StringComparison.OrdinalIgnoreCase)
            ? match.Hint
            : grade is int g
                ? $"Балл для {g} класса по {OfficialSubjectDifficultyReference.SourceLabelDative}"
                : "Выберите параллель справа для точного балла";
    }

    private void LoadClassForm(SchoolClass? item)
    {
        OnPropertyChanged(nameof(SaveClassLabel));
        if (item is null)
        {
            NewClassGrade = "";
            NewClassLetter = "";
            NewClassShift = "1";
            NewClassStudentCount = "25";
            NewClassIsCorrectional = false;
            NewClassBuilding = null;
            NewClassDefaultRoom = null;
            NewClassDefaultPeRoom = null;
            NewClassBellTemplate = "";
            NewClassShiftSanPinHint = "";
            return;
        }
        NewClassGrade = item.Grade.ToString();
        NewClassLetter = item.Letter;
        NewClassShift = item.Shift.ToString();
        NewClassStudentCount = item.StudentCount.ToString();
        NewClassIsCorrectional = item.IsCorrectional;
        NewClassBuilding = item.BuildingId is int bid
            ? BuildingList.FirstOrDefault(b => b.Id == bid)
            : null;
        NewClassDefaultRoom = item.DefaultRoomId is int rid
            ? RoomList.FirstOrDefault(r => r.Id == rid)
            : null;
        NewClassDefaultPeRoom = item.DefaultPeRoomId is int peId
            ? RoomList.FirstOrDefault(r => r.Id == peId)
            : null;
        NewClassBellTemplate = item.BellTemplateName;
        UpdateClassShiftSanPinHint();
    }

    private void UpdateClassShiftSanPinHint()
    {
        if (!TryBuildDraftClass(out var draft))
        {
            NewClassShiftSanPinHint = "";
            return;
        }

        NewClassShiftSanPinHint = ClassShiftCompliance.GetShiftWarning(draft) ?? "";
    }

    private bool TryBuildDraftClass(out SchoolClass draft)
    {
        draft = new SchoolClass();
        if (!int.TryParse(NewClassGrade, out var grade) || string.IsNullOrWhiteSpace(NewClassLetter))
            return false;

        draft.Grade = grade;
        draft.Letter = NewClassLetter.Trim();
        draft.Shift = int.TryParse(NewClassShift, out var shift) ? shift : 1;
        draft.IsCorrectional = NewClassIsCorrectional;
        return true;
    }

    private int? ResolveNewClassBuildingId() =>
        NewClassBuilding?.Id ?? NewClassDefaultRoom?.BuildingId;

    private void ApplyClassBuildingFields(SchoolClass cls, int? buildingId)
    {
        cls.BuildingId = buildingId;
        var building = buildingId is int id ? BuildingList.FirstOrDefault(b => b.Id == id) : null;
        cls.BuildingName = building?.Name ?? "";
        cls.BuildingColorHex = building?.ColorHex ?? "";
    }

    private void RebuildCurriculumCopyTargets(SchoolClass? sourceClass)
    {
        CurriculumCopyTargetClasses.Clear();
        if (sourceClass is null)
            return;

        foreach (var cls in ClassList
                     .Where(c => c.Grade == sourceClass.Grade && c.Id != sourceClass.Id)
                     .OrderBy(c => c.Letter, StringComparer.OrdinalIgnoreCase))
        {
            CurriculumCopyTargetClasses.Add(new CurriculumCopyClassOption
            {
                ClassId = cls.Id,
                Grade = cls.Grade,
                DisplayName = cls.DisplayName
            });
        }
    }

    private async Task LoadCurriculumTemplatesAsync()
    {
        var selectedId = SelectedCurriculumTemplate?.Id;
        CurriculumTemplateList.Clear();
        foreach (var template in await _curriculumTemplates.GetAllAsync())
            CurriculumTemplateList.Add(template);
        SelectedCurriculumTemplate = selectedId is int id
            ? CurriculumTemplateList.FirstOrDefault(t => t.Id == id)
            : CurriculumTemplateList.FirstOrDefault();
    }

    private void RebuildCurriculumTemplateTargets(CurriculumTemplate? template)
    {
        CurriculumTemplateTargetClasses.Clear();
        if (template is null)
            return;

        foreach (var cls in ClassList
                     .Where(c => c.Grade >= template.GradeFrom && c.Grade <= template.GradeTo)
                     .OrderBy(c => c.Grade)
                     .ThenBy(c => c.Letter, StringComparer.OrdinalIgnoreCase))
        {
            CurriculumTemplateTargetClasses.Add(new CurriculumCopyClassOption
            {
                ClassId = cls.Id,
                Grade = cls.Grade,
                DisplayName = cls.DisplayName,
                IsSelected = false
            });
        }
    }

    private async Task ApplyCurriculumTemplateAsync()
    {
        if (SelectedCurriculumTemplate is null)
        {
            StatusMessage = "Выберите шаблон нагрузки";
            return;
        }

        var targets = CurriculumTemplateTargetClasses.Where(c => c.IsSelected).ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "Отметьте классы, куда загрузить шаблон";
            return;
        }

        var targetIds = targets.Select(t => t.ClassId).ToList();
        var result = await _curriculumTemplateApply.ApplyAsync(
            SelectedCurriculumTemplate.Id,
            targetIds);
        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage;
            return;
        }

        _saveState.MarkDirty();
        await ReloadAfterMutationAsync();

        foreach (var option in CurriculumTemplateTargetClasses)
            option.IsSelected = false;

        var message = $"Шаблон «{SelectedCurriculumTemplate.Name}»: {result.RowsApplied} строк для {targetIds.Count} класс(ов)";
        if (result.SubjectsCreated > 0)
            message += $", создано предметов: {result.SubjectsCreated}";
        if (result.SkippedClassNames.Count > 0)
            message += $". Пропущены (не та параллель): {string.Join(", ", result.SkippedClassNames)}";
        StatusMessage = message;
    }

    private void ToggleCurriculumGroupsExpanded()
    {
        SetAllCurriculumGroupsExpanded(!AreCurriculumGroupsExpanded);
    }

    private void SetAllCurriculumGroupsExpanded(bool expanded)
    {
        foreach (var group in CurriculumGroups)
            group.IsExpanded = expanded;
        NotifyCurriculumGroupsToggleState();
    }

    private void NotifyCurriculumGroupsToggleState()
    {
        OnPropertyChanged(nameof(AreCurriculumGroupsExpanded));
        OnPropertyChanged(nameof(CurriculumGroupsToggleIcon));
        OnPropertyChanged(nameof(CurriculumGroupsToggleToolTip));
    }

    private async Task DeleteCurriculumClassAsync(CurriculumClassGroup? group)
    {
        if (group is null)
            return;

        var targets = CurriculumList.Where(c => c.ClassId == group.ClassId).ToList();
        if (targets.Count == 0)
            return;

        if (!_dialogs.ConfirmProceed(
                "Удалить нагрузку класса",
                $"Удалить все {targets.Count} строк нагрузки для класса {group.ClassName}?"))
            return;

        try
        {
            var snapshots = targets.Select(i => new CurriculumItem
            {
                Id = i.Id,
                ClassId = i.ClassId,
                SubjectId = i.SubjectId,
                HoursPerWeek = i.HoursPerWeek,
                HasSubgroups = i.HasSubgroups,
                WeekParity = i.WeekParity,
                SubjectDifficultyScore = i.SubjectDifficultyScore
            }).ToList();
            await _curriculum.DeleteByClassIdAsync(group.ClassId);
            var undoSnaps = snapshots;
            _undo.Push(async () =>
            {
                foreach (var s in undoSnaps)
                    await _curriculum.UpsertAsync(s);
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            await FinishDeleteAsync(targets.Count, $"Нагрузка класса {group.ClassName} удалена");
        }
        catch (Exception ex) { ShowDeleteError(ex); }
    }

    private void LoadCurriculumForm(CurriculumItem? item)
    {
        OnPropertyChanged(nameof(SaveCurriculumLabel));
        if (item is null)
        {
            NewCurriculumSubject = null;
            NewCurriculumHours = "1";
            NewCurriculumDifficulty = OfficialSubjectDifficultyReference.FormatScore(
                OfficialSubjectDifficultyReference.DefaultFallback);
            NewCurriculumHasSubgroups = false;
            NewCurriculumWeekParity = CurriculumWeekParity.EveryWeek;
            return;
        }
        NewCurriculumClass = ClassList.FirstOrDefault(c => c.Id == item.ClassId);
        NewCurriculumSubject = SubjectList.FirstOrDefault(s => s.Id == item.SubjectId);
        NewCurriculumHours = item.HoursPerWeek.ToString(System.Globalization.CultureInfo.InvariantCulture);
        NewCurriculumDifficulty = OfficialSubjectDifficultyReference.FormatScore(item.SubjectDifficultyScore);
        NewCurriculumHasSubgroups = item.HasSubgroups;
        NewCurriculumWeekParity = item.WeekParity;
    }

    private void LoadRoomForm(Room? item)
    {
        OnPropertyChanged(nameof(SaveRoomLabel));
        if (item is null)
        {
            NewRoomNumber = "";
            NewRoomCapacity = "30";
            NewRoomKind = RoomKinds.ToDisplay("");
            NewRoomAllowsParallelGroups = false;
            SelectedRoomBuilding = BuildingList.FirstOrDefault();
            OnPropertyChanged(nameof(IsNewRoomSportsHall));
            return;
        }
        NewRoomNumber = item.Number;
        NewRoomCapacity = item.Capacity.ToString();
        NewRoomKind = RoomKinds.ToDisplay(item.RoomKind);
        NewRoomAllowsParallelGroups = item.AllowsParallelGroups;
        SelectedRoomBuilding = BuildingList.FirstOrDefault(b => b.Id == item.BuildingId);
        OnPropertyChanged(nameof(IsNewRoomSportsHall));
    }

    private void LoadBellForm(BellPeriod? item)
    {
        OnPropertyChanged(nameof(SaveBellLabel));
        if (item is null)
            return;

        NewBellTemplate = item.TemplateName;
        NewBellGradeFrom = item.TemplateGradeFrom.ToString();
        NewBellGradeTo = item.TemplateGradeTo.ToString();
        NewBellPeriodKind = item.PeriodKind;
        NewBellLessonNumber = item.LessonNumber.ToString();
        NewBellShift = item.Shift.ToString();
        NewBellStartTime = item.StartTime;
        NewBellEndTime = item.EndTime;
        SyncBellLessonDurationFromForm();
        OnPropertyChanged(nameof(ShowBellDurationSelector));
    }

    private async Task FinishDeleteAsync(int count, string singleMessage)
    {
        _saveState.MarkDirty();
        await ReloadAfterMutationAsync();
        ResetActiveTabForm();
        StatusMessage = count == 1 ? singleMessage : $"Удалено записей: {count}";
    }

    private async Task SaveBuildingAsync()
    {
        if (string.IsNullOrWhiteSpace(NewBuildingName))
            return;
        var name = ProperNameFormatter.FormatBuildingOrAddress(NewBuildingName);
        UpdateBuildingDuplicate(name);
        if (IsNewBuildingDuplicate)
        {
            StatusMessage = NewBuildingDuplicateHint;
            return;
        }
        var color = BuildingColors.Normalize(NewBuildingColor);
        if (BuildingColors.IsTaken(color, BuildingList, SelectedBuilding?.Id))
        {
            StatusMessage = $"Цвет «{BuildingColors.LabelFor(color)}» уже используется другим зданием";
            return;
        }
        var isNew = SelectedBuilding is null;
        int? savedId = SelectedBuilding?.Id;
        if (SelectedBuilding is not null)
        {
            var before = new Building
            {
                Id = SelectedBuilding.Id,
                Name = SelectedBuilding.Name,
                ColorHex = SelectedBuilding.ColorHex
            };
            SelectedBuilding.Name = name;
            SelectedBuilding.ColorHex = color;
            await _buildings.UpdateAsync(SelectedBuilding);
            _undo.Push(async () =>
            {
                await _buildings.UpdateAsync(before);
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            StatusMessage = $"Здание «{name}» сохранено";
        }
        else
        {
            var id = await _buildings.InsertAsync(new Building { Name = name, ColorHex = color });
            if (BuildingList.Count >= 1)
                await _buildings.EnsureDefaultRoutesForBuildingAsync(id);
            _undo.Push(async () =>
            {
                await _buildings.DeleteAsync(id);
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            StatusMessage = $"Добавлено здание «{name}»";
        }
        NewBuildingSuggestionHint = "";
        NewBuildingDuplicateHint = "";
        IsNewBuildingDuplicate = false;
        ClearDuplicateHighlights(BuildingList);
        _saveState.MarkDirty();
        await ReloadAfterMutationAsync();
        CrudFormHelper.ApplyAfterReload(isNew, savedId, BuildingList, b => b.Id, BeginNewBuilding, b => SelectedBuilding = b);
    }

    private void LoadBuildingRouteMatrix()
    {
        BuildingRouteMatrix.Clear();
        if (BuildingList.Count < 2)
            return;

        foreach (var from in BuildingList.OrderBy(b => b.Name))
        {
            foreach (var to in BuildingList.Where(b => b.Id != from.Id).OrderBy(b => b.Name))
            {
                var minutes = _buildingRoutes
                    .FirstOrDefault(r => r.FromBuildingId == from.Id && r.ToBuildingId == to.Id)
                    ?.Minutes;
                BuildingRouteMatrix.Add(new BuildingRouteMatrixRow
                {
                    FromBuildingId = from.Id,
                    FromBuildingName = from.Name,
                    FromBuildingColorHex = from.ColorHex,
                    ToBuildingId = to.Id,
                    ToBuildingName = to.Name,
                    ToBuildingColorHex = to.ColorHex,
                    Minutes = minutes?.ToString() ?? BuildingRouteDefaults.MinutesText
                });
            }
        }
    }

    private async Task SaveBuildingRoutesAsync()
    {
        if (BuildingList.Count < 2)
        {
            _dialogs.ShowInfo("Здания", "Сначала добавьте минимум два здания.");
            return;
        }

        var saved = 0;
        foreach (var row in BuildingRouteMatrix)
        {
            if (string.IsNullOrWhiteSpace(row.Minutes))
                continue;
            if (!int.TryParse(row.Minutes.Trim(), out var minutes) || minutes < 1)
            {
                _dialogs.ShowInfo("Здания",
                    $"Укажите минуты для «{row.FromBuildingName} → {row.ToBuildingName}» (целое число ≥ 1).");
                return;
            }

            await _buildings.InsertRouteAsync(row.FromBuildingId, row.ToBuildingId, minutes);
            saved++;
        }

        _buildingRoutes = await _buildings.GetRoutesAsync();
        LoadBuildingRouteMatrix();
        _saveState.MarkDirty();
        StatusMessage = saved > 0
            ? $"Сохранено переходов: {saved}"
            : "Укажите минуты хотя бы для одной пары зданий";
    }

    private const int RouteMinutesStep = 5;
    private const int RouteMinutesMax = 180;

    private static int ParseRouteMinutesOrDefault(string? text) =>
        int.TryParse(text?.Trim(), out var value) && value >= 1
            ? value
            : BuildingRouteDefaults.Minutes;

    private void AdjustRouteMinutes(BuildingRouteMatrixRow? row, int delta)
    {
        if (row is null)
            return;

        var next = Math.Clamp(ParseRouteMinutesOrDefault(row.Minutes) + delta, 1, RouteMinutesMax);
        row.Minutes = next.ToString();
    }

    private static void ResetRouteMinutesToDefault(BuildingRouteMatrixRow? row)
    {
        if (row is null)
            return;
        row.Minutes = BuildingRouteDefaults.MinutesText;
    }

    private static void ClearRouteMinutes(BuildingRouteMatrixRow? row)
    {
        if (row is null)
            return;
        row.Minutes = "";
    }

    private void ApplyDefaultBuildingRoutes()
    {
        foreach (var row in BuildingRouteMatrix)
            row.Minutes = BuildingRouteDefaults.MinutesText;
        StatusMessage = $"Во все пары подставлено {BuildingRouteDefaults.Minutes} мин — нажмите «Сохранить переходы»";
    }

    private async Task DeleteBuildingAsync()
    {
        var targets = GetDeleteTargets(BuildingList, SelectedBuilding);
        if (!ConfirmDeleteTargets(targets, b => b.Name, DeleteEntityKind.Building))
            return;
        try
        {
            var snapshots = targets.Select(b => new Building { Id = b.Id, Name = b.Name, ColorHex = b.ColorHex }).ToList();
            foreach (var t in targets)
            {
                var result = await _buildings.TryDeleteAsync(t.Id);
                if (!result.Success)
                {
                    _dialogs.ShowWarning("Не удалось удалить", result.ErrorMessage ?? "Неизвестная ошибка.");
                    return;
                }
            }
            var undoSnaps = snapshots;
            _undo.Push(async () =>
            {
                foreach (var s in undoSnaps)
                    await _buildings.InsertAsync(new Building { Name = s.Name, ColorHex = s.ColorHex });
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            await FinishDeleteAsync(targets.Count, "Здание удалено");
        }
        catch (Exception ex) { ShowDeleteError(ex); }
    }

    private async Task DeleteBuildingCoreAsync(Building item)
    {
        var result = await _buildings.TryDeleteAsync(item.Id);
        if (!result.Success)
        {
            _dialogs.ShowWarning("Не удалось удалить", result.ErrorMessage ?? "Неизвестная ошибка.");
            return;
        }

        var snap = new Building { Id = item.Id, Name = item.Name, ColorHex = item.ColorHex };
        _undo.Push(async () =>
        {
            await _buildings.InsertAsync(new Building { Name = snap.Name, ColorHex = snap.ColorHex });
            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();
        });
        await FinishDeleteAsync(1, "Здание удалено");
    }

    private async Task SaveSubjectAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSubjectName))
            return;
        var name = ProperNameFormatter.FormatTitle(NewSubjectName);
        if (!double.TryParse(NewSubjectDifficulty.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var difficulty) || difficulty < 0)
            difficulty = _subjectCatalog.ResolveDifficulty(name, CatalogGradeIndex > 0 ? CatalogGradeIndex : null);

        var isNew = SelectedSubject is null;
        int? savedId = SelectedSubject?.Id;
        UpdateSubjectDuplicate(name);
        if (IsNewSubjectDuplicate)
        {
            StatusMessage = NewSubjectDuplicateHint;
            return;
        }
        if (SelectedSubject is not null)
        {
            var before = new Subject
            {
                Id = SelectedSubject.Id,
                Name = SelectedSubject.Name,
                DifficultyScore = SelectedSubject.DifficultyScore
            };
            SelectedSubject.Name = name;
            SelectedSubject.DifficultyScore = difficulty;
            await _subjects.UpdateAsync(SelectedSubject);
            _undo.Push(async () =>
            {
                await _subjects.UpdateAsync(before);
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            StatusMessage = $"Предмет «{name}» сохранён";
        }
        else
        {
            var id = await _subjects.InsertAsync(new Subject { Name = name, DifficultyScore = difficulty });
            _undo.Push(async () =>
            {
                await _subjects.DeleteAsync(id);
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            StatusMessage = $"Добавлен предмет «{name}»";
        }
        NewSubjectSuggestionHint = "";
        NewSubjectDuplicateHint = "";
        IsNewSubjectDuplicate = false;
        ClearDuplicateHighlights(SubjectList);
        _saveState.MarkDirty();
        await ReloadAfterMutationAsync();
        CrudFormHelper.ApplyAfterReload(isNew, savedId, SubjectList, s => s.Id, BeginNewSubject, s => SelectedSubject = s);
    }

    private async Task DeleteSubjectAsync()
    {
        var targets = GetDeleteTargets(SubjectList, SelectedSubject);
        if (!ConfirmDeleteTargets(targets, s => s.Name, DeleteEntityKind.Subject))
            return;
        try
        {
            var snapshots = targets.Select(s => new Subject { Id = s.Id, Name = s.Name, DifficultyScore = s.DifficultyScore }).ToList();
            foreach (var t in targets)
            {
                var result = await _subjects.TryDeleteAsync(t.Id);
                if (!result.Success)
                {
                    _dialogs.ShowWarning("Не удалось удалить", result.ErrorMessage ?? "Неизвестная ошибка.");
                    return;
                }
            }
            var undoSnaps = snapshots;
            _undo.Push(async () =>
            {
                foreach (var s in undoSnaps)
                    await _subjects.InsertAsync(new Subject { Name = s.Name, DifficultyScore = s.DifficultyScore });
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            await FinishDeleteAsync(targets.Count, "Предмет удалён");
        }
        catch (Exception ex) { ShowDeleteError(ex); }
    }

    private async Task DeleteSubjectCoreAsync(Subject item)
    {
        var result = await _subjects.TryDeleteAsync(item.Id);
        if (!result.Success)
        {
            _dialogs.ShowWarning("Не удалось удалить", result.ErrorMessage ?? "Неизвестная ошибка.");
            return;
        }

        var snap = new Subject { Id = item.Id, Name = item.Name, DifficultyScore = item.DifficultyScore };
        _undo.Push(async () =>
        {
            await _subjects.InsertAsync(new Subject { Name = snap.Name, DifficultyScore = snap.DifficultyScore });
            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();
        });
        await FinishDeleteAsync(1, "Предмет удалён");
    }

    private async Task SaveCurriculumAsync()
    {
        if (NewCurriculumClass is null || NewCurriculumSubject is null)
        {
            StatusMessage = "Выберите класс и предмет";
            return;
        }
        if (!double.TryParse(NewCurriculumHours.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var hours) || hours <= 0)
        {
            StatusMessage = "Укажите число часов в неделю";
            return;
        }
        if (!double.TryParse(NewCurriculumDifficulty.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var sivkov) || sivkov < 0)
        {
            StatusMessage = "Укажите корректный балл Сивкова (0 и выше)";
            return;
        }

        var item = new CurriculumItem
        {
            ClassId = NewCurriculumClass.Id,
            SubjectId = NewCurriculumSubject.Id,
            HoursPerWeek = hours,
            HasSubgroups = NewCurriculumHasSubgroups,
            WeekParity = NewCurriculumWeekParity,
            SubjectDifficultyScore = sivkov
        };

        var isNew = SelectedCurriculumItem is null;
        int? savedId = SelectedCurriculumItem?.Id;
        var keepClassId = NewCurriculumClass.Id;
        var keepWeekParity = NewCurriculumWeekParity;
        UpdateCurriculumDuplicate();
        if (IsNewCurriculumDuplicate)
        {
            StatusMessage = NewCurriculumDuplicateHint;
            return;
        }
        if (SelectedCurriculumItem is not null)
        {
            var before = new CurriculumItem
            {
                Id = SelectedCurriculumItem.Id,
                ClassId = SelectedCurriculumItem.ClassId,
                SubjectId = SelectedCurriculumItem.SubjectId,
                HoursPerWeek = SelectedCurriculumItem.HoursPerWeek,
                HasSubgroups = SelectedCurriculumItem.HasSubgroups,
                WeekParity = SelectedCurriculumItem.WeekParity,
                SubjectDifficultyScore = SelectedCurriculumItem.SubjectDifficultyScore
            };
            item.Id = SelectedCurriculumItem.Id;
            await _curriculum.UpdateAsync(item);
            _undo.Push(async () =>
            {
                await _curriculum.UpdateAsync(before);
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            StatusMessage = "Нагрузка обновлена";
        }
        else
        {
            var id = await _curriculum.InsertAsync(item);
            _undo.Push(async () =>
            {
                await _curriculum.DeleteAsync(id);
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            StatusMessage = $"Нагрузка: {NewCurriculumClass.DisplayName} · {NewCurriculumSubject.Name}";
        }
        _saveState.MarkDirty();
        await ReloadAfterMutationAsync();
        if (isNew)
        {
            SelectedCurriculumItem = null;
            NewCurriculumClass = ClassList.FirstOrDefault(c => c.Id == keepClassId);
            NewCurriculumSubject = null;
            NewCurriculumHours = "1";
            NewCurriculumWeekParity = keepWeekParity;
            NewCurriculumHasSubgroups = false;
            NewCurriculumDifficulty = OfficialSubjectDifficultyReference.FormatScore(
                OfficialSubjectDifficultyReference.DefaultFallback);
            UpdateCurriculumDuplicate();
            OnPropertyChanged(nameof(SaveCurriculumLabel));
            return;
        }

        CrudFormHelper.ApplyAfterReload(isNew, savedId, CurriculumList, c => c.Id, BeginNewCurriculum,
            c => SelectedCurriculumItem = c);
    }

    private async Task CopyCurriculumToClassesAsync()
    {
        if (CurriculumCopySourceClass is null)
        {
            StatusMessage = "Выберите класс-источник для копирования";
            return;
        }

        var targets = CurriculumCopyTargetClasses.Where(c => c.IsSelected).ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "Отметьте классы, куда копировать нагрузку";
            return;
        }

        var sourceItems = CurriculumList
            .Where(c => c.ClassId == CurriculumCopySourceClass.Id)
            .ToList();
        if (sourceItems.Count == 0)
        {
            StatusMessage = $"У класса {CurriculumCopySourceClass.DisplayName} нет нагрузки для копирования";
            return;
        }

        var copied = 0;
        foreach (var target in targets)
        {
            foreach (var source in sourceItems)
            {
                await _curriculum.UpsertAsync(new CurriculumItem
                {
                    ClassId = target.ClassId,
                    SubjectId = source.SubjectId,
                    HoursPerWeek = source.HoursPerWeek,
                    HasSubgroups = source.HasSubgroups,
                    WeekParity = source.WeekParity,
                    SubjectDifficultyScore = source.SubjectDifficultyScore
                });
                copied++;
            }
        }

        _saveState.MarkDirty();
        await ReloadAfterMutationAsync();
        foreach (var t in CurriculumCopyTargetClasses)
            t.IsSelected = false;
        StatusMessage = $"Скопировано строк нагрузки: {copied}";
    }

    private async Task DeleteCurriculumAsync()
    {
        var targets = GetCurriculumDeleteTargets();
        if (targets.Count == 0)
        {
            StatusMessage = "Выберите строку в таблице или отметьте галочкой слева";
            return;
        }

        if (!ConfirmDeleteTargets(targets, i => $"{i.ClassName} · {i.SubjectName}", DeleteEntityKind.Curriculum))
            return;
        try
        {
            var snapshots = targets.Select(i => new CurriculumItem
            {
                Id = i.Id,
                ClassId = i.ClassId,
                SubjectId = i.SubjectId,
                HoursPerWeek = i.HoursPerWeek,
                HasSubgroups = i.HasSubgroups,
                WeekParity = i.WeekParity,
                SubjectDifficultyScore = i.SubjectDifficultyScore
            }).ToList();
            foreach (var t in targets)
                await _curriculum.DeleteAsync(t.Id);
            var undoSnaps = snapshots;
            _undo.Push(async () =>
            {
                foreach (var s in undoSnaps)
                    await _curriculum.UpsertAsync(s);
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            await FinishDeleteAsync(targets.Count, "Строка нагрузки удалена");
        }
        catch (Exception ex) { ShowDeleteError(ex); }
    }

    private async Task DeleteCurriculumCoreAsync(CurriculumItem item)
    {
        var snap = new CurriculumItem
        {
            Id = item.Id,
            ClassId = item.ClassId,
            SubjectId = item.SubjectId,
            HoursPerWeek = item.HoursPerWeek,
            HasSubgroups = item.HasSubgroups,
            WeekParity = item.WeekParity,
            SubjectDifficultyScore = item.SubjectDifficultyScore
        };
        await _curriculum.DeleteAsync(item.Id);
        _undo.Push(async () =>
        {
            await _curriculum.UpsertAsync(snap);
            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();
        });
        await FinishDeleteAsync(1, "Строка нагрузки удалена");
    }

    private async Task SaveClassAsync()
    {
        if (!int.TryParse(NewClassGrade, out var grade) || string.IsNullOrWhiteSpace(NewClassLetter))
            return;
        if (!int.TryParse(NewClassShift, out var shift))
            shift = 1;
        if (!int.TryParse(NewClassStudentCount, out var count))
            count = 25;

        var isNew = SelectedClass is null;
        int? savedId = SelectedClass?.Id;
        UpdateClassDuplicate();
        if (IsNewClassDuplicate)
        {
            StatusMessage = NewClassDuplicateHint;
            return;
        }

        var buildingId = ResolveNewClassBuildingId();
        var bellTemplateId = await ResolveClassBellTemplateIdAsync();
        var bellTemplateName = NewClassBellTemplate.Trim();
        var draft = new SchoolClass
        {
            Grade = grade,
            Letter = NewClassLetter.Trim(),
            Shift = shift,
            StudentCount = count,
            IsCorrectional = NewClassIsCorrectional,
            BuildingId = buildingId,
            DefaultRoomId = NewClassDefaultRoom?.Id,
            DefaultPeRoomId = NewClassDefaultPeRoom?.Id
        };
        if (ClassShiftCompliance.ViolatesSecondShiftRule(draft)
            && !_dialogs.ConfirmProceed(
                "Смена и СанПиН",
                ClassShiftCompliance.FormatShiftViolation(draft) + "\n\nСохранить класс с 2-й сменой всё равно?"))
        {
            return;
        }

        if (SelectedClass is not null)
        {
            var before = new SchoolClass
            {
                Id = SelectedClass.Id,
                Grade = SelectedClass.Grade,
                Letter = SelectedClass.Letter,
                Shift = SelectedClass.Shift,
                StudentCount = SelectedClass.StudentCount,
                IsCorrectional = SelectedClass.IsCorrectional,
                BuildingId = SelectedClass.BuildingId,
                DefaultRoomId = SelectedClass.DefaultRoomId,
                DefaultPeRoomId = SelectedClass.DefaultPeRoomId,
                BellTemplateId = SelectedClass.BellTemplateId,
                BellTemplateName = SelectedClass.BellTemplateName
            };
            SelectedClass.Grade = grade;
            SelectedClass.Letter = NewClassLetter.Trim();
            SelectedClass.Shift = shift;
            SelectedClass.StudentCount = count;
            SelectedClass.IsCorrectional = NewClassIsCorrectional;
            ApplyClassBuildingFields(SelectedClass, buildingId);
            SelectedClass.DefaultRoomId = NewClassDefaultRoom?.Id;
            SelectedClass.DefaultRoomDisplay = NewClassDefaultRoom is null
                ? ""
                : $"{NewClassDefaultRoom.Number} · {NewClassDefaultRoom.BuildingName}";
            SelectedClass.DefaultPeRoomId = NewClassDefaultPeRoom?.Id;
            SelectedClass.DefaultPeRoomDisplay = NewClassDefaultPeRoom is null
                ? ""
                : $"{NewClassDefaultPeRoom.Number} · {NewClassDefaultPeRoom.BuildingName}";
            SelectedClass.BellTemplateId = bellTemplateId;
            SelectedClass.BellTemplateName = bellTemplateName;
            await _classes.UpdateAsync(SelectedClass);
            _undo.Push(async () =>
            {
                await _classes.UpdateAsync(before);
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            StatusMessage = $"Класс {SelectedClass.DisplayName} сохранён";
        }
        else
        {
            var id = await _classes.InsertAsync(new SchoolClass
            {
                Grade = grade,
                Letter = NewClassLetter.Trim(),
                Shift = shift,
                StudentCount = count,
                IsCorrectional = NewClassIsCorrectional,
                BuildingId = buildingId,
                DefaultRoomId = NewClassDefaultRoom?.Id,
                DefaultPeRoomId = NewClassDefaultPeRoom?.Id,
                BellTemplateId = bellTemplateId,
                BellTemplateName = bellTemplateName
            });
            _undo.Push(async () =>
            {
                await _classes.DeleteAsync(id);
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            StatusMessage = $"Добавлен класс {grade}{NewClassLetter.Trim()}";
        }
        _saveState.MarkDirty();
        await ReloadAfterMutationAsync();
        CrudFormHelper.ApplyAfterReload(isNew, savedId, ClassList, c => c.Id, BeginNewClass, c => SelectedClass = c);
    }

    private async Task DeleteClassAsync()
    {
        var targets = GetDeleteTargets(ClassList, SelectedClass);
        if (!ConfirmDeleteTargets(targets, c => c.DisplayName, DeleteEntityKind.SchoolClass))
            return;
        try
        {
            var snapshots = targets.Select(c => new SchoolClass
            {
                Id = c.Id,
                Grade = c.Grade,
                Letter = c.Letter,
                Shift = c.Shift,
                StudentCount = c.StudentCount
            }).ToList();
            foreach (var t in targets)
            {
                var result = await _classes.TryDeleteAsync(t.Id);
                if (!result.Success)
                {
                    _dialogs.ShowWarning("Не удалось удалить", result.ErrorMessage ?? "Неизвестная ошибка.");
                    return;
                }
            }
            var undoSnaps = snapshots;
            _undo.Push(async () =>
            {
                foreach (var s in undoSnaps)
                    await _classes.InsertAsync(new SchoolClass
                    {
                        Grade = s.Grade,
                        Letter = s.Letter,
                        Shift = s.Shift,
                        StudentCount = s.StudentCount
                    });
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            await FinishDeleteAsync(targets.Count, "Класс удалён");
        }
        catch (Exception ex) { ShowDeleteError(ex); }
    }

    private async Task DeleteClassCoreAsync(SchoolClass item)
    {
        var result = await _classes.TryDeleteAsync(item.Id);
        if (!result.Success)
        {
            _dialogs.ShowWarning("Не удалось удалить", result.ErrorMessage ?? "Неизвестная ошибка.");
            return;
        }

        var snap = new SchoolClass
        {
            Id = item.Id,
            Grade = item.Grade,
            Letter = item.Letter,
            Shift = item.Shift,
            StudentCount = item.StudentCount
        };
        _undo.Push(async () =>
        {
            await _classes.InsertAsync(new SchoolClass
            {
                Grade = snap.Grade,
                Letter = snap.Letter,
                Shift = snap.Shift,
                StudentCount = snap.StudentCount
            });
            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();
        });
        await FinishDeleteAsync(1, "Класс удалён");
    }

    private async Task SaveRoomAsync()
    {
        if (string.IsNullOrWhiteSpace(NewRoomNumber) || SelectedRoomBuilding is null)
        {
            StatusMessage = "Укажите кабинет и здание";
            return;
        }
        if (!int.TryParse(NewRoomCapacity, out var capacity))
            capacity = 30;

        var isNew = SelectedRoom is null;
        int? savedId = SelectedRoom?.Id;
        UpdateRoomDuplicate();
        if (IsNewRoomDuplicate)
        {
            StatusMessage = NewRoomDuplicateHint;
            return;
        }
        if (SelectedRoom is not null)
        {
            var before = new Room
            {
                Id = SelectedRoom.Id,
                Number = SelectedRoom.Number,
                BuildingId = SelectedRoom.BuildingId,
                Capacity = SelectedRoom.Capacity,
                RoomKind = SelectedRoom.RoomKind,
                AllowsParallelGroups = SelectedRoom.AllowsParallelGroups
            };
            SelectedRoom.Number = NewRoomNumber.Trim();
            SelectedRoom.BuildingId = SelectedRoomBuilding.Id;
            SelectedRoom.Capacity = capacity;
            SelectedRoom.RoomKind = RoomKinds.FromDisplay(NewRoomKind);
            SelectedRoom.AllowsParallelGroups = NewRoomAllowsParallelGroups && IsNewRoomSportsHall;
            await _rooms.UpdateAsync(SelectedRoom);
            _undo.Push(async () =>
            {
                await _rooms.UpdateAsync(before);
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            StatusMessage = $"Кабинет {SelectedRoom.Number} сохранён";
        }
        else
        {
            var id = await _rooms.InsertAsync(new Room
            {
                Number = NewRoomNumber.Trim(),
                BuildingId = SelectedRoomBuilding.Id,
                Capacity = capacity,
                RoomKind = RoomKinds.FromDisplay(NewRoomKind),
                AllowsParallelGroups = NewRoomAllowsParallelGroups && IsNewRoomSportsHall
            });
            _undo.Push(async () =>
            {
                await _rooms.DeleteAsync(id);
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            StatusMessage = $"Добавлен кабинет {NewRoomNumber.Trim()}";
        }
        _saveState.MarkDirty();
        await ReloadAfterMutationAsync();
        CrudFormHelper.ApplyAfterReload(isNew, savedId, RoomList, r => r.Id, BeginNewRoom, r => SelectedRoom = r);
    }

    private async Task DeleteRoomAsync()
    {
        var targets = GetDeleteTargets(RoomList, SelectedRoom);
        if (!ConfirmDeleteTargets(targets, r => $"{r.BuildingName} · каб.{r.Number}", DeleteEntityKind.Room))
            return;
        try
        {
            var snapshots = targets.Select(r => new Room
            {
                Id = r.Id,
                Number = r.Number,
                BuildingId = r.BuildingId,
                Capacity = r.Capacity
            }).ToList();
            foreach (var t in targets)
                await _rooms.DeleteAsync(t.Id);
            var undoSnaps = snapshots;
            _undo.Push(async () =>
            {
                foreach (var s in undoSnaps)
                    await _rooms.InsertAsync(new Room
                    {
                        Number = s.Number,
                        BuildingId = s.BuildingId,
                        Capacity = s.Capacity
                    });
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            await FinishDeleteAsync(targets.Count, "Кабинет удалён");
        }
        catch (Exception ex) { ShowDeleteError(ex); }
    }

    private async Task DeleteRoomCoreAsync(Room item)
    {
        var snap = new Room { Id = item.Id, Number = item.Number, BuildingId = item.BuildingId, Capacity = item.Capacity };
        await _rooms.DeleteAsync(item.Id);
        _undo.Push(async () =>
        {
            await _rooms.InsertAsync(new Room { Number = snap.Number, BuildingId = snap.BuildingId, Capacity = snap.Capacity });
            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();
        });
        await FinishDeleteAsync(1, "Кабинет удалён");
    }

    private static BellPeriod CopyBell(BellPeriod b) => new()
    {
        Id = b.Id,
        TemplateId = b.TemplateId,
        TemplateName = b.TemplateName,
        TemplateGradeFrom = b.TemplateGradeFrom,
        TemplateGradeTo = b.TemplateGradeTo,
        LessonNumber = b.LessonNumber,
        Shift = b.Shift,
        StartTime = b.StartTime,
        EndTime = b.EndTime,
        PeriodKind = b.PeriodKind
    };

    private void RefreshBellFilter() => RefreshBellTimeline();

    private async Task RefreshBellTemplateNamesAsync()
    {
        BellTemplateNames.Clear();
        foreach (var name in await _bells.GetTemplateNamesAsync())
            BellTemplateNames.Add(name);
        SanitizeBellTemplateUi();
    }

    private bool IsKnownBellTemplate(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && BellTemplateNames.Any(n => n.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    private async Task SaveBellAsync()
    {
        var templateName = (SelectedBellTemplateCard?.Name ?? NewBellTemplate).Trim();
        if (string.IsNullOrWhiteSpace(templateName) ||
            string.IsNullOrWhiteSpace(NewBellStartTime) ||
            string.IsNullOrWhiteSpace(NewBellEndTime))
            return;

        var shiftValue = SelectedBell is null
            ? SelectedBellEditorShift
            : int.TryParse(NewBellShift, out var parsedShift) ? parsedShift : SelectedBellEditorShift;
        if (shiftValue < 1)
            return;
        if (!int.TryParse(EditBellGradeFrom, out var gradeFrom))
            return;
        if (!int.TryParse(EditBellGradeTo, out var gradeTo))
            return;
        if (gradeTo < gradeFrom)
            (gradeFrom, gradeTo) = (gradeTo, gradeFrom);

        if (!int.TryParse(NewBellLessonNumber, out var lesson))
            return;
        if (BellPeriodKinds.IsLesson(NewBellPeriodKind) && lesson < 1)
            return;
        if (!BellPeriodKinds.IsLesson(NewBellPeriodKind) && lesson < 0)
            return;

        UpdateBellDuplicate(templateName, lesson, shiftValue, NewBellPeriodKind);
        if (IsNewBellDuplicate)
        {
            StatusMessage = NewBellDuplicateHint;
            return;
        }

        var templateId = await _bells.EnsureTemplateAsync(templateName, gradeFrom, gradeTo);
        await _bells.UpdateTemplateGradesAsync(templateId, gradeFrom, gradeTo);
        var start = BellTime.NormalizeInput(NewBellStartTime);
        var end = BellTime.NormalizeInput(NewBellEndTime);
        var isNew = SelectedBell is null;
        int? savedId = SelectedBell?.Id;

        if (SelectedBell is not null)
        {
            var before = CopyBell(SelectedBell);
            var item = CopyBell(SelectedBell);
            item.TemplateId = templateId;
            item.TemplateName = templateName;
            item.LessonNumber = lesson;
            item.Shift = shiftValue;
            item.StartTime = start;
            item.EndTime = end;
            item.PeriodKind = NewBellPeriodKind;
            item.TemplateGradeFrom = gradeFrom;
            item.TemplateGradeTo = gradeTo;
            await _bells.UpdateAsync(item);
            _undo.Push(async () =>
            {
                await _bells.UpdateAsync(before);
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            StatusMessage = $"«{BellPeriodKinds.ToDisplay(NewBellPeriodKind)}» ({templateName}) сохранено";
        }
        else
        {
            var id = await _bells.InsertPeriodAsync(new BellPeriod
            {
                TemplateId = templateId,
                TemplateName = templateName,
                TemplateGradeFrom = gradeFrom,
                TemplateGradeTo = gradeTo,
                LessonNumber = lesson,
                Shift = shiftValue,
                StartTime = start,
                EndTime = end,
                PeriodKind = NewBellPeriodKind
            });
            var snap = new BellPeriod
            {
                Id = id,
                TemplateId = templateId,
                TemplateName = templateName,
                TemplateGradeFrom = gradeFrom,
                TemplateGradeTo = gradeTo,
                LessonNumber = lesson,
                Shift = shiftValue,
                StartTime = start,
                EndTime = end,
                PeriodKind = NewBellPeriodKind
            };
            _undo.Push(async () =>
            {
                await _bells.DeleteAsync(snap.Id);
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            StatusMessage = $"Добавлено: {BellPeriodKinds.ToDisplay(NewBellPeriodKind)} · {start}–{end}";
        }

        NewBellDuplicateHint = "";
        IsNewBellDuplicate = false;
        ClearDuplicateHighlights(BellList);
        _saveState.MarkDirty();
        await ReloadAfterMutationAsync();
        CrudFormHelper.ApplyAfterReload(isNew, savedId, BellList, b => b.Id, BeginNewBell, b => SelectedBell = b);
    }

    private async Task DeleteBellAsync()
    {
        var targets = GetDeleteTargets(BellList, SelectedBell);
        if (!ConfirmDeleteTargets(targets, b => $"{b.TemplateName} · {b.SlotLabel}", DeleteEntityKind.Bell))
            return;
        try
        {
            var snapshots = targets.Select(CopyBell).ToList();
            foreach (var t in targets)
                await _bells.DeleteAsync(t.Id);
            var undoSnaps = snapshots;
            _undo.Push(async () =>
            {
                foreach (var s in undoSnaps)
                {
                    s.TemplateId = await _bells.EnsureTemplateAsync(s.TemplateName);
                    await _bells.InsertPeriodAsync(s);
                }
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            await FinishDeleteAsync(targets.Count, "Звонок удалён");
        }
        catch (Exception ex) { ShowDeleteError(ex); }
    }

    private async Task DeleteBellRowAsync(BellPeriod? item)
    {
        if (item is null || !ConfirmDelete($"{item.TemplateName} · {item.SlotLabel}", DeleteEntityKind.Bell))
            return;
        await DeleteBellCoreAsync(item);
    }

    private async Task DeleteBellCoreAsync(BellPeriod item)
    {
        var snap = CopyBell(item);
        await _bells.DeleteAsync(item.Id);
        _undo.Push(async () =>
        {
            snap.TemplateId = await _bells.EnsureTemplateAsync(snap.TemplateName);
            await _bells.InsertPeriodAsync(snap);
            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();
        });
        await FinishDeleteAsync(1, "Звонок удалён");
    }

    private async Task DeleteTeacherAsync()
    {
        var targets = GetDeleteTargets(TeacherList, SelectedTeacher);
        if (!ConfirmDeleteTargets(targets, t => t.FullName, DeleteEntityKind.Teacher))
            return;
        try
        {
            var snapshots = targets.Select(CopyTeacher).ToList();
            foreach (var t in targets)
            {
                await _teachers.DeleteAsync(t.Id);
                if (SelectedTeacher?.Id == t.Id)
                    SelectedTeacher = null;
            }
            var undoSnaps = snapshots;
            _undo.Push(async () =>
            {
                foreach (var s in undoSnaps)
                    await _teachers.InsertAsync(new Teacher
                    {
                        FullName = s.FullName,
                        TeacherType = s.TeacherType,
                        JobTitle = s.JobTitle,
                        Phone = s.Phone,
                        ContactUrl = s.ContactUrl,
                        ContactNote = s.ContactNote,
                        PrimarySubject = s.PrimarySubject,
                        RoomId = s.RoomId,
                        MaxLoadHours = s.MaxLoadHours,
                        WorksWithFirstGrade = s.WorksWithFirstGrade,
                        PreferredClassIds = s.PreferredClassIds.ToList(),
                        HomeroomClassId = s.HomeroomClassId,
                        HomeroomClass = s.HomeroomClass
                    });
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            await FinishDeleteAsync(targets.Count, "Сотрудник удалён");
        }
        catch (Exception ex) { ShowDeleteError(ex); }
    }

    private async Task DeleteTeacherCoreAsync(Teacher item)
    {
        try
        {
            var snap = CopyTeacher(item);
            await _teachers.DeleteAsync(item.Id);
            if (SelectedTeacher?.Id == item.Id)
                SelectedTeacher = null;
            _undo.Push(async () =>
            {
                await _teachers.InsertAsync(new Teacher
                {
                    FullName = snap.FullName,
                    TeacherType = snap.TeacherType,
                    JobTitle = snap.JobTitle,
                    Phone = snap.Phone,
                    ContactUrl = snap.ContactUrl,
                    ContactNote = snap.ContactNote,
                    PrimarySubject = snap.PrimarySubject,
                    RoomId = snap.RoomId,
                    MaxLoadHours = snap.MaxLoadHours,
                    WorksWithFirstGrade = snap.WorksWithFirstGrade,
                    PreferredClassIds = snap.PreferredClassIds.ToList(),
                    HomeroomClassId = snap.HomeroomClassId,
                    HomeroomClass = snap.HomeroomClass
                });
                _saveState.MarkDirty();
                await ReloadAfterMutationAsync();
            });
            await FinishDeleteAsync(1, "Сотрудник удалён");
        }
        catch (Exception ex) { ShowDeleteError(ex); }
    }

    private static Teacher CopyTeacher(Teacher item) => new()
    {
        Id = item.Id,
        FullName = item.FullName,
        TeacherType = item.TeacherType,
        JobTitle = item.JobTitle,
        Phone = item.Phone,
        ContactUrl = item.ContactUrl,
        ContactNote = item.ContactNote,
        PrimarySubject = item.PrimarySubject,
        SecondarySubjects = item.SecondarySubjects.ToList(),
        RoomId = item.RoomId,
        MaxLoadHours = item.MaxLoadHours,
        WorksWithFirstGrade = item.WorksWithFirstGrade,
        PreferredClassIds = item.PreferredClassIds.ToList(),
        HomeroomClassId = item.HomeroomClassId,
        HomeroomClass = item.HomeroomClass,
        CurriculumAssignments = item.CurriculumAssignments
            .Select(x => new TeacherCurriculumAssignment { CurriculumId = x.CurriculumId })
            .ToList()
    };

    private bool ConfirmDelete(string label, DeleteEntityKind kind) =>
        _dialogs.ConfirmDelete(label, kind);

    private void ShowDeleteError(Exception ex) =>
        _dialogs.ShowWarning(
            "Не удалось удалить",
            "Возможно, запись используется в расписании или нагрузке.\n\n" + ex.Message);

    private async Task AddTeacherAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTeacherName))
            return;
        var name = ProperNameFormatter.FormatPersonName(NewTeacherName);
        UpdateTeacherDuplicate(name);
        if (IsNewTeacherDuplicate)
        {
            StatusMessage = NewTeacherDuplicateHint;
            return;
        }
        await _teachers.InsertAsync(new Teacher
        {
            FullName = name,
            TeacherType = NewTeacherType
        });
        NewTeacherName = "";
        NewTeacherSuggestionHint = "";
        NewTeacherDuplicateHint = "";
        IsNewTeacherDuplicate = false;
        ClearDuplicateHighlights(TeacherList);
        _saveState.MarkDirty();
        await ReloadAfterMutationAsync();
    }

    private async Task LoadTeacherDetailsAsync()
    {
        StatusPeriods.Clear();
        Unavailabilities.Clear();
        TeacherBuildingDays.Clear();
        ClassPreferenceOptions.Clear();
        SecondarySubjectOptions.Clear();
        CurriculumAssignmentOptions.Clear();
        if (SelectedTeacher is null)
            return;

        await _teachers.RefreshCurriculumAssignmentsAsync(SelectedTeacher);

        EditTeacherName = SelectedTeacher.FullName;
        EditTeacherDuplicateHint = "";
        EditTeacherSuggestionHint = "";
        IsEditTeacherDuplicate = false;
        ClearDuplicateHighlights(TeacherList);
        EditJobTitle = SelectedTeacher.JobTitle ?? "";
        EditTeacherType = SelectedTeacher.TeacherType;
        EditPhone = SelectedTeacher.Phone ?? "";
        EditContactUrl = SelectedTeacher.ContactUrl ?? "";
        EditContactNote = SelectedTeacher.ContactNote ?? "";
        EditPrimarySubjectItem = string.IsNullOrWhiteSpace(SelectedTeacher.PrimarySubject)
            ? null
            : SubjectList.FirstOrDefault(s =>
                s.Name.Equals(SelectedTeacher.PrimarySubject, StringComparison.OrdinalIgnoreCase));
        EditWorksWithFirstGrade = SelectedTeacher.WorksWithFirstGrade;
        EditHomeroomClass = SelectedTeacher.HomeroomClassId is int homeroomId
            ? ClassList.FirstOrDefault(c => c.Id == homeroomId)
            : null;

        ClassPreferenceOptions.Clear();
        var preferred = SelectedTeacher.PreferredClassIds.ToHashSet();
        foreach (var cls in ClassList.OrderBy(c => c.Grade).ThenBy(c => c.Letter))
        {
            var option = new ClassPreferenceItem
            {
                ClassId = cls.Id,
                DisplayName = cls.DisplayName,
                IsSelected = preferred.Contains(cls.Id)
            };
            option.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ClassPreferenceItem.IsSelected)
                    && !_suppressCurriculumProfileSync)
                    RefreshCurriculumAssignmentOptions();
            };
            ClassPreferenceOptions.Add(option);
        }

        foreach (var s in await _statuses.GetForTeacherAsync(SelectedTeacher.Id))
            StatusPeriods.Add(s);
        foreach (var u in await _unavailability.GetForTeacherAsync(SelectedTeacher.Id))
            Unavailabilities.Add(u);
        OnPropertyChanged(nameof(UnavailabilityListCaption));

        foreach (var d in await _teacherBuildingDays.GetForTeacherAsync(SelectedTeacher.Id))
            TeacherBuildingDays.Add(d);
        OnPropertyChanged(nameof(TeacherBuildingDayListCaption));

        RefreshSecondarySubjectOptions();
        RefreshCurriculumAssignmentOptions();
    }

    private void RefreshCurriculumAssignmentOptions()
    {
        if (SelectedTeacher is null)
        {
            CurriculumAssignmentOptions.Clear();
            CurriculumAssignmentSections.Clear();
            return;
        }

        var assigned = CollectCurriculumSelectionSnapshot();

        _suppressCurriculumProfileSync = true;
        try
        {
            CurriculumAssignmentOptions.Clear();
            CurriculumAssignmentSections.Clear();

        var teacherSubjects = GetTeacherProfileSubjectNames();
        var hasProfile = teacherSubjects.Count > 0;
        var preferredClassIds = GetSelectedPreferredClassIds();
        var hasPreferredClasses = preferredClassIds.Count > 0;
        var othersMap = BuildCurriculumOtherAssigneesMap(SelectedTeacher.Id);

        var profilePreferred = new List<CurriculumItem>();
        var profileOther = new List<CurriculumItem>();
        var classOnly = new List<CurriculumItem>();
        var other = new List<CurriculumItem>();
        foreach (var item in CurriculumList)
        {
            var matchesProfile = hasProfile && MatchesTeacherSubject(item.SubjectName, teacherSubjects);
            var matchesPreferred = hasPreferredClasses && preferredClassIds.Contains(item.ClassId);

            if (matchesProfile && matchesPreferred)
                profilePreferred.Add(item);
            else if (matchesProfile)
                profileOther.Add(item);
            else if (matchesPreferred)
                classOnly.Add(item);
            else
                other.Add(item);
        }

        var hasAnySection = false;

        if (profilePreferred.Count > 0)
        {
            AddCurriculumAssignmentSection(
                BuildPreferredClassesSectionTitle(preferredClassIds),
                profilePreferred,
                assigned,
                othersMap,
                sort: items => items
                    .OrderBy(c => GetCurriculumAssignmentPriority(c, assigned, othersMap))
                    .ThenBy(c => GetPreferredClassSortRank(c.ClassId, preferredClassIds))
                    .ThenBy(c => GetTeacherSubjectSortRank(c.SubjectName))
                    .ThenBy(c => c.WeekParity, StringComparer.OrdinalIgnoreCase));
            hasAnySection = true;
        }

        if (profileOther.Count > 0)
        {
            AddCurriculumAssignmentSection(
                BuildTeacherSubjectsSectionTitle(teacherSubjects),
                profileOther,
                assigned,
                othersMap,
                sort: items => items
                    .OrderBy(c => GetCurriculumAssignmentPriority(c, assigned, othersMap))
                    .ThenBy(c => GetTeacherSubjectSortRank(c.SubjectName))
                    .ThenBy(c => c.ClassGrade)
                    .ThenBy(c => c.ClassName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(c => c.WeekParity, StringComparer.OrdinalIgnoreCase));
            hasAnySection = true;
        }

        if (classOnly.Count > 0)
        {
            var title = profilePreferred.Count > 0
                ? $"{BuildPreferredClassesSectionTitle(preferredClassIds)} · другие предметы"
                : BuildPreferredClassesSectionTitle(preferredClassIds);
            AddCurriculumAssignmentSection(
                title,
                classOnly,
                assigned,
                othersMap,
                sort: items => items
                    .OrderBy(c => GetCurriculumAssignmentPriority(c, assigned, othersMap))
                    .ThenBy(c => GetPreferredClassSortRank(c.ClassId, preferredClassIds))
                    .ThenBy(c => c.SubjectName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(c => c.WeekParity, StringComparer.OrdinalIgnoreCase));
            hasAnySection = true;
        }

        if (other.Count > 0)
        {
            AddCurriculumAssignmentSection(
                hasAnySection ? "Остальные" : "",
                other,
                assigned,
                othersMap,
                sort: items => items
                    .OrderBy(c => GetCurriculumAssignmentPriority(c, assigned, othersMap))
                    .ThenBy(c => c.ClassGrade)
                    .ThenBy(c => c.ClassName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(c => c.SubjectName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(c => c.WeekParity, StringComparer.OrdinalIgnoreCase));
        }
        }
        finally
        {
            _suppressCurriculumProfileSync = false;
        }

        NotifyTeacherCurriculumTotalChanged();
        _curriculumUiTeacherId = SelectedTeacher.Id;
    }

    private static int GetCurriculumAssignmentPriority(
        CurriculumItem item,
        HashSet<int> assigned,
        Dictionary<int, List<string>> othersMap)
    {
        if (assigned.Contains(item.Id))
            return 0;
        if (othersMap.TryGetValue(item.Id, out var names) && names.Count > 0)
            return 2;
        return 1;
    }

    private void AddCurriculumAssignmentSection(
        string title,
        List<CurriculumItem> items,
        HashSet<int> assigned,
        Dictionary<int, List<string>> othersMap,
        Func<IEnumerable<CurriculumItem>, IOrderedEnumerable<CurriculumItem>> sort)
    {
        var section = new CurriculumAssignmentSection { Title = title };
        foreach (var item in sort(items))
            AddCurriculumAssignmentOption(section.Items, item, assigned, othersMap);
        CurriculumAssignmentSections.Add(section);
    }

    private List<int> GetSelectedPreferredClassIds()
    {
        if (ClassPreferenceOptions.Count > 0)
        {
            return ClassPreferenceOptions
                .Where(o => o.IsSelected)
                .Select(o => o.ClassId)
                .ToList();
        }

        return SelectedTeacher?.PreferredClassIds.ToList() ?? [];
    }

    private string BuildPreferredClassesSectionTitle(IReadOnlyList<int> preferredClassIds)
    {
        var names = preferredClassIds
            .Select(id => ClassList.FirstOrDefault(c => c.Id == id)?.DisplayName ?? "")
            .Where(n => n.Length > 0)
            .ToList();
        return names.Count == 0
            ? "Классы педагога"
            : $"Классы · {string.Join(", ", names)}";
    }

    private static int GetPreferredClassSortRank(int classId, IReadOnlyList<int> preferredClassIds)
    {
        for (var i = 0; i < preferredClassIds.Count; i++)
        {
            if (preferredClassIds[i] == classId)
                return i;
        }

        return 1000;
    }

    private Dictionary<int, List<string>> BuildCurriculumOtherAssigneesMap(int currentTeacherId)
    {
        var map = new Dictionary<int, List<string>>();
        foreach (var teacher in TeacherList)
        {
            if (teacher.Id == currentTeacherId)
                continue;

            foreach (var assignment in teacher.CurriculumAssignments)
            {
                if (!map.TryGetValue(assignment.CurriculumId, out var names))
                {
                    names = [];
                    map[assignment.CurriculumId] = names;
                }

                if (!names.Contains(teacher.FullName))
                    names.Add(teacher.FullName);
            }
        }

        foreach (var names in map.Values)
            names.Sort(StringComparer.OrdinalIgnoreCase);

        return map;
    }

    private void AddCurriculumAssignmentOption(
        ICollection<CurriculumPreferenceItem> target,
        CurriculumItem item,
        HashSet<int> assigned,
        Dictionary<int, List<string>> othersMap)
    {
        othersMap.TryGetValue(item.Id, out var otherTeachers);
        var hoursLine = $"{FormatCurriculumHours(item.HoursPerWeek)} ч/нед";
        var option = new CurriculumPreferenceItem
        {
            CurriculumId = item.Id,
            DisplayName = item.PaletteLabel,
            DetailsLine = item.HasSubgroups ? $"п/г · {hoursLine}" : hoursLine,
            HoursPerWeek = item.HoursPerWeek,
            HasSubgroups = item.HasSubgroups,
            OtherTeacherNames = otherTeachers ?? [],
            IsSelected = assigned.Contains(item.Id)
        };
        option.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CurriculumPreferenceItem.IsSelected))
                OnCurriculumAssignmentSelectionChanged(option, option.IsSelected);
        };
        target.Add(option);
    }

    private HashSet<string> GetTeacherProfileSubjectNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var primary = EditPrimarySubjectItem?.Name ?? SelectedTeacher?.PrimarySubject;
        if (!string.IsNullOrWhiteSpace(primary))
            names.Add(primary.Trim());
        foreach (var subject in SecondarySubjectOptions.Where(o => o.IsSelected))
            names.Add(subject.DisplayName);
        if (!SecondarySubjectOptions.Any(o => o.IsSelected)
            && SelectedTeacher?.SecondarySubjects is { Count: > 0 } saved)
        {
            foreach (var name in saved)
                names.Add(name);
        }
        return names;
    }

    private string BuildTeacherSubjectsSectionTitle(HashSet<string> teacherSubjects)
    {
        var parts = new List<string>();
        var primary = EditPrimarySubjectItem?.Name ?? SelectedTeacher?.PrimarySubject;
        if (!string.IsNullOrWhiteSpace(primary))
            parts.Add(primary.Trim());
        foreach (var subject in SecondarySubjectOptions
                     .Where(o => o.IsSelected)
                     .OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase))
            parts.Add(subject.DisplayName);
        return parts.Count == 0
            ? string.Join(" · ", teacherSubjects.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            : string.Join(" · ", parts);
    }

    private int GetTeacherSubjectSortRank(string subjectName)
    {
        var primary = EditPrimarySubjectItem?.Name ?? SelectedTeacher?.PrimarySubject;
        if (!string.IsNullOrWhiteSpace(primary)
            && subjectName.Equals(primary.Trim(), StringComparison.OrdinalIgnoreCase))
            return 0;

        var index = 0;
        foreach (var subject in SecondarySubjectOptions.Where(o => o.IsSelected))
        {
            index++;
            if (subjectName.Equals(subject.DisplayName, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return 100;
    }

    private static bool MatchesTeacherSubject(string subjectName, HashSet<string> teacherSubjects) =>
        teacherSubjects.Contains(subjectName);

    private void NotifyTeacherCurriculumTotalChanged()
    {
        OnPropertyChanged(nameof(TeacherCurriculumTotalHours));
        OnPropertyChanged(nameof(TeacherCurriculumTotalDisplay));
    }

    private static string FormatCurriculumHours(double hours) =>
        Math.Abs(hours - Math.Round(hours)) < 0.01 ? $"{hours:0}" : $"{hours:0.#}";

    private async Task SaveTeacherAsync()
    {
        if (SelectedTeacher is null)
            return;
        if (string.IsNullOrWhiteSpace(EditTeacherName))
        {
            StatusMessage = "Укажите ФИО сотрудника";
            return;
        }

        var name = ProperNameFormatter.FormatPersonName(EditTeacherName);
        UpdateEditTeacherDuplicate(name);
        if (IsEditTeacherDuplicate)
        {
            StatusMessage = EditTeacherDuplicateHint;
            return;
        }

        var before = CopyTeacher(SelectedTeacher);
        SelectedTeacher.FullName = name;
        SelectedTeacher.JobTitle = string.IsNullOrWhiteSpace(EditJobTitle) ? null : ProperNameFormatter.FormatTitle(EditJobTitle);
        SelectedTeacher.TeacherType = EditTeacherType;
        SelectedTeacher.Phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone.Trim();
        SelectedTeacher.ContactUrl = string.IsNullOrWhiteSpace(EditContactUrl) ? null : EditContactUrl.Trim();
        SelectedTeacher.ContactNote = string.IsNullOrWhiteSpace(EditContactNote) ? null : EditContactNote.Trim();
        SelectedTeacher.PrimarySubject = EditPrimarySubjectItem?.Name;
        SelectedTeacher.SecondarySubjects = SecondarySubjectOptions
            .Where(o => o.IsSelected)
            .Select(o => o.DisplayName)
            .ToList();
        SelectedTeacher.WorksWithFirstGrade = EditWorksWithFirstGrade;
        SelectedTeacher.HomeroomClassId = EditHomeroomClass?.Id;
        SelectedTeacher.HomeroomClass = EditHomeroomClass?.DisplayName;
        SelectedTeacher.PreferredClassIds = ClassPreferenceOptions
            .Where(o => o.IsSelected)
            .Select(o => o.ClassId)
            .ToList();
        var id = SelectedTeacher.Id;
        await WaitPendingCurriculumGridSavesAsync();
        await _teachers.UpdateAsync(SelectedTeacher);
        _undo.Push(async () =>
        {
            await _teachers.UpdateAsync(before);
            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();
        });
        EditTeacherDuplicateHint = "";
        EditTeacherSuggestionHint = "";
        IsEditTeacherDuplicate = false;
        ClearDuplicateHighlights(TeacherList);
        _saveState.MarkDirty();
        await ReloadAfterMutationAsync();
        SelectedTeacher = TeacherList.FirstOrDefault(t => t.Id == id);
        StatusMessage = $"Данные сотрудника «{name}» сохранены";
    }

    private async Task AddStatusPeriodAsync()
    {
        if (SelectedTeacher is null || NewStatusStartDate is null)
            return;

        if (!NewStatusOpenEnded)
        {
            if (NewStatusEndDate is null)
            {
                StatusMessage = "Укажите дату окончания или отметьте «без даты окончания»";
                return;
            }

            if (NewStatusEndDate.Value.Date < NewStatusStartDate.Value.Date)
            {
                StatusMessage = "Дата окончания не может быть раньше начала";
                return;
            }
        }

        await _absences.MarkPeriodAsync(
            SelectedTeacher.Id,
            DateOnly.FromDateTime(NewStatusStartDate.Value),
            NewStatusOpenEnded ? null : DateOnly.FromDateTime(NewStatusEndDate!.Value),
            NewStatusType,
            string.IsNullOrWhiteSpace(NewStatusNote) ? null : NewStatusNote.Trim(),
            NewStatusIsOfficial,
            AbsenceSources.Profile);
        _saveState.MarkDirty();
        await LoadTeacherDetailsAsync();
        StatusMessage = "Статус добавлен";
    }

    private async Task RemoveStatusPeriodAsync(TeacherStatusPeriod? period)
    {
        if (period is null)
            return;
        await _statuses.DeleteAsync(period.Id);
        _saveState.MarkDirty();
        await LoadTeacherDetailsAsync();
    }

    private async Task AddUnavailabilityAsync()
    {
        if (SelectedTeacher is null || NewUnavailStartDate is null)
            return;

        if (NewUnavailEndDate is DateTime endDate &&
            endDate.Date < NewUnavailStartDate.Value.Date)
        {
            StatusMessage = "Дата окончания не может быть раньше начала";
            return;
        }

        int? lessonFrom = int.TryParse(NewUnavailLessonFrom, out var lf) ? lf : null;
        int? lessonTo = int.TryParse(NewUnavailLessonTo, out var lt) ? lt : null;

        await _unavailability.InsertAsync(new TeacherUnavailability
        {
            TeacherId = SelectedTeacher.Id,
            RecurrenceType = NewUnavailRecurrence,
            DayOfWeek = NewUnavailRecurrence == UnavailabilityRecurrence.Weekly ? NewUnavailDayIndex + 1 : null,
            StartDate = FormatPickerDate(NewUnavailStartDate),
            EndDate = NewUnavailEndDate is null ? null : FormatPickerDate(NewUnavailEndDate),
            AllDay = NewUnavailAllDay,
            LessonFrom = NewUnavailAllDay ? null : lessonFrom,
            LessonTo = NewUnavailAllDay ? null : lessonTo,
            Note = string.IsNullOrWhiteSpace(NewUnavailNote) ? null : NewUnavailNote.Trim()
        });
        _saveState.MarkDirty();
        await LoadTeacherDetailsAsync();
        OnPropertyChanged(nameof(UnavailabilityListCaption));
        StatusMessage = "Нерабочее время добавлено";
    }

    private static string FormatPickerDate(DateTime? date) =>
        date?.ToString("yyyy-MM-dd") ?? "";

    private async Task RemoveUnavailabilityAsync(TeacherUnavailability? item)
    {
        if (item is null)
            return;
        await _unavailability.DeleteAsync(item.Id);
        _saveState.MarkDirty();
        await LoadTeacherDetailsAsync();
        OnPropertyChanged(nameof(UnavailabilityListCaption));
    }

    private async Task AddTeacherBuildingDayAsync()
    {
        if (SelectedTeacher is null)
            return;
        if (NewTeacherBuildingDayBuilding is null)
        {
            StatusMessage = "Выберите здание для дня недели";
            return;
        }

        var day = NewTeacherBuildingDayIndex + 1;
        var existing = TeacherBuildingDays.FirstOrDefault(d => d.DayOfWeek == day);
        if (existing is not null)
        {
            await _teacherBuildingDays.DeleteAsync(existing.Id);
            TeacherBuildingDays.Remove(existing);
        }

        await _teacherBuildingDays.InsertAsync(new TeacherBuildingDay
        {
            TeacherId = SelectedTeacher.Id,
            DayOfWeek = day,
            BuildingId = NewTeacherBuildingDayBuilding.Id,
            BuildingName = NewTeacherBuildingDayBuilding.Name
        });
        _saveState.MarkDirty();
        await LoadTeacherDetailsAsync();
        StatusMessage = "Здание по дню недели сохранено";
    }

    private async Task RemoveTeacherBuildingDayAsync(TeacherBuildingDay? item)
    {
        if (item is null)
            return;
        await _teacherBuildingDays.DeleteAsync(item.Id);
        _saveState.MarkDirty();
        await LoadTeacherDetailsAsync();
        StatusMessage = "Запись удалена";
    }

    private async Task ExportAppDataAsync()
    {
        var dlg = new SaveFileDialog
        {
            Filter = $"Архив {AppBranding.ProductName} (*{AppDataTransferService.FileExtension})|*{AppDataTransferService.FileExtension}",
            FileName = AppDataTransferService.SuggestFileName(_settings.SchoolName),
            Title = "Выгрузить все данные"
        };
        if (dlg.ShowDialog() != true)
            return;

        var result = await _appDataTransfer.ExportAsync(dlg.FileName);
        if (result.Success)
        {
            StatusMessage = result.Message;
            _dialogs.ShowSuccess("Выгрузка данных", result.Message);
        }
        else
        {
            StatusMessage = result.Message;
            _dialogs.ShowError("Выгрузка данных", result.Message);
        }
    }

    private async Task ImportAppDataAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = $"Архив {AppBranding.ProductName} (*{AppDataTransferService.FileExtension})|*{AppDataTransferService.FileExtension}",
            Title = "Загрузить все данные"
        };
        if (dlg.ShowDialog() != true)
            return;

        var manifest = await _appDataTransfer.ReadManifestAsync(dlg.FileName);
        if (manifest is null)
        {
            _dialogs.ShowError("Загрузка данных", $"Не удалось прочитать архив. Проверьте, что это файл выгрузки {AppBranding.ProductName}.");
            return;
        }

        if (!_dialogs.ConfirmImportAppData(manifest))
            return;

        var result = await _appDataTransfer.ImportAsync(dlg.FileName);
        if (!result.Success)
        {
            StatusMessage = result.Message;
            _dialogs.ShowError("Загрузка данных", result.Message);
            return;
        }

        _undo.Clear();
        ResetEditorForms();
        await ReloadAfterMutationAsync();
        StatusMessage = result.Message;
        _dialogs.ShowSuccess("Загрузка данных", result.Message);
    }

    private async Task SaveSettingsAsync()
    {
        await _settings.SaveSchoolNameAsync(SchoolName);
        StatusMessage = "Название школы сохранено";
    }

    private async Task ClearAllDataAsync()
    {
        if (!_dialogs.ConfirmClearAllData())
            return;

        try
        {
            await _databaseClear.ClearAllAsync();
            _undo.Clear();
            ResetEditorForms();
            await ReloadAfterMutationAsync();
            _dialogs.ShowSuccess("Готово", "Все данные удалены. Встроенные шаблоны звонков и нагрузки сохранены — можно заполнять справочники заново.");
            StatusMessage = "Все данные удалены";
        }
        catch (Exception ex)
        {
            StatusMessage = "Не удалось очистить данные";
            _dialogs.ShowError("Ошибка", ex.Message);
        }
    }

    private async Task ClearDirectorySectionAsync(DirectoryClearSection section)
    {
        try
        {
            if (section == DirectoryClearSection.Curriculum)
            {
                var mode = _dialogs.AskCurriculumClearMode();
                if (mode is null)
                    return;

                await _databaseClear.ClearCurriculumAsync(mode.Value);
                _undo.Clear();
                ResetEditorForms();
                await ReloadAfterMutationAsync();

                var message = mode.Value == CurriculumClearMode.TeacherAssignmentsOnly
                    ? "Назначения педагогов по нагрузке сброшены. Часы по классам сохранены."
                    : "Раздел «Нагрузка» очищен.";
                _dialogs.ShowSuccess("Готово", message);
                StatusMessage = mode.Value == CurriculumClearMode.TeacherAssignmentsOnly
                    ? "Назначения педагогов сброшены"
                    : "Раздел «Нагрузка» очищен";
                return;
            }

            if (!_dialogs.ConfirmClearDirectorySection(section))
                return;

            await _databaseClear.ClearSectionAsync(section);
            _undo.Clear();
            ResetEditorForms();
            await ReloadAfterMutationAsync();

            var tabName = DirectoryClearOptions.First(o => o.Section == section).TabName;
            _dialogs.ShowSuccess("Готово", $"Раздел «{tabName}» очищен.");
            StatusMessage = $"Раздел «{tabName}» очищен";
        }
        catch (Exception ex)
        {
            StatusMessage = "Не удалось очистить раздел";
            _dialogs.ShowError("Ошибка", ex.Message);
        }
    }

    private void ResetEditorForms()
    {
        SelectedBuilding = null;
        SelectedSubject = null;
        SelectedClass = null;
        SelectedTeacher = null;
        SelectedRoom = null;
        SelectedCurriculumItem = null;
        SelectedBell = null;
        NewBuildingName = "";
        NewBuildingColor = "#2563EB";
        NewSubjectName = "";
        ResetSubjectDifficultyAutoFill();
        StatusPeriods.Clear();
        Unavailabilities.Clear();
        TeacherBuildingDays.Clear();
        ClassPreferenceOptions.Clear();
    }

    private void RefreshSubjectFilter()
    {
        FilteredSubjectList.Clear();
        var q = SubjectFilterText.Trim();
        var source = string.IsNullOrEmpty(q)
            ? SubjectList
            : SubjectList.Where(s => s.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        foreach (var s in source.OrderBy(s => s.Name))
            FilteredSubjectList.Add(s);
    }

    private void RefreshTeacherFilter()
    {
        FilteredTeacherList.Clear();
        var q = TeacherFilterText.Trim();
        var source = string.IsNullOrEmpty(q)
            ? TeacherList
            : TeacherList.Where(t => TeacherMatchesFilter(t, q));
        foreach (var t in source.OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase))
            FilteredTeacherList.Add(t);
    }

    private static bool TeacherMatchesFilter(Teacher teacher, string query) =>
        teacher.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || teacher.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part.StartsWith(query, StringComparison.OrdinalIgnoreCase));

    private void ClearTeacherFilter() => TeacherFilterText = "";

    private void RefreshSecondarySubjectOptions()
    {
        SecondarySubjectOptions.Clear();
        if (SelectedTeacher is null)
            return;

        var primaryName = EditPrimarySubjectItem?.Name ?? SelectedTeacher.PrimarySubject;
        var selected = SelectedTeacher.SecondarySubjects
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var subject in SubjectList
                     .Where(s => primaryName is null
                                 || !s.Name.Equals(primaryName, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            SecondarySubjectOptions.Add(new SubjectPreferenceItem
            {
                SubjectId = subject.Id,
                DisplayName = subject.Name,
                IsSelected = selected.Contains(subject.Name)
            });
        }

        foreach (var option in SecondarySubjectOptions)
        {
            option.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SubjectPreferenceItem.IsSelected)
                    && !_suppressCurriculumProfileSync)
                    RefreshCurriculumAssignmentOptions();
            };
        }
    }

    private Subject? FindSubjectByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return SubjectList.FirstOrDefault(s => s.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? SubjectList.FirstOrDefault(s =>
                s.Name.Equals(ProperNameFormatter.FormatTitle(name.Trim()), StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshCatalog()
    {
        var selectedName = SelectedCatalogEntry?.Name;
        CatalogEntries.Clear();
        int? grade = CatalogGradeIndex == 0 ? null : CatalogGradeIndex;
        foreach (var e in _subjectCatalog.Search(CatalogSearchText, grade))
            CatalogEntries.Add(e);
        if (!string.IsNullOrEmpty(selectedName))
            RestoreCatalogSelection(selectedName);
    }

    private void UpdateNewSubjectSuggestion()
    {
        var suggestion = _subjectCatalog.SuggestSubjectName(NewSubjectName, SubjectList);
        NewSubjectSuggestionHint = suggestion?.Hint ?? "";
    }

    private static void ClearDuplicateHighlights<T>(IEnumerable<T> items) where T : SelectableEntity
    {
        foreach (var item in items)
            item.IsDuplicateHighlight = false;
    }

    private static void HighlightDuplicateMatch<T>(IEnumerable<T> items, T? match) where T : SelectableEntity
    {
        foreach (var item in items)
            item.IsDuplicateHighlight = match is not null && ReferenceEquals(item, match);
    }

    private void UpdateBellDuplicate(string? templateName = null, int? lessonNumber = null, int? shift = null, string? periodKind = null)
    {
        var template = templateName ?? NewBellTemplate.Trim();
        var kind = periodKind ?? NewBellPeriodKind;
        if (string.IsNullOrWhiteSpace(template) ||
            !int.TryParse(lessonNumber?.ToString() ?? NewBellLessonNumber, out var lesson) ||
            !int.TryParse(shift?.ToString() ?? NewBellShift, out var sh))
        {
            NewBellDuplicateHint = "";
            IsNewBellDuplicate = false;
            ClearDuplicateHighlights(BellList);
            return;
        }

        var match = DuplicateEntryChecker.FindBell(BellList, template, lesson, sh, kind, SelectedBell?.Id);
        IsNewBellDuplicate = match is not null;
        NewBellDuplicateHint = match is null
            ? ""
            : $"Такая запись уже есть в «{match.TemplateName}»";
        HighlightDuplicateMatch(BellList, match);
    }

    private void UpdateBuildingDuplicate(string? normalizedName = null)
    {
        var name = normalizedName ?? ProperNameFormatter.FormatBuildingOrAddress(NewBuildingName);
        if (string.IsNullOrWhiteSpace(NewBuildingName))
        {
            NewBuildingDuplicateHint = "";
            IsNewBuildingDuplicate = false;
            ClearDuplicateHighlights(BuildingList);
            return;
        }

        var match = DuplicateEntryChecker.FindBuilding(BuildingList, name, SelectedBuilding?.Id);
        IsNewBuildingDuplicate = match is not null;
        NewBuildingDuplicateHint = match is null ? "" : $"Здание «{match.Name}» уже есть в списке";
        HighlightDuplicateMatch(BuildingList, match);
    }

    private void UpdateSubjectDuplicate(string? normalizedName = null)
    {
        if (string.IsNullOrWhiteSpace(NewSubjectName))
        {
            NewSubjectDuplicateHint = "";
            IsNewSubjectDuplicate = false;
            ClearDuplicateHighlights(SubjectList);
            return;
        }

        var name = normalizedName ?? ProperNameFormatter.FormatTitle(NewSubjectName);
        var match = DuplicateEntryChecker.FindSubject(SubjectList, name, SelectedSubject?.Id);
        IsNewSubjectDuplicate = match is not null;
        NewSubjectDuplicateHint = match is null ? "" : $"Предмет «{match.Name}» уже есть в списке";
        HighlightDuplicateMatch(SubjectList, match);
    }

    private void UpdateClassDuplicate()
    {
        if (!int.TryParse(NewClassGrade, out var grade) || string.IsNullOrWhiteSpace(NewClassLetter))
        {
            NewClassDuplicateHint = "";
            IsNewClassDuplicate = false;
            ClearDuplicateHighlights(ClassList);
            return;
        }

        var match = DuplicateEntryChecker.FindClass(ClassList, grade, NewClassLetter, SelectedClass?.Id);
        IsNewClassDuplicate = match is not null;
        NewClassDuplicateHint = match is null ? "" : $"Класс {match.DisplayName} уже есть в списке";
        HighlightDuplicateMatch(ClassList, match);
    }

    private void UpdateRoomDuplicate()
    {
        if (string.IsNullOrWhiteSpace(NewRoomNumber) || SelectedRoomBuilding is null)
        {
            NewRoomDuplicateHint = "";
            IsNewRoomDuplicate = false;
            ClearDuplicateHighlights(RoomList);
            return;
        }

        var match = DuplicateEntryChecker.FindRoom(
            RoomList, NewRoomNumber, SelectedRoomBuilding.Id, SelectedRoom?.Id);
        IsNewRoomDuplicate = match is not null;
        NewRoomDuplicateHint = match is null
            ? ""
            : $"Кабинет {match.Number} в «{match.BuildingName}» уже есть";
        HighlightDuplicateMatch(RoomList, match);
    }

    private void UpdateCurriculumDuplicate()
    {
        if (NewCurriculumClass is null || NewCurriculumSubject is null)
        {
            NewCurriculumDuplicateHint = "";
            IsNewCurriculumDuplicate = false;
            ClearDuplicateHighlights(CurriculumList);
            return;
        }

        var match = DuplicateEntryChecker.FindCurriculum(
            CurriculumList,
            NewCurriculumClass.Id,
            NewCurriculumSubject.Id,
            NewCurriculumWeekParity,
            SelectedCurriculumItem?.Id);
        IsNewCurriculumDuplicate = match is not null;
        NewCurriculumDuplicateHint = match is null
            ? ""
            : $"Нагрузка {match.ClassName} · {match.SubjectName} ({match.WeekParityDisplay}) уже есть";
        HighlightDuplicateMatch(CurriculumList, match);
    }

    private void UpdateTeacherDuplicate(string? normalizedName = null)
    {
        if (string.IsNullOrWhiteSpace(NewTeacherName))
        {
            NewTeacherDuplicateHint = "";
            IsNewTeacherDuplicate = false;
            if (SelectedTeacher is null || string.IsNullOrWhiteSpace(EditTeacherName))
                ClearDuplicateHighlights(TeacherList);
            return;
        }

        var name = normalizedName ?? ProperNameFormatter.FormatPersonName(NewTeacherName);
        var match = DuplicateEntryChecker.FindTeacher(TeacherList, name);
        IsNewTeacherDuplicate = match is not null;
        NewTeacherDuplicateHint = match is null ? "" : $"Сотрудник «{match.FullName}» уже есть в списке";
        HighlightDuplicateMatch(TeacherList, match);
    }

    private void UpdateEditTeacherDuplicate(string? normalizedName = null)
    {
        if (SelectedTeacher is null || string.IsNullOrWhiteSpace(EditTeacherName))
        {
            EditTeacherDuplicateHint = "";
            IsEditTeacherDuplicate = false;
            if (string.IsNullOrWhiteSpace(NewTeacherName))
                ClearDuplicateHighlights(TeacherList);
            return;
        }

        var name = normalizedName ?? ProperNameFormatter.FormatPersonName(EditTeacherName);
        var match = DuplicateEntryChecker.FindTeacher(TeacherList, name, SelectedTeacher.Id);
        IsEditTeacherDuplicate = match is not null;
        EditTeacherDuplicateHint = match is null ? "" : $"Сотрудник «{match.FullName}» уже есть в списке";
        HighlightDuplicateMatch(TeacherList, match);
    }

    private void UpdateNewBuildingSuggestion()
    {
        var formatted = ProperNameFormatter.FormatBuildingOrAddress(NewBuildingName);
        NewBuildingSuggestionHint = !string.IsNullOrWhiteSpace(NewBuildingName)
                                    && !NewBuildingName.Trim().Equals(formatted, StringComparison.Ordinal)
            ? $"Оформить: «{formatted}»?"
            : "";
    }

    private void UpdateNewTeacherSuggestion()
    {
        var suggestion = _textSuggestions.SuggestPersonName(NewTeacherName, TeacherList.Select(t => t.FullName));
        NewTeacherSuggestionHint = suggestion?.Hint ?? "";
    }

    private void ApplyNewSubjectSuggestion()
    {
        var suggestion = _subjectCatalog.SuggestSubjectName(NewSubjectName, SubjectList);
        if (suggestion is null)
            return;
        NewSubjectName = suggestion.Suggested;
        UpdateNewSubjectSuggestion();
        if (!_subjectDifficultyManuallyEdited)
            ApplySuggestedSubjectDifficulty();
    }

    private void ApplyNewBuildingSuggestion()
    {
        NewBuildingName = ProperNameFormatter.FormatBuildingOrAddress(NewBuildingName);
        UpdateNewBuildingSuggestion();
    }

    private void ApplyNewTeacherSuggestion()
    {
        var suggestion = _textSuggestions.SuggestPersonName(NewTeacherName, TeacherList.Select(t => t.FullName));
        if (suggestion is null)
            return;
        NewTeacherName = suggestion.Suggested;
        UpdateNewTeacherSuggestion();
    }

    private void ApplyEditTeacherSuggestion()
    {
        if (SelectedTeacher is null)
            return;
        var others = TeacherList.Where(t => t.Id != SelectedTeacher.Id).Select(t => t.FullName);
        var suggestion = _textSuggestions.SuggestPersonName(EditTeacherName, others);
        if (suggestion is null)
            return;
        EditTeacherName = suggestion.Suggested;
    }

    private async Task ImportCatalogEntryAsync()
    {
        if (SelectedCatalogEntry is null)
        {
            StatusMessage = "Выберите предмет в типовом перечне";
            return;
        }

        var entry = SelectedCatalogEntry;
        var entryName = entry.Name;
        try
        {
            var count = await _subjectCatalog.ImportEntriesAsync([entry]);
            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();
            RestoreCatalogSelection(entryName);
            StatusMessage = count > 0
                ? $"Добавлен: {entryName}"
                : $"«{entryName}» уже в справочнике";
        }
        catch (Exception ex)
        {
            ShowDeleteError(ex);
            StatusMessage = $"Не удалось добавить «{entryName}»";
        }
    }

    private async Task ImportCatalogForGradeAsync()
    {
        if (CatalogGradeIndex == 0)
        {
            StatusMessage = "Выберите класс (1–11) для пакетного добавления";
            return;
        }
        try
        {
            var grade = CatalogGradeIndex;
            var count = await _subjectCatalog.ImportForGradeAsync(grade);
            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();
            StatusMessage = $"Добавлено предметов для {grade} класса: {count}";
        }
        catch (Exception ex)
        {
            ShowDeleteError(ex);
            StatusMessage = "Не удалось добавить предметы для класса";
        }
    }

    private List<Subject> GetRefreshScoreTargets()
    {
        var checkedItems = SubjectList.Where(s => s.IsSelected).ToList();
        if (checkedItems.Count > 0)
            return checkedItems;
        if (SelectedSubject is not null)
            return [SelectedSubject];
        return SubjectList.ToList();
    }

    private async Task RefreshSubjectDifficultiesAsync()
    {
        var targets = GetRefreshScoreTargets();
        if (targets.Count == 0)
        {
            StatusMessage = "Справочник предметов пуст";
            return;
        }

        var scope = SubjectList.Any(s => s.IsSelected)
            ? $"отмеченных предметов ({targets.Count})"
            : SelectedSubject is not null
                ? $"«{SelectedSubject.Name}»"
                : $"всех предметов ({targets.Count})";
        var gradeHint = CatalogGradeIndex > 0
            ? $"Параллель для расчёта: {CatalogGradeIndex} класс."
            : "Параллель не выбрана — для каждого предмета возьмётся ориентир из типового перечня (или 5 класс).";

        if (!_dialogs.ConfirmProceed(
                "Обновить баллы Сивкова",
                $"Пересчитать баллы для {scope} по {OfficialSubjectDifficultyReference.SourceLabelDativeVariants}?\n\n" +
                $"{gradeHint}\n\n" +
                "Названия не изменятся. В расписании балл всё равно считается по параллели класса."))
            return;

        try
        {
            var snapshots = targets
                .Select(s => new Subject { Id = s.Id, Name = s.Name, DifficultyScore = s.DifficultyScore })
                .ToList();
            var referenceGrade = CatalogGradeIndex > 0 ? CatalogGradeIndex : (int?)null;
            var result = await _subjectCatalog.RefreshDifficultiesAsync(targets, referenceGrade);

            if (result.UpdatedCount > 0)
            {
                var changedIds = result.Updated.Select(c => c.SubjectId).ToHashSet();
                var undoSnaps = snapshots.Where(s => changedIds.Contains(s.Id)).ToList();
                _undo.Push(async () =>
                {
                    foreach (var snap in undoSnaps)
                    {
                        var item = await _subjects.GetByIdAsync(snap.Id);
                        if (item is null)
                            continue;
                        item.DifficultyScore = snap.DifficultyScore;
                        await _subjects.UpdateAsync(item);
                    }
                    _saveState.MarkDirty();
                    await ReloadAfterMutationAsync();
                });
            }

            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();

            if (SelectedSubject is not null)
                SelectedSubject = SubjectList.FirstOrDefault(s => s.Id == SelectedSubject.Id);

            StatusMessage = result.UpdatedCount > 0
                ? $"Баллы обновлены: {result.UpdatedCount}. Без изменений: {result.Unchanged}. Не распознано: {result.Unmatched}."
                : result.Unmatched > 0
                    ? $"Нечего менять. Без изменений: {result.Unchanged}. Не распознано: {result.Unmatched}."
                    : "Все баллы уже соответствуют справочнику.";

            if (result.UpdatedCount > 0 && result.Unmatched > 0)
                _dialogs.ShowInfo("Обновление баллов",
                    $"Обновлено: {result.UpdatedCount}\n" +
                    $"Без изменений: {result.Unchanged}\n" +
                    $"Не распознано (балл не менялся): {result.Unmatched}");
        }
        catch (Exception ex)
        {
            ShowDeleteError(ex);
            StatusMessage = "Не удалось обновить баллы";
        }
    }

    private void RestoreCatalogSelection(string entryName) =>
        SelectedCatalogEntry = CatalogEntries.FirstOrDefault(e =>
            e.Name.Equals(entryName, StringComparison.OrdinalIgnoreCase));
}
