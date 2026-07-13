using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using ArmZavuch.Data;
using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Catalog;
using ArmZavuch.Services.Data;
using ArmZavuch.Services.Dialog;
using ArmZavuch.Services.Navigation;
using ArmZavuch.Services.Save;
using ArmZavuch.Services.Schedule;
using ArmZavuch.Services.Staff;
using ArmZavuch.Services.Text;
using ArmZavuch.Services.Undo;
using ArmZavuch.Services.Validation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;

namespace ArmZavuch.ViewModels;

public partial class ConstructorViewModel : ObservableObject
{
	private readonly List<ComplianceIssue> _allComplianceIssues = new List<ComplianceIssue>();

	private readonly HashSet<string> _dismissedComplianceKeys = new HashSet<string>(StringComparer.Ordinal);

	private const int MaxLessons = 8;

	private readonly WeekTemplateRepository _templates;

	private readonly SchoolClassRepository _classes;

	private readonly SubjectRepository _subjects;

	private readonly TeacherRepository _teachers;

	private readonly RoomRepository _rooms;

	private readonly SchedulePeriodRepository _periods;

	private readonly CalendarRepository _calendar;

	private readonly CurriculumRepository _curriculum;

	private readonly LoadBalanceChecker _loadBalance;

	private readonly ScheduleComplianceChecker _compliance;

	private readonly BellRepository _bells;

	private readonly BellTemplateAssignmentService _bellAssignment;

	private readonly ScheduleConflictDetector _conflictDetector;

	private readonly BuildingTransitionChecker _transitionChecker;

	private readonly TeacherAvailabilityService _availability;

	private readonly TeacherClassPreferenceSyncService _teacherClassSync;

	private readonly TeacherCurriculumSyncService _teacherCurriculumSync;


	private readonly SubjectCatalogService _subjectCatalog;

	private readonly ISaveStateService _saveState;

	private readonly CrudUndoService _undo;

	private readonly IAppDialogService _dialogs;

	private readonly IAppDataRevisionService _revision;

	private readonly IModuleNavigationService _navigation;

	private bool _isLoaded;

	private long _loadedReferenceRevision = -1L;

	private long _loadedScheduleRevision = -1L;

	private readonly HashSet<string> _collapsedGridSections = new HashSet<string>(StringComparer.Ordinal);

	private int _editPanelSyncSuppressDepth;

	private bool _preparingDropEdit;

	private bool _suppressFocusClassRoomSync;

	private bool _suppressFocusClassPeRoomSync;

	private bool _suppressPeriodNameAutofill;

	private bool _isReloadingReferenceData;

	private bool _resettingSubgroupForCellChange;

	private readonly ConstructorDragHintService _dragHints;

	private readonly TeacherUnavailabilityRepository _unavailabilityRepo;

	private List<LessonSlot> _templateSlotsCache = new List<LessonSlot>();

	private IReadOnlyDictionary<(string From, string To), int>? _routeMapCache;

	private int? _dragPrefetchTeacherId;

	private List<TeacherUnavailability>? _dragPrefetchUnavailability;

	private List<BellPeriod> _bellPeriods = new List<BellPeriod>();

	private Dictionary<int, double> _plannedHoursByTeacher = new Dictionary<int, double>();

	[ObservableProperty]
	private bool _isDragHintActive;

	[ObservableProperty]
	private WeekTemplateInfo? _selectedTemplate;

	[ObservableProperty]
	private int _selectedDayIndex;

	[ObservableProperty]
	private GridCell? _selectedCell;

	[ObservableProperty]
	private Subject? _editSubject;

	[ObservableProperty]
	private string _subjectSearchText = "";

	[ObservableProperty]
	private string _subjectSuggestionHint = "";

	[ObservableProperty]
	private Teacher? _editTeacher;

	[ObservableProperty]
	private TeacherPickerItem? _selectedTeacherPickerItem;

	[ObservableProperty]
	private Room? _editRoom;

	[ObservableProperty]
	private string _periodName = PeriodTypes.ToDisplay("Quarter");

	[ObservableProperty]
	private DateTime? _periodStart = new DateTime(2025, 9, 1);

	[ObservableProperty]
	private DateTime? _periodEnd = new DateTime(2025, 10, 31);

	[ObservableProperty]
	private string _selectedPeriodType = "Quarter";

	[ObservableProperty]
	private string _selectedRecurrence = "EveryOtherWeek";

	[ObservableProperty]
	private DateTime? _calStartDate = DateTime.Today;

	[ObservableProperty]
	private DateTime? _calEndDate;

	[ObservableProperty]
	private string _selectedCalType = "Holiday";

	[ObservableProperty]
	private string _calNote = "";

	[ObservableProperty]
	private int _calDonorDay = 1;

	[ObservableProperty]
	private int _selectedSubgroupIndex;

	[ObservableProperty]
	private string _complianceSummary = "";

	[ObservableProperty]
	private int _complianceErrorCount;

	[ObservableProperty]
	private int _complianceWarningCount;

	[ObservableProperty]
	private int _complianceInfoCount;

	[ObservableProperty]
	private bool _hasComplianceFindings;

	[ObservableProperty]
	private string _complianceActionMessage = "";

	[ObservableProperty]
	private bool _complianceShowErrors = true;

	[ObservableProperty]
	private bool _complianceShowWarnings = true;

	[ObservableProperty]
	private bool _complianceShowInfo;

	[ObservableProperty]
	private SchoolClass? _complianceFilterClass;

	[ObservableProperty]
	private string _complianceSearchText = "";

	[ObservableProperty]
	private string _complianceSortMode = "Severity";

	[ObservableProperty]
	private bool _complianceShowDismissed;

	[ObservableProperty]
	private int _complianceDismissedCount;

	[ObservableProperty]
	private string _complianceFilterSummary = "";

	[ObservableProperty]
	private bool _hasDayConflicts;

	[ObservableProperty]
	private string _dayConflictSummary = "";

	[ObservableProperty]
	private bool _hasDayRoomSharedWarnings;

	[ObservableProperty]
	private string _dayRoomSharedSummary = "";

	[ObservableProperty]
	private bool _hasEditConflict;

	[ObservableProperty]
	private int _editConflictCount;

	[ObservableProperty]
	private string _editConflictSummary = "";

	[ObservableProperty]
	private bool _hasEditTransitionWarning;

	[ObservableProperty]
	private string _editTransitionSummary = "";

	[ObservableProperty]
	private bool _hasEditUnavailabilityWarning;

	[ObservableProperty]
	private string _editUnavailabilitySummary = "";

	[ObservableProperty]
	private bool _hasEditRoomSharedWarning;

	[ObservableProperty]
	private string _editRoomSharedSummary = "";

	[ObservableProperty]
	private string _selectedTemplateParity = "Any";

	[ObservableProperty]
	private SchedulePeriodInfo? _selectedPeriod;

	[ObservableProperty]
	private CalendarEntry? _selectedCalendarEntry;

	[ObservableProperty]
	private int _activeTabIndex;

	[ObservableProperty]
	private string _statusMessage = "";

	[ObservableProperty]
	private string _paletteGroupMode = "Class";

	[ObservableProperty]
	private string _paletteSortMode = "ClassSubject";

	[ObservableProperty]
	private SubjectPaletteClassFilter? _subjectPaletteClassFilter;

	[ObservableProperty]
	private string _gridViewMode = "AllClassesDay";

	[ObservableProperty]
	private SchoolClass? _focusClass;

	[ObservableProperty]
	private Room? _focusClassDefaultRoom;

	[ObservableProperty]
	private Room? _focusClassDefaultPeRoom;

	[ObservableProperty]
	private bool _showAllBalanceRows;

	[ObservableProperty]
	private string _workflowStep = "Teachers";

	[ObservableProperty]
	private bool _isGridFullscreen;

	[ObservableProperty]
	private double _gridZoom = 1.0;

	[ObservableProperty]
	private SchoolClass? _workloadChartClass;

	[ObservableProperty]
	private string _weeklyLoadPolylinePoints = "";

	[ObservableProperty]
	private string _weeklyLoadPolylinePointsLarge = "";

	[ObservableProperty]
	private string _workloadChartCaption = "Выберите класс для графика нагрузки";

	[ObservableProperty]
	private double _workloadChartWeekTotal;

	[ObservableProperty]
	private bool _isWorkloadChartPopupOpen;

	[ObservableProperty]
	private string _sivkovChartsSummary = "Выберите шаблон недели на вкладке «Сетка»";

	[ObservableProperty]
	private string _teacherPaletteSearchText = "";

	[ObservableProperty]
	private bool _teacherPaletteFilterPrimary = true;

	[ObservableProperty]
	private bool _teacherPaletteFilterSubject = true;

	[ObservableProperty]
	private bool _teacherPaletteFilterAuxiliary = true;

	[ObservableProperty]
	private bool _teacherPaletteFilterUnassigned = true;

	[ObservableProperty]
	private TeacherPaletteItem? _selectedPlacementTeacher;

	private GridCell? _highlightedGridCell;


	public IReadOnlyList<string> ComplianceSortModes { get; } = ComplianceIssueSortModes.All;

	public IRelayCommand<ComplianceIssue?> OpenComplianceIssueCommand { get; private set; } = null;

	public IRelayCommand<ComplianceIssue?> DismissComplianceIssueCommand { get; private set; } = null;

	public IRelayCommand RestoreDismissedComplianceCommand { get; private set; } = null;

	public IRelayCommand ResetComplianceFiltersCommand { get; private set; } = null;

	private bool IsEditPanelSyncSuppressed => _editPanelSyncSuppressDepth > 0;

	public ObservableCollection<WeekTemplateInfo> Templates { get; } = new ObservableCollection<WeekTemplateInfo>();

	public ObservableCollection<CurriculumItem> PaletteItems { get; } = new ObservableCollection<CurriculumItem>();

	public ICollectionView PaletteView { get; }

	public ObservableCollection<ClassGridRow> GridRows { get; } = new ObservableCollection<ClassGridRow>();

	public ObservableCollection<ConstructorDayGridSection> DayGridSections { get; } = new ObservableCollection<ConstructorDayGridSection>();

	public bool HasDayGridSections => DayGridSections.Count > 0;

	public ObservableCollection<ConstructorWeekDayPanel> WeekDayPanels { get; } = new ObservableCollection<ConstructorWeekDayPanel>();

	public bool HasWeekDayPanels => WeekDayPanels.Count > 0;

	public ObservableCollection<LessonWeekRow> WeekGridRows { get; } = new ObservableCollection<LessonWeekRow>();

	public ObservableCollection<LessonNumberHeader> DayLessonHeaders { get; } = new ObservableCollection<LessonNumberHeader>();

	public ObservableCollection<LoadBalanceRow> BalanceRows { get; } = new ObservableCollection<LoadBalanceRow>();

	public ICollectionView BalanceView { get; }

	public ObservableCollection<CalendarEntry> CalendarEntries { get; } = new ObservableCollection<CalendarEntry>();

	public ObservableCollection<SchedulePeriodInfo> Periods { get; } = new ObservableCollection<SchedulePeriodInfo>();

	public ObservableCollection<Subject> SubjectList { get; } = new ObservableCollection<Subject>();

	public ObservableCollection<Teacher> TeacherList { get; } = new ObservableCollection<Teacher>();

	public ObservableCollection<TeacherPickerItem> TeacherPickerItems { get; } = new ObservableCollection<TeacherPickerItem>();

	public ICollectionView TeacherPickerView { get; }

	public ICollectionView TeacherPaletteView { get; }

	public ObservableCollection<Room> RoomList { get; } = new ObservableCollection<Room>();

	public ObservableCollection<SchoolClass> ClassList { get; } = new ObservableCollection<SchoolClass>();

	public ObservableCollection<ComplianceIssue> ComplianceIssues { get; } = new ObservableCollection<ComplianceIssue>();

	public string[] DayNames { get; } = new string[6] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб" };

	public ObservableCollection<string> EditConflictMessages { get; } = new ObservableCollection<string>();

	public ObservableCollection<string> EditTransitionMessages { get; } = new ObservableCollection<string>();

	public ObservableCollection<string> EditUnavailabilityMessages { get; } = new ObservableCollection<string>();

	public ObservableCollection<string> EditRoomSharedMessages { get; } = new ObservableCollection<string>();

	public ObservableCollection<SubjectPaletteClassFilter> SubjectPaletteClassFilters { get; } = new ObservableCollection<SubjectPaletteClassFilter>();

	public ObservableCollection<TeacherPaletteItem> TeacherPaletteItems { get; } = new ObservableCollection<TeacherPaletteItem>();

	public ObservableCollection<SubjectPaletteItem> SubjectPaletteItems { get; } = new ObservableCollection<SubjectPaletteItem>();

	public ObservableCollection<RoomPaletteBuildingGroup> RoomPaletteGroups { get; } = new ObservableCollection<RoomPaletteBuildingGroup>();

	public ObservableCollection<WeeklyLoadChartPoint> WeeklyLoadChartPoints { get; } = new ObservableCollection<WeeklyLoadChartPoint>();

	public ObservableCollection<WeeklyLoadChartPoint> WeeklyLoadChartPointsLarge { get; } = new ObservableCollection<WeeklyLoadChartPoint>();

	public ObservableCollection<ClassWeeklyLoadChartCard> ClassWorkloadCharts { get; } = new ObservableCollection<ClassWeeklyLoadChartCard>();

	public bool HasWorkloadChartClass => WorkloadChartClass != null;

	public string WorkloadChartExpandLabel => IsWorkloadChartPopupOpen ? "Свернуть" : "Развернуть";

	public string BalanceToggleLabel => ShowAllBalanceRows ? "Только расхождения" : "Показать все";

	public string BalancePanelSummary => (BalanceRows.Count == 0) ? "" : (ShowAllBalanceRows ? $"Все {BalanceRows.Count} предметов" : $"{BalanceRows.Count((LoadBalanceRow r) => r.HasWarning)} расхождений из {BalanceRows.Count}");

	public bool ShowBalanceEmptyHint => BalanceRows.Count > 0 && !ShowAllBalanceRows && !BalanceRows.Any((LoadBalanceRow r) => r.HasWarning);

	public bool IsEditRoomRequired => !SubjectScheduleRules.IsDynamicPause(GetEditSubjectName());

	public string EditRoomHint => (!IsEditRoomRequired) ? "Кабинет необязателен — можно оставить пустым или указать вручную" : (SubjectScheduleRules.IsPhysicalEducationSubject(GetEditSubjectName()) ? "Подставляется спортзал класса; для отдельного урока можно изменить" : "Подставляется кабинет класса; для отдельного урока можно изменить");

	public string SubgroupEditorHint
	{
		get
		{
			if (SelectedCell == null)
			{
				return "";
			}
			if (SelectedSubgroupIndex == 0)
			{
				return (SelectedCell.GetPart(1) != null) ? "Подгруппа 1 · в ячейке также есть подгруппа 2" : "Подгруппа 1 · весь класс или первая группа";
			}
			return (SelectedCell.GetPart(0) == null) ? "Подгруппа 2 · можно без подгруппы 1 в этом слоте (окно у другой п/г)" : "Подгруппа 2 · вторая половина ячейки";
		}
	}

	public string SelectedPlacementTeacherCaption => SelectedPlacementTeacher?.PrimaryLine ?? "кликните педагога в палитре";

	public string BalanceEmptyHint => "Расхождений нет — сетка совпадает с нагрузкой из справочника.";

	public bool ShowSubjectPaletteClassFilter => IsSubjectsWorkflow && (IsAllClassesDayMode || IsAllClassesWeekMode);

	public bool IsAllClassesDayMode => GridViewMode == "AllClassesDay";

	public bool IsSingleClassWeekMode => GridViewMode == "SingleClassWeek";

	public bool IsAllClassesWeekMode => GridViewMode == "AllClassesWeek";

	public bool IsTeachersWorkflow => WorkflowStep == "Teachers";

	public bool IsSubjectsWorkflow => WorkflowStep == "Subjects";

	public bool IsRoomsWorkflow => WorkflowStep == "Rooms";

	public string WorkflowStepHint
	{
		get
		{
			string workflowStep = WorkflowStep;
			if (1 == 0)
			{
			}
			string result = ((workflowStep == "Teachers") ? "Перетащите педагога из палитры в ячейку сетки. Зелёная карточка — все часы из нагрузки уже в сетке." : ((!(workflowStep == "Subjects")) ? "Перетащите кабинет в ячейку с педагогом и предметом. Проверяем занятость кабинета." : "Перетащите предмет к уже назначенному педагогу. На карточке — балл Сивкова и остаток часов."));
			if (1 == 0)
			{
			}
			return result;
		}
	}

	public string GridViewModeHint
	{
		get
		{
			string gridViewMode = GridViewMode;
			if (1 == 0)
			{
			}
			string result = ((gridViewMode == "SingleClassWeek") ? "Неделя одного класса: дни Пн–Сб в колонках, уроки в строках" : ((!(gridViewMode == "AllClassesWeek")) ? "Все классы на один день: классы в столбцах по зданиям, уроки в строках" : "Все классы на всю неделю: блоки Пн–Сб друг под другом, прокручивайте вниз"));
			if (1 == 0)
			{
			}
			return result;
		}
	}

	public string TemplateSectionHint
	{
		get
		{
			WeekTemplateInfo selectedTemplate = SelectedTemplate;
			if (1 == 0)
			{
			}
			string result;
			if (selectedTemplate != null)
			{
				string weekParity = selectedTemplate.WeekParity;
				if (!(weekParity == "WeekA"))
				{
					if (weekParity == "WeekB")
					{
						WeekTemplateInfo weekTemplateInfo = selectedTemplate;
						result = "Сейчас редактируете «" + weekTemplateInfo.Name + "». Это расписание на недели Б — вторая половина чередования с «Неделя А».";
					}
					else
					{
						WeekTemplateInfo weekTemplateInfo2 = selectedTemplate;
						result = "Сейчас редактируете «" + weekTemplateInfo2.Name + "». Этот вариант действует каждую неделю — чередование не требуется.";
					}
				}
				else
				{
					result = "Сейчас редактируете «" + selectedTemplate.Name + "». Это расписание на недели А. Создайте также «Неделя Б» и настройте чередование на вкладке «Период».";
				}
			}
			else
			{
				result = "Выберите вкладку недели выше. Если расписание не чередуется — достаточно одного шаблона «Основная» или «Неделя А».";
			}
			if (1 == 0)
			{
			}
			return result;
		}
	}

	public string TemplateActionsHint => "＋ Создать — новый пустой шаблон (имя подберётся само) · \ufffd\u0098 Копия — дублировать текущий со всеми уроками · \ud83d\uddd1 Удалить — без восстановления";

	public string PeriodFormHint => "Задайте, когда действует расписание: триместр, модули внутри него, четверти и т.д. Периоды могут перекрываться — на конкретную дату берётся самый короткий (например, модуль вместо триместра).";

	public string PeriodOverlapHint => "Если триместр чередуется А/Б, а модуль внутри — каждую неделю, то на даты модуля действует правило модуля. Следующий модуль может снова переключиться на А/Б.";

	public string PeriodRecurrenceHint
	{
		get
		{
			string selectedRecurrence = SelectedRecurrence;
			if (1 == 0)
			{
			}
			string result = ((!(selectedRecurrence == "EveryOtherWeek")) ? "Один шаблон каждую неделю — подойдёт «Основная» или «Неделя А» без чередования." : "Недели А и Б чередуются от даты начала этого периода. Нужны шаблоны «Неделя А» и «Неделя Б» на вкладке «Сетка».");
			if (1 == 0)
			{
			}
			return result;
		}
	}

	public string CalendarFormHint => "Праздник — обычно один день (поле «По» можно не заполнять — кнопка «Очистить» рядом). Каникулы — первый и последний день. Компенсационная суббота — когда идут уроки в субботу и с какого буднего дня переносится расписание.";

	public bool IsCalDonorDayVisible => SelectedCalType == "Compensation";

	public int CalDonorDayIndex
	{
		get
		{
			return Math.Clamp(CalDonorDay - 1, 0, 5);
		}
		set
		{
			CalDonorDay = value + 1;
			OnPropertyChanged("CalDonorDayIndex");
		}
	}

	public bool IsScheduleTab => ActiveTabIndex == 0;

	public bool IsSivkovChartsTab => ActiveTabIndex == 3;

	public bool HasClassWorkloadCharts => ClassWorkloadCharts.Count > 0;

	public string HeaderHint
	{
		get
		{
			int activeTabIndex = ActiveTabIndex;
			if (1 == 0)
			{
			}
			string result;
			switch (activeTabIndex)
			{
			case 0:
				result = ((SelectedCell != null) ? (SelectedCell.IsDynamicPauseColumn ? (SelectedCell.ClassName + " · дин. пауза · " + FormatCellDayLabel(SelectedCell)) : $"{SelectedCell.ClassName} · урок {SelectedCell.LessonNumber} · {FormatCellDayLabel(SelectedCell)}") : (IsAllClassesWeekMode ? "Все классы · неделя целиком" : ((!IsSingleClassWeekMode || FocusClass == null) ? "Клик по ячейке — редактирование справа" : ("Класс " + FocusClass.DisplayName + " · неделя целиком"))));
				break;
			case 1:
			case 4:
				result = CheckedCountHint;
				break;
			default:
				result = "";
				break;
			}
			if (1 == 0)
			{
			}
			return result;
		}
	}

	public string SivkovChartsHint => (SelectedTemplate == null) ? "Сначала выберите шаблон недели на вкладке «Сетка»." : ("Графики по шаблону «" + SelectedTemplate.Name + "». Сумма баллов Сивкова по дням — форма «буква М»: пики во Вт и Чт.");

	public string CellEditorTitle => (SelectedCell == null) ? "Ячейка не выбрана" : (SelectedCell.IsDynamicPauseColumn ? (SelectedCell.ClassName + " · дин. пауза · " + FormatCellDayLabel(SelectedCell)) : $"{SelectedCell.ClassName} · урок {SelectedCell.LessonNumber} · {FormatCellDayLabel(SelectedCell)}");

	public string ContextHelpTip
	{
		get
		{
			int activeTabIndex = ActiveTabIndex;
			if (1 == 0)
			{
			}
			string result = activeTabIndex switch
			{
				0 => "1. Режим сетки: день, неделя одного класса или все классы на всю неделю.\n2. Выберите вкладку недели (А/Б — если чередуете).\n3. Перетащите предмет из «Нагрузки» или назначьте педагога и кабинет справа.\nКрасная рамка — накладка. Оранжевая — несколько групп в спортзале одновременно. Delete — очистить ячейку.", 
				1 => "Период связывает шаблон с датами учебного года — выберите их в календаре. Галочка в таблице включает период в построение расписания на день.", 
				2 => "Проверка норм: слева — замечания к уже собранной сетке. Справа — сверка часов в справочнике с типовой программой. «Эталон» заполняет «Справочники → Нагрузка», сетку не меняет.", 
				3 => "Графики всех классов: сумма баллов Сивкова по дням недели. Оранжевая рамка — превышение рекомендуемой дневной нагрузки.", 
				4 => "Каникулы, праздники и компенсационные субботы — даты выбирайте в календаре. Строки с галочкой участвуют в расчёте расписания на день.", 
				_ => "", 
			};
			if (1 == 0)
			{
			}
			return result;
		}
	}

	public bool CanUndo => _undo.CanUndo;

	public IAsyncRelayCommand LoadCommand { get; }

	public IAsyncRelayCommand CreateTemplateCommand { get; }

	public IAsyncRelayCommand CopyTemplateCommand { get; }

	public IAsyncRelayCommand DeleteTemplateCommand { get; }

	public IRelayCommand<GridCell> SelectCellCommand { get; }

	public IAsyncRelayCommand SaveCellCommand { get; }

	public IAsyncRelayCommand ClearCellCommand { get; }

	public IAsyncRelayCommand SavePeriodCommand { get; }

	public IAsyncRelayCommand DeletePeriodCommand { get; }

	public IAsyncRelayCommand SaveCalendarCommand { get; }

	public IAsyncRelayCommand DeleteCalendarCommand { get; }

	public IRelayCommand CancelEditCommand { get; }

	public IRelayCommand ClearCheckboxSelectionCommand { get; }

	public IAsyncRelayCommand UndoLastCommand { get; }

	public IRelayCommand<SchedulePeriodInfo> EditPeriodRowCommand { get; }

	public IAsyncRelayCommand<SchedulePeriodInfo> DeletePeriodRowCommand { get; }

	public IRelayCommand<CalendarEntry> EditCalendarRowCommand { get; }

	public IAsyncRelayCommand<CalendarEntry> DeleteCalendarRowCommand { get; }

	public IRelayCommand ApplySubjectSuggestionCommand { get; }

	public IAsyncRelayCommand RefreshComplianceCommand { get; }

	public IRelayCommand<LoadBalanceRow?> OpenBalanceRowCommand { get; private set; } = null;

	public IAsyncRelayCommand ClearScheduleCommand { get; }

	public IAsyncRelayCommand SaveTemplateParityCommand { get; }

	public IRelayCommand<ConstructorDayGridSection> ToggleGridSectionCommand { get; }

	public IRelayCommand<int> OpenClassWeekCommand { get; }

	public IRelayCommand<object?> OpenAllClassesDayCommand { get; }

	public IRelayCommand ToggleShowAllBalanceCommand { get; }

	public IRelayCommand ClearEditRoomCommand { get; }

	public IRelayCommand ClearCalEndDateCommand { get; }

	public IRelayCommand ToggleGridFullscreenCommand { get; }

	public IRelayCommand ResetGridZoomCommand { get; }

	public IRelayCommand ToggleWorkloadChartPopupCommand { get; }

	public string FullscreenToggleLabel => IsGridFullscreen ? "Свернуть" : "На весь экран";

	public string SavePeriodLabel => (SelectedPeriod == null) ? "Добавить период" : "Сохранить период";

	public string SaveCalendarLabel => (SelectedCalendarEntry == null) ? "Добавить" : "Сохранить";

	public string EditRoomBuildingColorHex => EditRoom?.BuildingColorHex ?? "#94A3B8";

	public string CheckedCountHint
	{
		get
		{
			int num = Periods.Count((SchedulePeriodInfo p) => p.IsSelected);
			if (num > 0)
			{
				return $"Отмечено: {num}";
			}
			int num2 = CalendarEntries.Count((CalendarEntry c) => c.IsSelected);
			return (num2 > 0) ? $"Отмечено: {num2}" : "";
		}
	}


	private void InitializeComplianceUiCommands()
	{
		OpenComplianceIssueCommand = new RelayCommand<ComplianceIssue>(NavigateToComplianceIssue);
		DismissComplianceIssueCommand = new RelayCommand<ComplianceIssue>(DismissComplianceIssue);
		RestoreDismissedComplianceCommand = new RelayCommand(RestoreDismissedCompliance);
		ResetComplianceFiltersCommand = new RelayCommand(ResetComplianceFilters);
	}

	private void ResetComplianceFilters()
	{
		ComplianceShowErrors = true;
		ComplianceShowWarnings = true;
		ComplianceShowInfo = false;
		ComplianceFilterClass = null;
		ComplianceSearchText = "";
		ComplianceSortMode = "Severity";
		ComplianceShowDismissed = false;
	}

	private void DismissComplianceIssue(ComplianceIssue? issue)
	{
		if (issue != null)
		{
			_dismissedComplianceKeys.Add(issue.StableKey);
			ApplyComplianceFilters();
		}
	}

	private void RestoreDismissedCompliance()
	{
		_dismissedComplianceKeys.Clear();
		ApplyComplianceFilters();
	}

	private void PruneDismissedComplianceKeys()
	{
		HashSet<string> valid = _allComplianceIssues.Select((ComplianceIssue i) => i.StableKey).ToHashSet<string>(StringComparer.Ordinal);
		_dismissedComplianceKeys.RemoveWhere((string key) => !valid.Contains(key));
		ComplianceDismissedCount = _dismissedComplianceKeys.Count;
	}

	private void ApplyComplianceFilters()
	{
		IEnumerable<ComplianceIssue> source = _allComplianceIssues.Where(MatchesComplianceFilters);
		string complianceSortMode = ComplianceSortMode;
		if (1 == 0)
		{
		}
		IOrderedEnumerable<ComplianceIssue> orderedEnumerable = ((complianceSortMode == "ClassDay") ? source.OrderByDescending((ComplianceIssue i) => i.Severity).ThenBy<ComplianceIssue, string>((ComplianceIssue i) => i.ClassName, StringComparer.OrdinalIgnoreCase).ThenBy((ComplianceIssue i) => i.DayOfWeek ?? 99)
			.ThenBy((ComplianceIssue i) => i.LessonNumber ?? 99) : ((!(complianceSortMode == "Code")) ? source.OrderByDescending((ComplianceIssue i) => i.Severity).ThenBy<ComplianceIssue, string>((ComplianceIssue i) => i.ClassName, StringComparer.OrdinalIgnoreCase).ThenBy((ComplianceIssue i) => i.DayOfWeek ?? 99) : source.OrderByDescending((ComplianceIssue i) => i.Severity).ThenBy<ComplianceIssue, string>((ComplianceIssue i) => i.Code, StringComparer.OrdinalIgnoreCase).ThenBy<ComplianceIssue, string>((ComplianceIssue i) => i.ClassName, StringComparer.OrdinalIgnoreCase)));
		if (1 == 0)
		{
		}
		source = orderedEnumerable;
		ComplianceIssues.Clear();
		foreach (ComplianceIssue item in source)
		{
			ComplianceIssues.Add(item);
		}
		int num = _allComplianceIssues.Count - ComplianceIssues.Count;
		ComplianceFilterSummary = ((ComplianceIssues.Count == _allComplianceIssues.Count) ? $"Показано: {ComplianceIssues.Count}" : ($"Показано: {ComplianceIssues.Count} из {_allComplianceIssues.Count}" + ((num > 0) ? $" (скрыто фильтром: {num})" : "")));
		ComplianceDismissedCount = _dismissedComplianceKeys.Count;
	}

	private bool MatchesComplianceFilters(ComplianceIssue issue)
	{
		if (!ComplianceShowDismissed && _dismissedComplianceKeys.Contains(issue.StableKey))
		{
			return false;
		}
		if (issue.Severity == ComplianceSeverity.Error && !ComplianceShowErrors)
		{
			return false;
		}
		if (issue.Severity == ComplianceSeverity.Warning && !ComplianceShowWarnings)
		{
			return false;
		}
		if (issue.Severity == ComplianceSeverity.Info && !ComplianceShowInfo)
		{
			return false;
		}
		if (ComplianceFilterClass != null && issue.ClassId != ComplianceFilterClass.Id)
		{
			return false;
		}
		string text = ComplianceSearchText.Trim();
		if (text != null && text.Length > 0)
		{
			string text2 = $"{issue.ClassName} {issue.DayName} {issue.Message} {issue.Code} {issue.SeverityLabel}";
			if (!text2.Contains(text, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}
		return true;
	}

	private void NavigateToComplianceIssue(ComplianceIssue? issue)
	{
		if (issue != null && issue.CanNavigate)
		{
			switch (issue.NavigationTarget)
			{
			case ComplianceNavigationTarget.ScheduleGrid:
				NavigateToScheduleGrid(issue.ClassId, issue.DayOfWeek, issue.LessonNumber);
				break;
			case ComplianceNavigationTarget.DirectoriesClasses:
				_navigation.GoToDirectories(new DirectoriesNavigationContext
				{
					TabIndex = 2,
					ClassId = issue.ClassId
				});
				break;
			case ComplianceNavigationTarget.DirectoriesTeachers:
				_navigation.GoToDirectories(new DirectoriesNavigationContext
				{
					TabIndex = 3,
					TeacherId = issue.TeacherId,
					ClassId = issue.ClassId
				});
				break;
			case ComplianceNavigationTarget.DirectoriesCurriculum:
				_navigation.GoToDirectories(new DirectoriesNavigationContext
				{
					TabIndex = 5,
					ClassId = issue.ClassId
				});
				break;
			}
		}
	}

	private void NavigateToScheduleGrid(int? classId, int? dayOfWeek, int? lessonNumber)
	{
		ActiveTabIndex = 0;
		if (classId.HasValue)
		{
			int cid = classId.GetValueOrDefault();
			if (true)
			{
				SchoolClass schoolClass = ClassList.FirstOrDefault((SchoolClass c) => c.Id == cid);
				if (schoolClass != null)
				{
					FocusClass = schoolClass;
				}
				if (lessonNumber.HasValue)
				{
					int valueOrDefault = lessonNumber.GetValueOrDefault();
					if (dayOfWeek.HasValue)
					{
						int valueOrDefault2 = dayOfWeek.GetValueOrDefault();
						if (true)
						{
							GridViewMode = "SingleClassWeek";
							ReselectGridCell(cid, valueOrDefault, valueOrDefault2);
							StatusMessage = $"Сетка: {schoolClass?.DisplayName ?? issueClassLabel(cid)} · урок {valueOrDefault} · {SanPiNRules.DayName(valueOrDefault2)}";
							goto IL_022f;
						}
					}
				}
				if (dayOfWeek.HasValue)
				{
					int valueOrDefault3 = dayOfWeek.GetValueOrDefault();
					if (true)
					{
						GridViewMode = "SingleClassWeek";
						SelectedDayIndex = valueOrDefault3 - 1;
						StatusMessage = "Сетка: " + (schoolClass?.DisplayName ?? issueClassLabel(cid)) + " · " + SanPiNRules.DayName(valueOrDefault3);
						goto IL_022f;
					}
				}
				GridViewMode = "SingleClassWeek";
				StatusMessage = "Сетка: неделя " + (schoolClass?.DisplayName ?? issueClassLabel(cid));
				goto IL_022f;
			}
		}
		if (dayOfWeek.HasValue)
		{
			int valueOrDefault4 = dayOfWeek.GetValueOrDefault();
			if (true)
			{
				OpenAllClassesDay(valueOrDefault4 - 1);
				StatusMessage = "Сетка: все классы · " + SanPiNRules.DayName(valueOrDefault4);
			}
		}
		goto IL_022f;
		IL_022f:
		OnPropertyChanged("HeaderHint");
	}

	private string issueClassLabel(int classId)
	{
		return ClassList.FirstOrDefault((SchoolClass c) => c.Id == classId)?.DisplayName ?? $"класс #{classId}";
	}

	private void EnterEditPanelSyncSuppress()
	{
		_editPanelSyncSuppressDepth++;
	}

	private void ExitEditPanelSyncSuppress()
	{
		if (_editPanelSyncSuppressDepth > 0)
		{
			_editPanelSyncSuppressDepth--;
		}
	}

	public ConstructorViewModel(WeekTemplateRepository templates, SchoolClassRepository classes, SubjectRepository subjects, TeacherRepository teachers, RoomRepository rooms, SchedulePeriodRepository periods, CalendarRepository calendar, CurriculumRepository curriculum, LoadBalanceChecker loadBalance, ScheduleComplianceChecker compliance, BellRepository bells, BellTemplateAssignmentService bellAssignment, ScheduleConflictDetector conflictDetector, BuildingTransitionChecker transitionChecker, ConstructorDragHintService dragHints, TeacherAvailabilityService availability, TeacherUnavailabilityRepository unavailabilityRepo, TeacherClassPreferenceSyncService teacherClassSync, TeacherCurriculumSyncService teacherCurriculumSync, SubjectCatalogService subjectCatalog, ISaveStateService saveState, CrudUndoService undo, IAppDialogService dialogs, IAppDataRevisionService revision, IModuleNavigationService navigation)
	{
		_templates = templates;
		_classes = classes;
		_subjects = subjects;
		_teachers = teachers;
		_rooms = rooms;
		_periods = periods;
		_calendar = calendar;
		_curriculum = curriculum;
		_loadBalance = loadBalance;
		_compliance = compliance;
		_bells = bells;
		_bellAssignment = bellAssignment;
		_conflictDetector = conflictDetector;
		_transitionChecker = transitionChecker;
		_dragHints = dragHints;
		_availability = availability;
		_unavailabilityRepo = unavailabilityRepo;
		_teacherClassSync = teacherClassSync;
		_teacherCurriculumSync = teacherCurriculumSync;
		_subjectCatalog = subjectCatalog;
		_saveState = saveState;
		_undo = undo;
		_dialogs = dialogs;
		_revision = revision;
		_navigation = navigation;
		InitializeComplianceUiCommands();
		OpenBalanceRowCommand = new RelayCommand<LoadBalanceRow>(NavigateToBalanceRow);
		LoadCommand = new AsyncRelayCommand(LoadAsync);
		CreateTemplateCommand = new AsyncRelayCommand(CreateTemplateAsync);
		CopyTemplateCommand = new AsyncRelayCommand(CopyTemplateAsync);
		SelectCellCommand = new RelayCommand<GridCell>(delegate(GridCell? c)
		{
			SelectedCell = c;
		});
		SaveCellCommand = new AsyncRelayCommand(SaveCellAsync);
		ClearCellCommand = new AsyncRelayCommand(ClearCellAsync);
		SavePeriodCommand = new AsyncRelayCommand(SavePeriodAsync);
		DeletePeriodCommand = new AsyncRelayCommand(DeletePeriodAsync);
		SaveCalendarCommand = new AsyncRelayCommand(SaveCalendarAsync);
		DeleteCalendarCommand = new AsyncRelayCommand(DeleteCalendarAsync);
		DeleteTemplateCommand = new AsyncRelayCommand(DeleteTemplateAsync);
		CancelEditCommand = new RelayCommand(CancelEdit);
		ClearCheckboxSelectionCommand = new RelayCommand(ClearCheckboxSelection);
		UndoLastCommand = new AsyncRelayCommand(UndoLastAsync, () => CanUndo);
		EditPeriodRowCommand = new RelayCommand<SchedulePeriodInfo>(delegate(SchedulePeriodInfo? p)
		{
			if (p != null)
			{
				SelectedPeriod = p;
			}
		});
		DeletePeriodRowCommand = new AsyncRelayCommand<SchedulePeriodInfo>(DeletePeriodRowAsync);
		EditCalendarRowCommand = new RelayCommand<CalendarEntry>(delegate(CalendarEntry? c)
		{
			if (c != null)
			{
				SelectedCalendarEntry = c;
			}
		});
		DeleteCalendarRowCommand = new AsyncRelayCommand<CalendarEntry>(DeleteCalendarRowAsync);
		ApplySubjectSuggestionCommand = new RelayCommand(ApplySubjectSuggestion);
		RefreshComplianceCommand = new AsyncRelayCommand(RefreshComplianceAsync);
		ClearScheduleCommand = new AsyncRelayCommand(RunClearScheduleAsync);
		SaveTemplateParityCommand = new AsyncRelayCommand(SaveTemplateParityAsync);
		ToggleGridSectionCommand = new RelayCommand<ConstructorDayGridSection>(ToggleGridSection);
		OpenClassWeekCommand = new RelayCommand<int>(OpenClassWeek);
		OpenAllClassesDayCommand = new RelayCommand<object>(OpenAllClassesDay);
		ToggleShowAllBalanceCommand = new RelayCommand(delegate
		{
			ShowAllBalanceRows = !ShowAllBalanceRows;
		});
		ClearEditRoomCommand = new RelayCommand(ClearEditRoom);
		ClearCalEndDateCommand = new RelayCommand(ClearCalEndDate);
		ToggleGridFullscreenCommand = new RelayCommand(delegate
		{
			IsGridFullscreen = !IsGridFullscreen;
		});
		ResetGridZoomCommand = new RelayCommand(delegate
		{
			GridZoom = 1.0;
		});
		ToggleWorkloadChartPopupCommand = new RelayCommand(delegate
		{
			IsWorkloadChartPopupOpen = !IsWorkloadChartPopupOpen;
		});
		_undo.Changed += delegate
		{
			OnPropertyChanged("CanUndo");
			UndoLastCommand.NotifyCanExecuteChanged();
		};
		TeacherPickerView = CollectionViewSource.GetDefaultView(TeacherPickerItems);
		TeacherPickerView.GroupDescriptions.Add(new PropertyGroupDescription("GroupName"));
		TeacherPaletteView = CollectionViewSource.GetDefaultView(TeacherPaletteItems);
		TeacherPaletteView.GroupDescriptions.Add(new PropertyGroupDescription("GroupName"));
		TeacherPaletteView.SortDescriptions.Add(new SortDescription("GroupName", ListSortDirection.Ascending));
		TeacherPaletteView.SortDescriptions.Add(new SortDescription("PrimaryLine", ListSortDirection.Ascending));
		ApplyTeacherPaletteView();
		PaletteView = CollectionViewSource.GetDefaultView(PaletteItems);
		ApplyPaletteView();
		BalanceView = CollectionViewSource.GetDefaultView(BalanceRows);
		ApplyBalanceView();
	}

	public async Task ActivateAsync()
	{
		if (!_isLoaded)
		{
			await LoadAsync();
			_isLoaded = true;
			_loadedReferenceRevision = _revision.ReferenceDataRevision;
			_loadedScheduleRevision = _revision.ScheduleRevision;
			return;
		}
		bool referenceStale = _loadedReferenceRevision != _revision.ReferenceDataRevision;
		bool scheduleStale = _loadedScheduleRevision != _revision.ScheduleRevision;
		if (referenceStale)
		{
			await ReloadReferenceDataAsync();
			await EnsureDefaultTemplateAsync();
			await RefreshPaletteAsync();
			await RefreshGridAsync();
			_loadedReferenceRevision = _revision.ReferenceDataRevision;
			_loadedScheduleRevision = _revision.ScheduleRevision;
		}
		else if (scheduleStale)
		{
			await RefreshGridAsync();
			_loadedScheduleRevision = _revision.ScheduleRevision;
		}
	}

	private void MarkScheduleChanged()
	{
		_revision.NotifyScheduleChanged();
		_loadedScheduleRevision = _revision.ScheduleRevision;
	}

	private void MarkReferenceChanged()
	{
		_revision.NotifyReferenceDataChanged();
		_loadedReferenceRevision = _revision.ReferenceDataRevision;
		_loadedScheduleRevision = _revision.ScheduleRevision;
	}

	[RelayCommand]
	private void ClearTeacherPaletteSearch()
	{
		TeacherPaletteSearchText = "";
	}

	private async Task RefreshTemplateViewAsync()
	{
		await RefreshPaletteAsync();
		await RefreshGridAsync();
	}

	private async Task SaveFocusClassDefaultRoomAsync()
	{
		if (FocusClass != null)
		{
			SchoolClass target = ClassList.FirstOrDefault((SchoolClass c) => c.Id == FocusClass.Id) ?? FocusClass;
			target.DefaultRoomId = FocusClass.DefaultRoomId;
			target.DefaultRoomDisplay = FocusClass.DefaultRoomDisplay;
			await _classes.UpdateAsync(target);
			if (FocusClass != target)
			{
				FocusClass = target;
			}
			_saveState.MarkDirty();
			StatusMessage = ((FocusClassDefaultRoom == null) ? ("Кабинет по умолчанию для " + FocusClass.DisplayName + " снят") : ("Кабинет " + FocusClassDefaultRoom.Number + " закреплён за " + FocusClass.DisplayName));
		}
	}

	private async Task SaveFocusClassDefaultPeRoomAsync()
	{
		if (FocusClass != null)
		{
			SchoolClass target = ClassList.FirstOrDefault((SchoolClass c) => c.Id == FocusClass.Id) ?? FocusClass;
			target.DefaultPeRoomId = FocusClass.DefaultPeRoomId;
			target.DefaultPeRoomDisplay = FocusClass.DefaultPeRoomDisplay;
			await _classes.UpdateAsync(target);
			if (FocusClass != target)
			{
				FocusClass = target;
			}
			_saveState.MarkDirty();
			StatusMessage = ((FocusClassDefaultPeRoom == null) ? ("Зал физкультуры для " + FocusClass.DisplayName + " не задан") : $"Физ-ра: {FocusClassDefaultPeRoom.Number} ({FocusClassDefaultPeRoom.BuildingName}) — {FocusClass.DisplayName}");
		}
	}

	private int ResolveCellDay(GridCell? cell)
	{
		int result;
		if (IsSingleClassWeekMode || IsAllClassesWeekMode)
		{
			int? num = cell?.DayOfWeek;
			if (num.HasValue)
			{
				int valueOrDefault = num.GetValueOrDefault();
				if (valueOrDefault >= 1 && valueOrDefault <= 6)
				{
					result = valueOrDefault;
					goto IL_004e;
				}
			}
		}
		result = SelectedDayIndex + 1;
		goto IL_004e;
		IL_004e:
		return result;
	}

	private int ResolveCellDayIndex(GridCell? cell)
	{
		return ResolveCellDay(cell) - 1;
	}

	private string FormatCellDayLabel(GridCell? cell)
	{
		return (cell == null) ? "" : DayNames[ResolveCellDayIndex(cell)];
	}

	private int? ResolveSubjectPaletteClassId()
	{
		if (IsSingleClassWeekMode)
		{
			return FocusClass?.Id;
		}
		if (IsAllClassesDayMode || IsAllClassesWeekMode)
		{
			return SubjectPaletteClassFilter?.Class?.Id;
		}
		return null;
	}

	private void RebuildSubjectPaletteClassFilters()
	{
		int? num = SubjectPaletteClassFilter?.Class?.Id;
		SubjectPaletteClassFilters.Clear();
		SubjectPaletteClassFilters.Add(new SubjectPaletteClassFilter
		{
			DisplayName = "Все"
		});
		foreach (SchoolClass item in ClassList.OrderBy((SchoolClass c) => c.Grade).ThenBy<SchoolClass, string>((SchoolClass c) => c.Letter, StringComparer.OrdinalIgnoreCase))
		{
			SubjectPaletteClassFilters.Add(new SubjectPaletteClassFilter
			{
				Class = item,
				DisplayName = item.DisplayName
			});
		}
		SubjectPaletteClassFilter? subjectPaletteClassFilter;
		if (num.HasValue)
		{
			int id = num.GetValueOrDefault();
			subjectPaletteClassFilter = SubjectPaletteClassFilters.FirstOrDefault(delegate(SubjectPaletteClassFilter f)
			{
				SchoolClass? @class = f.Class;
				return @class != null && @class.Id == id;
			});
		}
		else
		{
			subjectPaletteClassFilter = SubjectPaletteClassFilters.FirstOrDefault();
		}
		SubjectPaletteClassFilter = subjectPaletteClassFilter;
		if (SubjectPaletteClassFilter == null)
		{
			SubjectPaletteClassFilter subjectPaletteClassFilter3 = (SubjectPaletteClassFilter = SubjectPaletteClassFilters.FirstOrDefault());
		}
	}

	private void OpenClassWeek(int classId)
	{
		SchoolClass schoolClass = ClassList.FirstOrDefault((SchoolClass c) => c.Id == classId);
		if (schoolClass != null)
		{
			FocusClass = schoolClass;
			GridViewMode = "SingleClassWeek";
			WorkloadChartClass = schoolClass;
			StatusMessage = "Неделя класса " + schoolClass.DisplayName;
			OnPropertyChanged("HeaderHint");
		}
	}

	private void OpenAllClassesDay(object? parameter)
	{
		if (1 == 0)
		{
		}
		int num2;
		if (!(parameter is int num))
		{
			if (parameter is string text)
			{
				string s = text;
				if (int.TryParse(s, out var result))
				{
					num2 = result;
					goto IL_0053;
				}
			}
			num2 = SelectedDayIndex;
		}
		else
		{
			int num3 = num;
			num2 = num3;
		}
		goto IL_0053;
		IL_0053:
		if (1 == 0)
		{
		}
		int value = num2;
		value = (SelectedDayIndex = Math.Clamp(value, 0, DayNames.Length - 1));
		GridViewMode = "AllClassesDay";
		StatusMessage = "Все классы · " + DayNames[value];
		OnPropertyChanged("HeaderHint");
	}

	private void ToggleGridSection(ConstructorDayGridSection? section)
	{
		if (section == null)
		{
			return;
		}
		section.IsCollapsed = !section.IsCollapsed;
		if (!string.IsNullOrWhiteSpace(section.SectionKey))
		{
			if (section.IsCollapsed)
			{
				_collapsedGridSections.Add(section.SectionKey);
			}
			else
			{
				_collapsedGridSections.Remove(section.SectionKey);
			}
		}
		RecomputeGridSectionVisibility();
	}

	private void RecomputeGridSectionVisibility()
	{
		if (IsAllClassesWeekMode)
		{
			foreach (ConstructorWeekDayPanel weekDayPanel in WeekDayPanels)
			{
				RecomputeSectionsVisibility(weekDayPanel.Sections);
			}
			return;
		}
		RecomputeSectionsVisibility(DayGridSections);
	}

	private static void RecomputeSectionsVisibility(IEnumerable<ConstructorDayGridSection> sections)
	{
		bool hiddenByParentShift = false;
		foreach (ConstructorDayGridSection section in sections)
		{
			if (section.IsShiftHeader)
			{
				hiddenByParentShift = section.IsCollapsed;
			}
			else
			{
				section.SetHiddenByParentShift(hiddenByParentShift);
			}
		}
	}

	private int? ResolvePaletteFilterClassId()
	{
		return ResolveSubjectPaletteClassId();
	}

	private void LoadEditFromPart()
	{
		if (SelectedCell == null)
		{
			EditSubject = null;
			SubjectSearchText = "";
			EditTeacher = null;
			EditRoom = null;
			UpdateSubjectSuggestion();
			OnPropertyChanged("SubgroupEditorHint");
			return;
		}
		SubgroupPart part = SelectedCell.GetPart(SelectedSubgroupIndex);
		int? num;
		if (part == null)
		{
			SubgroupPart part2 = SelectedCell.GetPart((SelectedSubgroupIndex == 0) ? 1 : 0);
			num = part2?.SubjectId;
			if (num.HasValue)
			{
				int sid = num.GetValueOrDefault();
				if (true)
				{
					SubjectSearchText = (EditSubject = SubjectList.FirstOrDefault((Subject s) => s.Id == sid))?.Name ?? part2.SubjectName ?? "";
					goto IL_0131;
				}
			}
			EditSubject = null;
			SubjectSearchText = "";
			goto IL_0131;
		}
		Subject subject2 = ResolveSubject(part);
		SubjectSearchText = subject2?.Name ?? part?.SubjectName ?? "";
		EditSubject = subject2;
		num = part?.TeacherId;
		object editTeacher;
		if (num.HasValue)
		{
			int tid = num.GetValueOrDefault();
			editTeacher = TeacherList.FirstOrDefault((Teacher t) => t.Id == tid);
		}
		else
		{
			editTeacher = null;
		}
		EditTeacher = (Teacher?)editTeacher;
		num = part?.RoomId;
		if (num.HasValue)
		{
			int rid = num.GetValueOrDefault();
			if (rid > 0)
			{
				EditRoom = RoomList.FirstOrDefault((Room r) => r.Id == rid);
				goto IL_02ce;
			}
		}
		if (SubjectScheduleRules.IsDynamicPause(subject2?.Name ?? part?.SubjectName))
		{
			EditRoom = null;
		}
		else
		{
			EditRoom = ResolveRoomForClass(SelectedCell.ClassId, EditTeacher, subject2?.Name ?? part?.SubjectName);
		}
		goto IL_02ce;
		IL_0131:
		EditTeacher = null;
		EditRoom = null;
		ApplyRoomRulesForCurrentSubject();
		UpdateSubjectSuggestion();
		OnPropertyChanged("IsEditRoomRequired");
		OnPropertyChanged("EditRoomBuildingColorHex");
		OnPropertyChanged("SubgroupEditorHint");
		return;
		IL_02ce:
		UpdateSubjectSuggestion();
		OnPropertyChanged("IsEditRoomRequired");
		OnPropertyChanged("EditRoomBuildingColorHex");
		OnPropertyChanged("SubgroupEditorHint");
		TrySuggestTeacherForCell();
	}

	private void TrySuggestTeacherForCell()
	{
		if (EditTeacher != null || SelectedCell == null)
		{
			return;
		}
		Subject subject = EditSubject ?? ResolveSubject(SelectedCell.GetPart(SelectedSubgroupIndex));
		if (subject == null && string.IsNullOrWhiteSpace(SubjectSearchText))
		{
			return;
		}
		string subjectName = subject?.Name ?? SubjectSearchText;
		int subjectId = subject?.Id ?? 0;
		Teacher teacher = CurriculumDropResolver.ResolveTeacher(TeacherList, subjectName, SelectedCell.ClassId, SelectedCell.ClassGrade, SelectedCell.ClassName, subjectId);
		if (teacher == null)
		{
			return;
		}
		EnterEditPanelSyncSuppress();
		try
		{
			EditTeacher = teacher;
		}
		finally
		{
			ExitEditPanelSyncSuppress();
		}
	}

	private Subject? ResolveSubject(SubgroupPart? part)
	{
		int? num = part?.SubjectId;
		if (num.HasValue)
		{
			int sid = num.GetValueOrDefault();
			if (true)
			{
				Subject subject = SubjectList.FirstOrDefault((Subject s) => s.Id == sid);
				if (subject != null)
				{
					return subject;
				}
			}
		}
		if (string.IsNullOrWhiteSpace(part?.SubjectName))
		{
			return null;
		}
		return SubjectList.FirstOrDefault((Subject s) => SubjectNamesMatch(s.Name, part.SubjectName));
	}

	private (int Id, string Name) ResolveEditSubjectForPanel()
	{
		string trimmed = SubjectSearchText.Trim();
		if (EditSubject != null && (trimmed.Length == 0 || SubjectNamesMatch(EditSubject.Name, trimmed)))
		{
			return (Id: EditSubject.Id, Name: EditSubject.Name);
		}
		if (trimmed.Length > 0)
		{
			Subject subject = SubjectList.FirstOrDefault((Subject s) => SubjectNamesMatch(s.Name, trimmed));
			return (subject != null) ? (Id: subject.Id, Name: subject.Name) : (Id: 0, Name: trimmed);
		}
		SubgroupPart subgroupPart = SelectedCell?.GetPart(SelectedSubgroupIndex);
		Subject subject2 = ResolveSubject(subgroupPart);
		if (subject2 != null)
		{
			return (Id: subject2.Id, Name: subject2.Name);
		}
		string text = subgroupPart?.SubjectName?.Trim() ?? "";
		return (text.Length > 0) ? (Id: 0, Name: text) : (Id: 0, Name: "");
	}

	private static bool SubjectNamesMatch(string? a, string? b)
	{
		return !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && a.Trim().Equals(b.Trim(), StringComparison.OrdinalIgnoreCase);
	}

	private string GetEditSubjectName()
	{
		return EditSubject?.Name ?? SubjectSearchText.Trim();
	}

	private Dictionary<int, Room> BuildRoomsById()
	{
		return RoomList.ToDictionary((Room r) => r.Id);
	}

	private Room? ResolveRoomForClass(int classId, Teacher? teacher = null, string? subjectName = null)
	{
		SchoolClass schoolClass = ClassList.FirstOrDefault((SchoolClass c) => c.Id == classId);
		int? num;
		if (SubjectScheduleRules.IsPhysicalEducationSubject(subjectName))
		{
			num = schoolClass?.DefaultPeRoomId;
			if (num.HasValue)
			{
				int peRoomId = num.GetValueOrDefault();
				if (true)
				{
					return RoomList.FirstOrDefault((Room r) => r.Id == peRoomId);
				}
			}
		}
		num = schoolClass?.DefaultRoomId;
		if (num.HasValue)
		{
			int roomId = num.GetValueOrDefault();
			if (true)
			{
				return RoomList.FirstOrDefault((Room r) => r.Id == roomId);
			}
		}
		num = teacher?.RoomId;
		if (num.HasValue)
		{
			int teacherRoomId = num.GetValueOrDefault();
			if (true)
			{
				return RoomList.FirstOrDefault((Room r) => r.Id == teacherRoomId);
			}
		}
		return null;
	}

	private void TryApplyClassDefaultRoom()
	{
		if (SelectedCell != null && IsEditRoomRequired)
		{
			Room room = ResolveRoomForClass(SelectedCell.ClassId, EditTeacher, GetEditSubjectName());
			if (room != null && (EditRoom == null || IsAutoSubstitutedRoom(EditRoom)))
			{
				EditRoom = room;
			}
		}
	}

	private bool IsAutoSubstitutedRoom(Room room)
	{
		if (SelectedCell == null)
		{
			return false;
		}
		SchoolClass schoolClass = ClassList.FirstOrDefault((SchoolClass c) => c.Id == SelectedCell.ClassId);
		if (schoolClass == null)
		{
			return false;
		}
		return room.Id == schoolClass.DefaultRoomId || room.Id == schoolClass.DefaultPeRoomId;
	}

	private void ApplyRoomRulesForCurrentSubject()
	{
		OnPropertyChanged("IsEditRoomRequired");
		OnPropertyChanged("EditRoomHint");
		if (!IsEditRoomRequired)
		{
			EditRoom = null;
		}
		else if (SelectedCell != null)
		{
			EditRoom = ResolveRoomForClass(SelectedCell.ClassId, EditTeacher, GetEditSubjectName());
		}
	}

	private int? ResolveEditRoomIdForSave()
	{
		return EditRoom?.Id;
	}

	private void ClearEditRoom()
	{
		EditRoom = null;
		StatusMessage = (IsEditRoomRequired ? "Кабинет снят — при сохранении укажите другой или он подставится снова" : "Кабинет снят — для дин. паузы можно оставить пустым");
		RefreshEditConflictHintAsync();
	}

	private void ClearCalEndDate()
	{
		CalEndDate = null;
	}

	private async Task SyncTeacherCurriculumAsync(int classId, int? subjectId, int? previousTeacherId, int? newTeacherId, WeekTemplateInfo? template = null)
	{
		if (template == null)
		{
			template = SelectedTemplate;
		}
		int sid = default(int);
		int num;
		if (template != null && subjectId.HasValue)
		{
			sid = subjectId.GetValueOrDefault();
			num = ((sid <= 0) ? 1 : 0);
		}
		else
		{
			num = 1;
		}
		if (num == 0)
		{
			await _teacherCurriculumSync.SyncAfterSlotChangeAsync(template.Id, template.WeekParity, classId, sid, previousTeacherId, newTeacherId);
			await RefreshTeachersCurriculumInMemoryAsync(previousTeacherId, newTeacherId);
		}
	}

	private async Task RefreshTeachersCurriculumInMemoryAsync(params int?[] teacherIds)
	{
		foreach (int id in (from i in teacherIds
			where i is int
			select i.Value).Distinct())
		{
			Teacher teacher = TeacherList.FirstOrDefault((Teacher t) => t.Id == id);
			if (teacher != null)
			{
				await _teachers.RefreshCurriculumAssignmentsAsync(teacher);
			}
		}
		if (SelectedCell != null)
		{
			RebuildTeacherPickerItems();
		}
	}

	private async Task SyncTeacherClassPreferencesAsync(int classId, int? previousTeacherId, int? newTeacherId)
	{
		await _teacherClassSync.SyncAfterSlotChangeAsync(classId, previousTeacherId, newTeacherId);
		await RefreshTeachersPreferredClassesInMemoryAsync(previousTeacherId, newTeacherId);
	}

	private async Task RefreshTeachersPreferredClassesInMemoryAsync(params int?[] teacherIds)
	{
		foreach (int id in (from i in teacherIds
			where i is int
			select i.Value).Distinct())
		{
			Teacher teacher = TeacherList.FirstOrDefault((Teacher t) => t.Id == id);
			if (teacher != null)
			{
				await _teachers.RefreshPreferredClassesAsync(teacher);
			}
		}
		if (SelectedCell != null)
		{
			RebuildTeacherPickerItems();
		}
	}

	private void SyncTeacherPickerSelection()
	{
		EnterEditPanelSyncSuppress();
		try
		{
			SelectedTeacherPickerItem = ((EditTeacher == null) ? null : TeacherPickerItems.FirstOrDefault((TeacherPickerItem i) => i.Teacher?.Id == EditTeacher.Id));
		}
		finally
		{
			ExitEditPanelSyncSuppress();
		}
	}

	private void RebuildTeacherPickerItems()
	{
		TeacherPickerItems.Clear();
		if (SelectedCell == null)
		{
			foreach (Teacher item in TeacherList.OrderBy<Teacher, string>((Teacher t) => t.FullName, StringComparer.OrdinalIgnoreCase))
			{
				TeacherPickerItems.Add(new TeacherPickerItem
				{
					Teacher = item,
					GroupName = "Остальные"
				});
			}
		}
		else
		{
			Subject subject = EditSubject ?? ResolveSubject(SelectedCell.GetPart(SelectedSubgroupIndex));
			var (list, list2, list3) = TeacherRecommendation.SplitForCell(TeacherList, SelectedCell.ClassId, SelectedCell.ClassGrade, SelectedCell.ClassName, subject?.Id ?? 0, subject?.Name);
			foreach (Teacher item2 in list)
			{
				TeacherPickerItems.Add(new TeacherPickerItem
				{
					Teacher = item2,
					GroupName = "★ Нагрузка для ячейки"
				});
			}
			foreach (Teacher item3 in list2)
			{
				TeacherPickerItems.Add(new TeacherPickerItem
				{
					Teacher = item3,
					GroupName = "★ Для этого класса"
				});
			}
			foreach (Teacher item4 in list3)
			{
				TeacherPickerItems.Add(new TeacherPickerItem
				{
					Teacher = item4,
					GroupName = "Остальные"
				});
			}
		}
		TeacherPickerView.Refresh();
	}

	private void RefreshTeacherPicker()
	{
		RebuildTeacherPickerItems();
		SyncTeacherPickerSelection();
	}

	private void UpdateSubjectSuggestion()
	{
		SubjectSuggestionHint = _subjectCatalog.SuggestSubjectName(SubjectSearchText, SubjectList)?.Hint ?? "";
	}

	private void ApplySubjectSuggestion()
	{
		TextSuggestion suggestion = _subjectCatalog.SuggestSubjectName(SubjectSearchText, SubjectList);
		if (suggestion != null)
		{
			SubjectSearchText = suggestion.Suggested;
			EditSubject = SubjectList.FirstOrDefault((Subject s) => SubjectNamesMatch(s.Name, suggestion.Suggested));
			UpdateSubjectSuggestion();
		}
	}

	private async Task LoadAsync()
	{
		await ReloadReferenceDataAsync();
		await EnsureDefaultTemplateAsync();
		await RefreshPaletteAsync();
		await RefreshGridAsync();
		_loadedReferenceRevision = _revision.ReferenceDataRevision;
		_loadedScheduleRevision = _revision.ScheduleRevision;
	}

	private async Task ReloadReferenceDataAsync()
	{
		int? focusClassId = FocusClass?.Id;
		int? selectedTemplateId = SelectedTemplate?.Id;
		_isReloadingReferenceData = true;
		try
		{
			Task<List<WeekTemplateInfo>> templatesTask = _templates.GetTemplatesAsync();
			Task<List<Subject>> subjectsTask = _subjects.GetAllAsync();
			Task<List<Teacher>> teachersTask = _teachers.GetAllAsync();
			Task<List<Room>> roomsTask = _rooms.GetAllAsync();
			Task<List<SchoolClass>> classesTask = _classes.GetAllAsync();
			Task<List<CalendarEntry>> calendarTask = _calendar.GetAllAsync();
			Task<List<SchedulePeriodInfo>> periodsTask = _periods.GetAllAsync();
			await Task.WhenAll(templatesTask, subjectsTask, teachersTask, roomsTask, classesTask, calendarTask, periodsTask);
			Templates.Clear();
			foreach (WeekTemplateInfo t2 in templatesTask.Result)
			{
				Templates.Add(t2);
			}
			SubjectList.Clear();
			foreach (Subject s in subjectsTask.Result)
			{
				SubjectList.Add(s);
			}
			TeacherList.Clear();
			foreach (Teacher t3 in teachersTask.Result)
			{
				TeacherList.Add(t3);
			}
			RefreshTeacherPicker();
			RoomList.Clear();
			foreach (Room r in roomsTask.Result)
			{
				RoomList.Add(r);
			}
			ClassList.Clear();
			foreach (SchoolClass c2 in classesTask.Result)
			{
				ClassList.Add(c2);
			}
			RebuildSubjectPaletteClassFilters();
			if (WorkloadChartClass == null)
			{
				WorkloadChartClass = (from c in ClassList
					orderby c.Grade, c.Letter
					select c).FirstOrDefault();
			}
			CalendarEntries.Clear();
			foreach (CalendarEntry c3 in calendarTask.Result)
			{
				CalendarEntries.Add(c3);
			}
			Periods.Clear();
			foreach (SchedulePeriodInfo p in periodsTask.Result)
			{
				Periods.Add(p);
			}
		}
		finally
		{
			ConstructorViewModel constructorViewModel = this;
			object selectedTemplate;
			if (selectedTemplateId.HasValue)
			{
				int templateId = selectedTemplateId.GetValueOrDefault();
				selectedTemplate = Templates.FirstOrDefault((WeekTemplateInfo t) => t.Id == templateId) ?? Templates.FirstOrDefault();
			}
			else
			{
				selectedTemplate = Templates.FirstOrDefault();
			}
			constructorViewModel.SelectedTemplate = (WeekTemplateInfo?)selectedTemplate;
			_isReloadingReferenceData = false;
		}
		int classId = default(int);
		int num;
		if (focusClassId.HasValue)
		{
			classId = focusClassId.GetValueOrDefault();
			num = 1;
		}
		else
		{
			num = 0;
		}
		if (num != 0)
		{
			FocusClass = ClassList.FirstOrDefault((SchoolClass c) => c.Id == classId);
		}
	}

	private async Task EnsureDefaultTemplateAsync()
	{
		if (Templates.Count > 0)
		{
			if (SelectedTemplate == null)
			{
				SelectedTemplate = Templates.FirstOrDefault();
			}
			return;
		}
		int id = await _templates.CreateAsync("Неделя А");
		Templates.Add(new WeekTemplateInfo
		{
			Id = id,
			Name = "Неделя А",
			WeekParity = "WeekA"
		});
		SelectedTemplate = Templates.FirstOrDefault();
		MarkReferenceChanged();
	}

	private async Task RefreshPaletteAsync()
	{
		PaletteItems.Clear();
		string templateParity = SelectedTemplate?.WeekParity ?? "Any";
		Dictionary<int, SchoolClass> classMap = ClassList.ToDictionary((SchoolClass c) => c.Id);
		foreach (CurriculumItem item in await _curriculum.GetAllAsync())
		{
			if (CurriculumWeekParity.MatchesForTemplate(item.WeekParity, templateParity))
			{
				if (classMap.TryGetValue(item.ClassId, out var cls))
				{
					item.ClassGrade = cls.Grade;
				}
				PaletteItems.Add(item);
				cls = null;
			}
		}
		await UpdatePaletteScheduleCountsAsync();
	}

	private async Task UpdatePaletteScheduleCountsAsync()
	{
		if (SelectedTemplate == null)
		{
			foreach (CurriculumItem item in PaletteItems)
			{
				item.SetScheduleCounts(0.0);
			}
			ApplyPaletteView();
			_templateSlotsCache = new List<LessonSlot>();
			await RefreshWorkflowPalettesAsync(Array.Empty<LessonSlot>());
			return;
		}
		List<LessonSlot> slots = (_templateSlotsCache = await _templates.GetAllSlotsForTemplateAsync(SelectedTemplate.Id));
		Dictionary<int, List<int>> assigneesByCurriculum = await _teachers.GetExplicitAssigneesByCurriculumAsync();
		foreach (CurriculumItem item2 in PaletteItems)
		{
			assigneesByCurriculum.TryGetValue(item2.Id, out List<int> teacherIds);
			double scheduled = CurriculumScheduledHours.CountForClassSubject(slots, item2.ClassId, item2.SubjectId, item2.HasSubgroups, teacherIds);
			item2.SetScheduleCounts(scheduled);
			teacherIds = null;
		}
		ApplyPaletteView();
		await RefreshWorkflowPalettesAsync(slots);
	}

	private void ApplyPaletteView()
	{
		PaletteView.GroupDescriptions.Clear();
		PaletteView.SortDescriptions.Clear();
		switch (PaletteGroupMode)
		{
		case "Class":
			PaletteView.GroupDescriptions.Add(new PropertyGroupDescription("ClassName"));
			break;
		case "Subject":
			PaletteView.GroupDescriptions.Add(new PropertyGroupDescription("SubjectName"));
			break;
		case "Grade":
			PaletteView.GroupDescriptions.Add(new PropertyGroupDescription("GradeGroupTitle"));
			break;
		}
		switch (PaletteSortMode)
		{
		case "SubjectClass":
			PaletteView.SortDescriptions.Add(new SortDescription("SubjectName", ListSortDirection.Ascending));
			PaletteView.SortDescriptions.Add(new SortDescription("ClassName", ListSortDirection.Ascending));
			break;
		case "HoursDesc":
			PaletteView.SortDescriptions.Add(new SortDescription("HoursPerWeek", ListSortDirection.Descending));
			PaletteView.SortDescriptions.Add(new SortDescription("ClassName", ListSortDirection.Ascending));
			PaletteView.SortDescriptions.Add(new SortDescription("SubjectName", ListSortDirection.Ascending));
			break;
		case "HoursAsc":
			PaletteView.SortDescriptions.Add(new SortDescription("HoursPerWeek", ListSortDirection.Ascending));
			PaletteView.SortDescriptions.Add(new SortDescription("ClassName", ListSortDirection.Ascending));
			PaletteView.SortDescriptions.Add(new SortDescription("SubjectName", ListSortDirection.Ascending));
			break;
		default:
			PaletteView.SortDescriptions.Add(new SortDescription("ClassName", ListSortDirection.Ascending));
			PaletteView.SortDescriptions.Add(new SortDescription("SubjectName", ListSortDirection.Ascending));
			break;
		}
		foreach (CurriculumItem paletteItem in PaletteItems)
		{
			paletteItem.PalettePrimaryLine = BuildPalettePrimaryLine(paletteItem);
		}
		int? num = ResolvePaletteFilterClassId();
		if (num.HasValue)
		{
			int classId = num.GetValueOrDefault();
			if (true)
			{
				PaletteView.Filter = (object obj) => obj is CurriculumItem { IsFullyScheduled: false } curriculumItem && curriculumItem.ClassId == classId;
				goto IL_02c0;
			}
		}
		PaletteView.Filter = (object obj) => obj is CurriculumItem curriculumItem2 && !curriculumItem2.IsFullyScheduled;
		goto IL_02c0;
		IL_02c0:
		PaletteView.Refresh();
	}

	private void ApplyBalanceView()
	{
		BalanceView.Filter = (ShowAllBalanceRows ? null : ((Predicate<object>)((object obj) => obj is LoadBalanceRow loadBalanceRow && loadBalanceRow.HasWarning)));
		BalanceView.Refresh();
		OnPropertyChanged("BalanceToggleLabel");
		OnPropertyChanged("BalancePanelSummary");
		OnPropertyChanged("ShowBalanceEmptyHint");
	}

	private string BuildPalettePrimaryLine(CurriculumItem item)
	{
		string text = ((item.WeekParity == "EveryWeek") ? item.SubjectName : (item.SubjectName + " (" + item.WeekParityDisplay + ")"));
		string paletteGroupMode = PaletteGroupMode;
		if (1 == 0)
		{
		}
		string result = paletteGroupMode switch
		{
			"Class" => text, 
			"Subject" => item.ClassName, 
			"Grade" => item.ClassName + " · " + text, 
			_ => item.PaletteLabel, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private async Task RefreshGridAsync()
	{
		int? reselectClassId = SelectedCell?.ClassId;
		int? reselectLesson = SelectedCell?.LessonNumber;
		int? reselectDay = SelectedCell?.DayOfWeek;
		GridRows.Clear();
		DayGridSections.Clear();
		OnPropertyChanged("HasDayGridSections");
		WeekDayPanels.Clear();
		OnPropertyChanged("HasWeekDayPanels");
		WeekGridRows.Clear();
		DayLessonHeaders.Clear();
		HasDayConflicts = false;
		DayConflictSummary = "";
		HasDayRoomSharedWarnings = false;
		DayRoomSharedSummary = "";
		if (SelectedTemplate == null)
		{
			return;
		}
		if (IsSingleClassWeekMode)
		{
			await RefreshWeekGridAsync();
		}
		else if (!IsAllClassesWeekMode)
		{
			await RefreshDayGridAsync();
		}
		else
		{
			await RefreshAllClassesWeekGridAsync();
		}
		BalanceRows.Clear();
		foreach (LoadBalanceRow b in await _loadBalance.CheckAsync(SelectedTemplate.Id))
		{
			BalanceRows.Add(b);
		}
		ApplyBalanceView();
		await RefreshComplianceAsync();
		if (SelectedTemplate != null)
		{
			_templateSlotsCache = await _templates.GetAllSlotsForTemplateAsync(SelectedTemplate.Id);
		}
		else
		{
			_templateSlotsCache = new List<LessonSlot>();
		}
		await UpdatePaletteScheduleCountsAsync();
		await RefreshWeeklyLoadChartAsync();
		await RefreshAllClassWorkloadChartsAsync();
		ReselectGridCell(reselectClassId, reselectLesson, reselectDay);
		await RefreshEditConflictHintAsync();
		await RefreshDragHintCacheAsync();
	}

	private async Task RefreshDragHintCacheAsync()
	{
		if (SelectedTemplate == null)
		{
			_templateSlotsCache = new List<LessonSlot>();
			return;
		}
		_templateSlotsCache = await _templates.GetAllSlotsForTemplateAsync(SelectedTemplate.Id);
		IReadOnlyDictionary<(string From, string To), int> routeMapCache = _routeMapCache;
		IReadOnlyDictionary<(string From, string To), int> readOnlyDictionary = routeMapCache;
		if (readOnlyDictionary == null)
		{
			readOnlyDictionary = (_routeMapCache = await _transitionChecker.LoadRouteMapAsync());
		}
		_ = readOnlyDictionary;
	}

	public void PrefetchTeacherDragHints(int teacherId)
	{
		_dragPrefetchTeacherId = teacherId;
		_dragPrefetchUnavailability = null;
		PrefetchTeacherDragHintsAsync(teacherId);
	}

	private async Task PrefetchTeacherDragHintsAsync(int teacherId)
	{
		List<TeacherUnavailability> blocks = await _unavailabilityRepo.GetForTeacherAsync(teacherId);
		if (_dragPrefetchTeacherId == teacherId)
		{
			_dragPrefetchUnavailability = blocks;
		}
	}

	public void BeginTeacherDragHints(Teacher teacher)
	{
		if (SelectedTemplate == null || _bellPeriods.Count == 0)
		{
			return;
		}
		Dictionary<int, SchoolClass> classesById = ClassList.ToDictionary((SchoolClass c) => c.Id);
		Dictionary<int, Room> roomsById = BuildRoomsById();
		BellTemplateAssignmentSnapshot bellAssignment = _bellAssignment.CreateSnapshot(ClassList, Grade1BellSemesterRules.ReferenceDateForGrid(null));
		IReadOnlyDictionary<(string, string), int> routeMap = _routeMapCache ?? new Dictionary<(string, string), int>();
		List<TeacherUnavailability> teacherUnavailability = ((_dragPrefetchTeacherId == teacher.Id) ? _dragPrefetchUnavailability : null);
		ConstructorDragHintService.EvaluationContext ctx = new ConstructorDragHintService.EvaluationContext
		{
			TemplateSlots = _templateSlotsCache,
			Bells = _bellPeriods,
			RoomsById = roomsById,
			BellAssignment = bellAssignment,
			RouteMap = routeMap,
			ClassHasSubgroups = (int classId) => PaletteItems.Any((CurriculumItem p) => p.ClassId == classId && p.HasSubgroups),
			ResolvePauseSubjectForClass = (int classId) => DynamicPauseScheduleHelper.FindSubjectForClass(classId, PaletteItems, SubjectList),
			TeacherUnavailability = teacherUnavailability,
			ClassesById = classesById
		};
		foreach (GridCell item in EnumerateVisibleCells())
		{
			DragHintResult dragHintResult = _dragHints.EvaluateTeacherDrop(ctx, teacher, item, ResolveCellDay(item));
			item.DragHintLevel = dragHintResult.Level;
			item.DragHintMessage = dragHintResult.Message;
		}
		IsDragHintActive = true;
	}

	public void ClearDragHints()
	{
		foreach (GridCell item in EnumerateVisibleCells())
		{
			item.DragHintLevel = DragHintLevel.None;
			item.DragHintMessage = "";
		}
		_dragPrefetchTeacherId = null;
		_dragPrefetchUnavailability = null;
		IsDragHintActive = false;
	}

	private IEnumerable<GridCell> EnumerateVisibleCells()
	{
		foreach (ConstructorDayGridSection section in DayGridSections)
		{
			foreach (ConstructorLessonRow row in section.LessonRows)
			{
				foreach (GridCell cell in row.Cells)
				{
					yield return cell;
				}
			}
			foreach (ClassGridRow row2 in section.Rows)
			{
				foreach (GridCell lesson in row2.Lessons)
				{
					yield return lesson;
				}
			}
		}
		foreach (ClassGridRow row3 in GridRows)
		{
			foreach (GridCell lesson2 in row3.Lessons)
			{
				yield return lesson2;
			}
		}
		foreach (LessonWeekRow row4 in WeekGridRows)
		{
			foreach (GridCell day in row4.Days)
			{
				yield return day;
			}
		}
		foreach (ConstructorWeekDayPanel panel in WeekDayPanels)
		{
			foreach (ConstructorDayGridSection section2 in panel.Sections)
			{
				foreach (ConstructorLessonRow row5 in section2.LessonRows)
				{
					foreach (GridCell cell2 in row5.Cells)
					{
						yield return cell2;
					}
				}
				foreach (ClassGridRow row6 in section2.Rows)
				{
					foreach (GridCell lesson3 in row6.Lessons)
					{
						yield return lesson3;
					}
				}
			}
		}
	}

	private async Task RefreshDayGridAsync()
	{
		List<BellPeriod> bells = (_bellPeriods = await _bells.GetAllPeriodsAsync());
		List<SchoolClass> classes = await _classes.GetAllAsync();
		BellTemplateAssignmentSnapshot bellAssignment = _bellAssignment.CreateSnapshot(classes, Grade1BellSemesterRules.ReferenceDateForGrid(null));
		if (await TimelineSlotNormalizer.NormalizeTemplateSlotsAsync(_templates, SelectedTemplate.Id, classes, bells, bellAssignment) > 0)
		{
			_saveState.MarkDirty();
		}
		int day = SelectedDayIndex + 1;
		List<LessonSlot> slots = await _templates.GetSlotsForTemplateDayAsync(SelectedTemplate.Id, day);
		List<ScheduleConflict> conflicts = _conflictDetector.Detect(slots, bells, BuildRoomsById(), bellAssignment);
		List<ClassGridRow> rows = ScheduleGridBuilder.BuildDayGrid(classes, slots, 8, RoomList.ToDictionary((Room r) => r.Id));
		AnnotateGridConflicts(rows.SelectMany((ClassGridRow r) => r.Lessons), conflicts);
		List<ConstructorDayGridSection> sections = ConstructorTransposedGridBuilder.BuildSections(classes, slots, bells, RoomList.ToDictionary((Room r) => r.Id), 8, bellAssignment);
		foreach (ConstructorDayGridSection section in sections)
		{
			if (!string.IsNullOrWhiteSpace(section.SectionKey) && _collapsedGridSections.Contains(section.SectionKey))
			{
				section.IsCollapsed = true;
			}
			if (!section.IsShiftHeader)
			{
				AnnotateGridConflicts(section.LessonRows.SelectMany((ConstructorLessonRow r) => r.Cells), conflicts);
			}
			DayGridSections.Add(section);
		}
		RecomputeGridSectionVisibility();
		OnPropertyChanged("HasDayGridSections");
		foreach (ClassGridRow row in rows)
		{
			GridRows.Add(row);
		}
		RefreshStandardSectionHeaders(ResolveDayGridBellReferenceClass());
		List<ScheduleConflict> blocking = conflicts.Where((ScheduleConflict c) => c.IsBlocking).ToList();
		List<ScheduleConflict> shared = conflicts.Where((ScheduleConflict c) => !c.IsBlocking).ToList();
		HasDayConflicts = blocking.Count > 0;
		DayConflictSummary = ((blocking.Count == 0) ? "" : $"⚠ {blocking.Count} накладок в {DayNames[SelectedDayIndex]} — ячейки подсвечены красным");
		HasDayRoomSharedWarnings = shared.Count > 0;
		DayRoomSharedSummary = ((shared.Count == 0) ? "" : $"\ud83c\udfc3 {shared.Count} совпадений в спортзале в {DayNames[SelectedDayIndex]} — оранжевая рамка, сохранение не блокируется");
	}

	private async Task RefreshWeekGridAsync()
	{
		if (FocusClass == null)
		{
			return;
		}
		List<ScheduleConflict> allConflicts = new List<ScheduleConflict>();
		List<LessonSlot> weekSlots = new List<LessonSlot>();
		List<BellPeriod> bells = await _bells.GetAllPeriodsAsync();
		Dictionary<int, Room> roomsById = RoomList.ToDictionary((Room r) => r.Id);
		BellTemplateAssignmentSnapshot bellAssignment = _bellAssignment.CreateSnapshot(new List<SchoolClass> { FocusClass }, Grade1BellSemesterRules.ReferenceDateForGrid(null));
		if (await TimelineSlotNormalizer.NormalizeClassSlotsAsync(_templates, SelectedTemplate.Id, FocusClass, bells, bellAssignment.GetTemplateName(FocusClass)) > 0)
		{
			_saveState.MarkDirty();
		}
		for (int day = 1; day <= 6; day++)
		{
			List<LessonSlot> daySlots = await _templates.GetSlotsForTemplateDayAsync(SelectedTemplate.Id, day);
			weekSlots.AddRange(daySlots.Where((LessonSlot s) => s.ClassId == FocusClass.Id));
			allConflicts.AddRange(_conflictDetector.Detect(daySlots, bells, roomsById, bellAssignment));
		}
		HashSet<int> focusSlotIds = (from s in weekSlots
			select s.SlotId into id
			where id > 0
			select id).ToHashSet();
		List<ScheduleConflict> classConflicts = allConflicts.Where((ScheduleConflict c) => c.SlotIds.Any((int id) => id > 0 && focusSlotIds.Contains(id))).ToList();
		List<LessonWeekRow> rows = ScheduleGridBuilder.BuildClassWeekGrid(FocusClass, weekSlots, bells, 8, roomsById, bellAssignment);
		AnnotateGridConflicts(rows.SelectMany((LessonWeekRow r) => r.Days), classConflicts);
		foreach (LessonWeekRow row in rows)
		{
			if (FocusClass.Grade != 1)
			{
				row.BellTimeDisplay = BellScheduleResolver.GetLessonBellTime(templateName: bellAssignment.GetTemplateName(FocusClass), allPeriods: bells, classGrade: FocusClass.Grade, classShift: FocusClass.Shift, lessonNumber: row.LessonNumber);
			}
			WeekGridRows.Add(row);
		}
		List<ScheduleConflict> blocking = classConflicts.Where((ScheduleConflict c) => c.IsBlocking).ToList();
		List<ScheduleConflict> shared = classConflicts.Where((ScheduleConflict c) => !c.IsBlocking).ToList();
		HasDayConflicts = blocking.Count > 0;
		DayConflictSummary = ((blocking.Count == 0) ? "" : $"⚠ {blocking.Count} накладок у {FocusClass.DisplayName} за неделю — учитель или кабинет заняты другим классом");
		HasDayRoomSharedWarnings = shared.Count > 0;
		DayRoomSharedSummary = ((shared.Count == 0) ? "" : $"\ud83c\udfc3 {shared.Count} совпадений в спортзале у {FocusClass.DisplayName} — оранжевая рамка");
	}

	private async Task RefreshAllClassesWeekGridAsync()
	{
		List<BellPeriod> bells = (_bellPeriods = await _bells.GetAllPeriodsAsync());
		List<SchoolClass> classes = await _classes.GetAllAsync();
		BellTemplateAssignmentSnapshot bellAssignment = _bellAssignment.CreateSnapshot(classes, Grade1BellSemesterRules.ReferenceDateForGrid(null));
		if (await TimelineSlotNormalizer.NormalizeTemplateSlotsAsync(_templates, SelectedTemplate.Id, classes, bells, bellAssignment) > 0)
		{
			_saveState.MarkDirty();
		}
		Dictionary<int, Room> roomsById = RoomList.ToDictionary((Room r) => r.Id);
		int blockingCount = 0;
		int sharedCount = 0;
		for (int day = 1; day <= 6; day++)
		{
			int dayIndex = day - 1;
			List<LessonSlot> slots = await _templates.GetSlotsForTemplateDayAsync(SelectedTemplate.Id, day);
			List<ScheduleConflict> conflicts = _conflictDetector.Detect(slots, bells, roomsById, bellAssignment);
			blockingCount += conflicts.Count((ScheduleConflict c) => c.IsBlocking);
			sharedCount += conflicts.Count((ScheduleConflict c) => !c.IsBlocking);
			List<ConstructorDayGridSection> sections = ConstructorTransposedGridBuilder.BuildSections(classes, slots, bells, roomsById, 8, bellAssignment);
			ApplyDayGridSectionState(sections, day, conflicts);
			RecomputeSectionsVisibility(sections);
			WeekDayPanels.Add(new ConstructorWeekDayPanel
			{
				DayOfWeek = day,
				DayTitle = DayNames[dayIndex],
				Sections = sections
			});
		}
		OnPropertyChanged("HasWeekDayPanels");
		HasDayConflicts = blockingCount > 0;
		DayConflictSummary = ((blockingCount == 0) ? "" : $"⚠ {blockingCount} накладок за неделю — ячейки подсвечены красным");
		HasDayRoomSharedWarnings = sharedCount > 0;
		DayRoomSharedSummary = ((sharedCount == 0) ? "" : $"\ud83c\udfc3 {sharedCount} совпадений в спортзале за неделю — оранжевая рамка, сохранение не блокируется");
	}

	private void ApplyDayGridSectionState(IList<ConstructorDayGridSection> sections, int dayOfWeek, IReadOnlyList<ScheduleConflict> conflicts)
	{
		foreach (ConstructorDayGridSection section in sections)
		{
			if (!string.IsNullOrWhiteSpace(section.SectionKey))
			{
				string item = (section.SectionKey = $"d{dayOfWeek}-{section.SectionKey}");
				if (_collapsedGridSections.Contains(item))
				{
					section.IsCollapsed = true;
				}
			}
			if (section.IsShiftHeader)
			{
				continue;
			}
			AnnotateGridConflicts(section.LessonRows.SelectMany((ConstructorLessonRow r) => r.Cells), conflicts);
			foreach (GridCell item2 in section.LessonRows.SelectMany((ConstructorLessonRow r) => r.Cells))
			{
				item2.DayOfWeek = dayOfWeek;
			}
		}
	}

	private SchoolClass? ResolveDayGridBellReferenceClass()
	{
		if (SelectedCell != null)
		{
			return ClassList.FirstOrDefault((SchoolClass c) => c.Id == SelectedCell.ClassId);
		}
		ConstructorDayGridSection standardSection = DayGridSections.FirstOrDefault((ConstructorDayGridSection s) => !s.IsFirstGradeSection);
		ConstructorDayGridSection constructorDayGridSection = standardSection;
		if (constructorDayGridSection != null && constructorDayGridSection.Rows.Count > 0)
		{
			return ClassList.FirstOrDefault((SchoolClass c) => c.Id == standardSection.Rows[0].ClassId);
		}
		if (GridRows.Count > 0)
		{
			return ClassList.FirstOrDefault((SchoolClass c) => c.Id == GridRows[0].ClassId);
		}
		return null;
	}

	private void RefreshStandardSectionHeaders(SchoolClass? referenceClass)
	{
		DayLessonHeaders.Clear();
		ConstructorDayGridSection constructorDayGridSection = DayGridSections.FirstOrDefault((ConstructorDayGridSection s) => !s.IsFirstGradeSection);
		if (constructorDayGridSection == null)
		{
			return;
		}
		if (referenceClass == null || referenceClass.Grade == 1)
		{
			for (int i = 1; i <= 8; i++)
			{
				DayLessonHeaders.Add(new LessonNumberHeader
				{
					LessonNumber = i
				});
			}
		}
		else
		{
			foreach (LessonNumberHeader item in BellScheduleResolver.BuildLessonHeaders(_bellPeriods, referenceClass.Grade, referenceClass.Shift, 8))
			{
				DayLessonHeaders.Add(item);
			}
		}
		constructorDayGridSection.Columns.Clear();
		foreach (LessonNumberHeader dayLessonHeader in DayLessonHeaders)
		{
			constructorDayGridSection.Columns.Add(new ConstructorTimelineColumn
			{
				LessonNumber = dayLessonHeader.LessonNumber,
				StorageLessonNumber = dayLessonHeader.LessonNumber,
				Title = dayLessonHeader.Title,
				BellTimeDisplay = dayLessonHeader.BellTimeDisplay
			});
		}
	}

	private void RefreshDayLessonHeaders(SchoolClass? referenceClass)
	{
		RefreshStandardSectionHeaders(referenceClass);
	}

	private void ReselectGridCell(int? classId, int? lessonNumber, int? dayOfWeek = null)
	{
		if (!classId.HasValue)
		{
			return;
		}
		int cid = classId.GetValueOrDefault();
		if (!lessonNumber.HasValue)
		{
			return;
		}
		int lesson = lessonNumber.GetValueOrDefault();
		if (1 == 0)
		{
			return;
		}
		int valueOrDefault;
		int num;
		if (IsSingleClassWeekMode)
		{
			if (dayOfWeek.HasValue)
			{
				valueOrDefault = dayOfWeek.GetValueOrDefault();
				switch (valueOrDefault)
				{
				case 1:
				case 2:
				case 3:
				case 4:
				case 5:
				case 6:
					goto IL_0088;
				}
			}
			num = SelectedDayIndex + 1;
			goto IL_008a;
		}
		int valueOrDefault2;
		int num2;
		if (IsAllClassesWeekMode)
		{
			if (dayOfWeek.HasValue)
			{
				valueOrDefault2 = dayOfWeek.GetValueOrDefault();
				switch (valueOrDefault2)
				{
				case 1:
				case 2:
				case 3:
				case 4:
				case 5:
				case 6:
					goto IL_0115;
				}
			}
			num2 = SelectedDayIndex + 1;
			goto IL_0117;
		}
		SelectedCell = DayGridSections.Where((ConstructorDayGridSection s) => s.IsTransposed).SelectMany((ConstructorDayGridSection s) => s.LessonRows).SelectMany((ConstructorLessonRow r) => r.Cells)
			.FirstOrDefault((GridCell c) => c.ClassId == cid && c.LessonNumber == lesson) ?? DayGridSections.SelectMany((ConstructorDayGridSection s) => s.Rows).SelectMany((ClassGridRow r) => r.Lessons).FirstOrDefault((GridCell c) => c.ClassId == cid && c.LessonNumber == lesson) ?? GridRows.SelectMany((ClassGridRow r) => r.Lessons).FirstOrDefault((GridCell c) => c.ClassId == cid && c.LessonNumber == lesson);
		return;
		IL_008a:
		int day = num;
		SelectedCell = WeekGridRows.SelectMany((LessonWeekRow r) => r.Days).FirstOrDefault((GridCell c) => c.ClassId == cid && c.LessonNumber == lesson && c.DayOfWeek == day);
		return;
		IL_0088:
		num = valueOrDefault;
		goto IL_008a;
		IL_0115:
		num2 = valueOrDefault2;
		goto IL_0117;
		IL_0117:
		int day2 = num2;
		SelectedCell = (from s in WeekDayPanels.Where((ConstructorWeekDayPanel p) => p.DayOfWeek == day2).SelectMany((ConstructorWeekDayPanel p) => p.Sections)
			where s.IsTransposed
			select s).SelectMany((ConstructorDayGridSection s) => s.LessonRows).SelectMany((ConstructorLessonRow r) => r.Cells).FirstOrDefault((GridCell c) => c.ClassId == cid && c.LessonNumber == lesson);
	}

	private async Task RefreshComplianceAsync()
	{
		_allComplianceIssues.Clear();
		if (SelectedTemplate == null)
		{
			ComplianceIssues.Clear();
			ComplianceSummary = "";
			ComplianceErrorCount = 0;
			ComplianceWarningCount = 0;
			ComplianceInfoCount = 0;
			HasComplianceFindings = false;
			ComplianceActionMessage = "Выберите шаблон недели на вкладке «Сетка».";
			ComplianceFilterSummary = "";
			return;
		}
		List<BellPeriod> bells = await _bells.GetAllPeriodsAsync();
		List<SchoolClass> classes = await _classes.GetAllAsync();
		if (await TimelineSlotNormalizer.NormalizeTemplateSlotsAsync(assignment: _bellAssignment.CreateSnapshot(classes, Grade1BellSemesterRules.ReferenceDateForGrid(null)), templates: _templates, templateId: SelectedTemplate.Id, classes: classes, bells: bells) > 0)
		{
			_saveState.MarkDirty();
		}
		foreach (ComplianceIssue issue in (await _compliance.CheckTemplateAsync(SelectedTemplate.Id)).OrderByDescending((ComplianceIssue i) => i.Severity).ThenBy<ComplianceIssue, string>((ComplianceIssue i) => i.ClassName, StringComparer.OrdinalIgnoreCase).ThenBy<ComplianceIssue, string>((ComplianceIssue i) => i.DayName, StringComparer.OrdinalIgnoreCase))
		{
			_allComplianceIssues.Add(issue);
		}
		PruneDismissedComplianceKeys();
		ApplyComplianceFilters();
		ComplianceErrorCount = _allComplianceIssues.Count((ComplianceIssue i) => i.Severity == ComplianceSeverity.Error);
		ComplianceWarningCount = _allComplianceIssues.Count((ComplianceIssue i) => i.Severity == ComplianceSeverity.Warning);
		ComplianceInfoCount = _allComplianceIssues.Count((ComplianceIssue i) => i.Severity == ComplianceSeverity.Info);
		HasComplianceFindings = ComplianceErrorCount + ComplianceWarningCount + ComplianceInfoCount > 0;
		int errors = ComplianceErrorCount;
		int warnings = ComplianceWarningCount;
		ComplianceSummary = ((errors + warnings == 0) ? "Замечаний к сетке нет" : ((errors == 0) ? $"Замечаний к сетке: {warnings}" : ((warnings == 0) ? $"Накладок в сетке: {errors}" : $"Накладок: {errors} · замечаний к сетке: {warnings}")));
		ComplianceActionMessage = $"Пересчитано в {DateTime.Now:HH:mm}. Замечаний: {_allComplianceIssues.Count} (показано {ComplianceIssues.Count}). " + "Сверка нагрузки и сетки — на вкладке «Сетка», панель «Сетка vs нагрузка».";
	}

	private async Task RefreshWorkloadBalanceAsync()
	{
		BalanceRows.Clear();
		if (SelectedTemplate == null)
		{
			return;
		}
		foreach (LoadBalanceRow row in await _loadBalance.CheckAsync(SelectedTemplate.Id))
		{
			BalanceRows.Add(row);
		}
		ApplyBalanceView();
	}

	private void NavigateToBalanceRow(LoadBalanceRow? row)
	{
		if (row == null)
		{
			return;
		}
		if (row.IsExtraInGrid)
		{
			SchoolClass schoolClass = ClassList.FirstOrDefault((SchoolClass c) => c.Id == row.ClassId);
			if (schoolClass != null)
			{
				FocusClass = schoolClass;
			}
			ActiveTabIndex = 0;
			GridViewMode = "SingleClassWeek";
			StatusMessage = "Сетка: " + row.Label + " — лишние часы в расписании";
			OnPropertyChanged("HeaderHint");
		}
		else
		{
			_navigation.GoToDirectories(new DirectoriesNavigationContext
			{
				TabIndex = 5,
				ClassId = row.ClassId,
				SubjectName = row.SubjectName
			});
		}
	}

	private async Task RunClearScheduleAsync()
	{
		if (SelectedTemplate == null)
		{
			_dialogs.ShowInfo("Очистить расписание", "Выберите шаблон недели.");
			return;
		}
		var (classIds, dayOnly) = ResolveClearScheduleScope();
		if (classIds.Count == 0)
		{
			_dialogs.ShowInfo("Очистить расписание", IsSingleClassWeekMode ? "Выберите класс в режиме «один класс / неделя»." : "Нет классов для очистки.");
			return;
		}
		string scope = DescribeClearScheduleScope(classIds, dayOnly);
		if (!_dialogs.ConfirmProceed("Очистить расписание", "Удалить все ячейки для " + scope + "?\n\nПредметы, педагоги и кабинеты будут сняты. Действие нельзя отменить кнопкой «Отменить»."))
		{
			return;
		}
		List<LessonSlot> affected = (from s in await _templates.GetAllSlotsForTemplateAsync(SelectedTemplate.Id)
			where classIds.Contains(s.ClassId)
			where !dayOnly.HasValue || s.DayOfWeek == dayOnly.Value
			select s).ToList();
		if (affected.Count == 0)
		{
			_dialogs.ShowInfo("Очистить расписание", "В выбранном диапазоне ячеек нет.");
			return;
		}
		List<(int TeacherId, int ClassId)> teacherClassPairs = (from s in affected
			where s.TeacherId > 0
			select (TeacherId: s.TeacherId, ClassId: s.ClassId)).Distinct().ToList();
		int day = default(int);
		int num;
		if (dayOnly.HasValue)
		{
			day = dayOnly.GetValueOrDefault();
			num = 1;
		}
		else
		{
			num = 0;
		}
		if (num == 0)
		{
			await _templates.DeleteSlotsForClassesAsync(SelectedTemplate.Id, classIds);
		}
		else
		{
			await _templates.DeleteSlotsForClassesOnDayAsync(SelectedTemplate.Id, day, classIds);
		}
		foreach (var (teacherId, classId) in teacherClassPairs)
		{
			await _teacherClassSync.SyncAfterUnassignAsync(teacherId, classId);
		}
		foreach (LessonSlot slot in affected.Where((LessonSlot s) => s.TeacherId > 0 && s.SubjectId > 0))
		{
			await SyncTeacherCurriculumAsync(slot.ClassId, slot.SubjectId, slot.TeacherId, null);
		}
		await RefreshTeachersPreferredClassesInMemoryAsync(((IEnumerable<(int, int)>)teacherClassPairs).Select((Func<(int, int), int?>)(((int TeacherId, int ClassId) p) => p.TeacherId)).ToArray());
		await RefreshTeachersCurriculumInMemoryAsync(((IEnumerable<(int, int)>)teacherClassPairs).Select((Func<(int, int), int?>)(((int TeacherId, int ClassId) p) => p.TeacherId)).ToArray());
		SelectedCell = null;
		_saveState.MarkDirty();
		MarkScheduleChanged();
		await RefreshGridAsync();
		await RefreshPaletteAsync();
		await RefreshComplianceAsync();
		_dialogs.ShowInfo("Очищено", $"Расписание для {scope} удалено ({affected.Count} ячеек).");
	}

	private void RefreshPlacementSelectionUi()
	{
		int? num = SelectedPlacementTeacher?.Teacher.Id;
		foreach (TeacherPaletteItem teacherPaletteItem in TeacherPaletteItems)
		{
			teacherPaletteItem.IsSelectedForPlacement = num == teacherPaletteItem.Teacher.Id;
		}
	}

	private (IReadOnlyList<int> ClassIds, int? DayOnly) ResolveClearScheduleScope()
	{
		if (IsSingleClassWeekMode && FocusClass != null)
		{
			return (ClassIds: new List<int> { FocusClass.Id }, DayOnly: null);
		}
		List<int> item = (from c in ClassList
			select c.Id into id
			orderby ClassList.FirstOrDefault((SchoolClass c) => c.Id == id)?.Grade ?? 0
			select id).ThenBy<int, string>((int id) => ClassList.FirstOrDefault((SchoolClass c) => c.Id == id)?.DisplayName ?? "", StringComparer.OrdinalIgnoreCase).ToList();
		if (IsAllClassesDayMode)
		{
			return (ClassIds: item, DayOnly: SelectedDayIndex + 1);
		}
		return (ClassIds: item, DayOnly: null);
	}

	private string DescribeClearScheduleScope(IReadOnlyList<int> classIds, int? dayOnly)
	{
		if (IsSingleClassWeekMode && FocusClass != null && classIds.Count == 1)
		{
			return (!dayOnly.HasValue) ? (FocusClass.DisplayName + " (вся неделя)") : (FocusClass.DisplayName + ", " + DayNames[dayOnly.Value - 1]);
		}
		if (IsAllClassesDayMode && dayOnly.HasValue)
		{
			int valueOrDefault = dayOnly.GetValueOrDefault();
			if (true)
			{
				return "всех классов, " + DayNames[valueOrDefault - 1];
			}
		}
		if (classIds.Count <= 6)
		{
			List<string> list = (from id in classIds
				select ClassList.FirstOrDefault((SchoolClass c) => c.Id == id)?.DisplayName into n
				where !string.IsNullOrWhiteSpace(n)
				select n).ToList();
			if (list.Count > 0)
			{
				return (!dayOnly.HasValue) ? (string.Join(", ", list) + " (вся неделя)") : (string.Join(", ", list) + ", " + DayNames[dayOnly.Value - 1]);
			}
		}
		return (!dayOnly.HasValue) ? $"{classIds.Count} классов (вся неделя)" : $"{classIds.Count} классов, {DayNames[dayOnly.Value - 1]}";
	}

	private async Task SaveTemplateParityAsync()
	{
		if (SelectedTemplate != null)
		{
			await _templates.UpdateParityAsync(SelectedTemplate.Id, SelectedTemplateParity);
			SelectedTemplate.WeekParity = SelectedTemplateParity;
			_saveState.MarkDirty();
			MarkScheduleChanged();
			await RefreshPaletteAsync();
			await RefreshGridAsync();
		}
	}

	private async Task CreateTemplateAsync()
	{
		string suggested = WeekTemplateNameSuggestions.SuggestNext(Templates.Select((WeekTemplateInfo t) => t.Name));
		string name = _dialogs.PromptForText("Новый шаблон", "Укажите название недели. Для чередования используйте «Неделя А», «Неделя Б», «Неделя В» и далее по алфавиту.", suggested);
		if (name == null)
		{
			return;
		}
		if (await _templates.NameExistsAsync(name))
		{
			_dialogs.ShowWarning("Конструктор", "Шаблон «" + name + "» уже существует.");
			return;
		}
		try
		{
			int id = await _templates.CreateAsync(name);
			_saveState.MarkDirty();
			await LoadAsync();
			SelectedTemplate = Templates.FirstOrDefault((WeekTemplateInfo t) => t.Id == id);
			_dialogs.ShowInfo("Конструктор", "Создан шаблон «" + name + "».");
		}
		catch (Exception ex)
		{
			_dialogs.ShowError("Конструктор", "Не удалось создать шаблон.\n" + ex.Message);
		}
	}

	private async Task CopyTemplateAsync()
	{
		if (SelectedTemplate == null)
		{
			_dialogs.ShowInfo("Конструктор", "Сначала выберите шаблон для копирования.");
			return;
		}
		string sourceName = SelectedTemplate.Name;
		string suggested = WeekTemplateNameSuggestions.SuggestCopyName(sourceName, Templates.Select((WeekTemplateInfo t) => t.Name));
		string name = _dialogs.PromptForText("Копия шаблона", "Создаётся копия «" + sourceName + "». Укажите название новой недели — обычно следующая буква алфавита.", suggested, "Название", "Создать копию");
		if (name == null)
		{
			return;
		}
		if (await _templates.NameExistsAsync(name))
		{
			_dialogs.ShowWarning("Конструктор", "Шаблон «" + name + "» уже существует.");
			return;
		}
		try
		{
			int id = await _templates.CreateFromCopyAsync(name, SelectedTemplate.Id);
			_saveState.MarkDirty();
			await LoadAsync();
			SelectedTemplate = Templates.FirstOrDefault((WeekTemplateInfo t) => t.Id == id);
			_dialogs.ShowInfo("Конструктор", $"Создана копия «{name}» из «{sourceName}».");
		}
		catch (Exception ex)
		{
			_dialogs.ShowError("Конструктор", "Не удалось скопировать шаблон.\n" + ex.Message);
		}
	}

	private async Task<Subject?> ResolveEditSubjectForSaveAsync()
	{
		if (EditSubject != null && (string.IsNullOrWhiteSpace(SubjectSearchText) || SubjectNamesMatch(EditSubject.Name, SubjectSearchText)))
		{
			return EditSubject;
		}
		if (!string.IsNullOrWhiteSpace(SubjectSearchText))
		{
			return await _subjectCatalog.ResolveOrCreateAsync(SubjectSearchText, SubjectList, null);
		}
		var (id, name) = ResolveEditSubjectForPanel();
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}
		if (id > 0)
		{
			Subject subject = SubjectList.FirstOrDefault((Subject s) => s.Id == id);
			Subject subject2 = subject;
			if (subject2 == null)
			{
				subject2 = await _subjectCatalog.ResolveOrCreateAsync(name, SubjectList, null);
			}
			return subject2;
		}
		return await _subjectCatalog.ResolveOrCreateAsync(name, SubjectList, null);
	}

	private async Task SaveCellAsync()
	{
		WeekTemplateInfo template = SelectedTemplate;
		GridCell cell = SelectedCell;
		Teacher teacher = EditTeacher;
		Room room = EditRoom;
		int subgroupIndex = SelectedSubgroupIndex;
		if (template == null || cell == null || teacher == null)
		{
			_dialogs.ShowInfo("Конструктор", "Выберите ячейку и учителя.");
			return;
		}
		int classId = cell.ClassId;
		int lessonNumber = cell.LessonNumber;
		int cellDay = ResolveCellDay(cell);
		SubgroupPart part = cell.GetPart(subgroupIndex);
		int? previousTeacherId = part?.TeacherId;
		int? roomId = room?.Id;
		try
		{
			Subject subject = await ResolveEditSubjectForSaveAsync();
			if (subject == null)
			{
				_dialogs.ShowInfo("Конструктор", "Укажите предмет.");
				return;
			}
			if (!SubjectList.Any((Subject s) => s.Id == subject.Id))
			{
				SubjectList.Add(subject);
			}
			if (SubjectScheduleRules.RequiresRoom(subject.Name) && room == null)
			{
				_dialogs.ShowInfo("Конструктор", "Укажите кабинет для этого урока.");
			}
			else
			{
				if (!ValidateTimelineCellSubject(cell, subject.Name))
				{
					return;
				}
				if (subgroupIndex == 1 && !PaletteItems.Any((CurriculumItem p) => p.ClassId == classId && p.SubjectId == subject.Id && p.HasSubgroups))
				{
					_dialogs.ShowInfo("Конструктор", "В нагрузке для этого предмета не отмечены подгруппы. Включите «П/г» в справочнике нагрузки.");
					return;
				}
				LessonSlot proposed = BuildProposedSlot(classId, lessonNumber, subject.Id, subject.Name, teacher.Id, teacher.FullName, roomId, room?.Number, subgroupIndex, part?.SlotId, cellDay);
				if (!WarnAndBlockSave(await GetConflictsForProposedAsync(proposed)) && await ConfirmTransitionWarningsAsync(proposed) && await ConfirmUnavailabilityWarningsAsync(proposed))
				{
					await _templates.UpsertSlotAsync(template.Id, cellDay, lessonNumber, classId, subject.Id, teacher.Id, roomId, subgroupIndex);
					await SyncTeacherClassPreferencesAsync(classId, previousTeacherId, teacher.Id);
					await SyncTeacherCurriculumAsync(classId, subject.Id, previousTeacherId, teacher.Id, template);
					_saveState.MarkDirty();
					MarkScheduleChanged();
					StatusMessage = ((cell.IsSplit || subgroupIndex > 0) ? $"Сохранено · подгруппа {subgroupIndex + 1} · {DayNames[Math.Clamp(cellDay - 1, 0, 5)]}, урок {lessonNumber}" : $"Сохранено · {DayNames[Math.Clamp(cellDay - 1, 0, 5)]}, урок {lessonNumber}");
					await RefreshGridAsync();
				}
			}
		}
		catch (ArgumentException)
		{
			_dialogs.ShowInfo("Конструктор", "Укажите корректное название предмета.");
		}
		catch (Exception ex2)
		{
			_dialogs.ShowError("Конструктор", "Не удалось сохранить ячейку.\n" + ex2.Message);
		}
	}

	private async Task ClearCellAsync()
	{
		if (SelectedTemplate == null || SelectedCell == null)
		{
			return;
		}
		int classId = SelectedCell.ClassId;
		int lessonNumber = SelectedCell.LessonNumber;
		int day = ResolveCellDay(SelectedCell);
		SubgroupPart part = SelectedCell.GetPart(SelectedSubgroupIndex);
		if (part == null)
		{
			_dialogs.ShowInfo("Конструктор", "В выбранной подгруппе нет урока.");
			return;
		}
		int? slotId = part.SlotId;
		int id = default(int);
		int num;
		if (slotId.HasValue)
		{
			id = slotId.GetValueOrDefault();
			num = 1;
		}
		else
		{
			num = 0;
		}
		if (num == 0)
		{
			await _templates.DeleteSlotAtAsync(SelectedTemplate.Id, day, lessonNumber, classId, SelectedSubgroupIndex);
		}
		else
		{
			await _templates.DeleteSlotAsync(id);
		}
		slotId = part.TeacherId;
		int teacherId = default(int);
		int num2;
		if (slotId.HasValue)
		{
			teacherId = slotId.GetValueOrDefault();
			num2 = 1;
		}
		else
		{
			num2 = 0;
		}
		if (num2 != 0)
		{
			await _teacherClassSync.SyncAfterUnassignAsync(teacherId, classId);
			slotId = part.SubjectId;
			int subjectId = default(int);
			int num3;
			if (slotId.HasValue)
			{
				subjectId = slotId.GetValueOrDefault();
				num3 = 1;
			}
			else
			{
				num3 = 0;
			}
			if (num3 != 0)
			{
				await SyncTeacherCurriculumAsync(classId, subjectId, teacherId, null);
			}
			await RefreshTeachersPreferredClassesInMemoryAsync(teacherId);
			await RefreshTeachersCurriculumInMemoryAsync(teacherId);
		}
		_saveState.MarkDirty();
		MarkScheduleChanged();
		StatusMessage = ((SelectedCell.IsSplit || SelectedSubgroupIndex > 0) ? $"Подгруппа {SelectedSubgroupIndex + 1} очищена" : "Ячейка очищена");
		await RefreshGridAsync();
	}

	private void WireSelection(SelectableEntity item)
	{
		item.PropertyChanged += delegate(object? _, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "IsSelected")
			{
				OnPropertyChanged("CheckedCountHint");
				OnPropertyChanged("HeaderHint");
			}
		};
	}

	private void ClearCheckboxSelection()
	{
		foreach (SchedulePeriodInfo period in Periods)
		{
			period.IsSelected = false;
		}
		foreach (CalendarEntry calendarEntry in CalendarEntries)
		{
			calendarEntry.IsSelected = false;
		}
		CancelEdit();
		OnPropertyChanged("CheckedCountHint");
		OnPropertyChanged("HeaderHint");
	}

	private static List<T> GetDeleteTargets<T>(IEnumerable<T> all, T? single) where T : SelectableEntity
	{
		List<T> list = all.Where((T x) => x.IsSelected).ToList();
		if (list.Count > 0)
		{
			return list;
		}
		List<T> list2;
		if (single == null)
		{
			list2 = new List<T>();
		}
		else
		{
			int num = 1;
			list2 = new List<T>(num);
			CollectionsMarshal.SetCount(list2, num);
			Span<T> span = CollectionsMarshal.AsSpan(list2);
			int num2 = 0;
			span[num2] = single;
			num2++;
		}
		return list2;
	}

	private async Task UndoLastAsync()
	{
		await _undo.UndoAsync();
		OnPropertyChanged("CanUndo");
	}

	private bool ConfirmDeleteMany(int count, DeleteEntityKind kind)
	{
		return _dialogs.ConfirmDeleteMany(count, kind);
	}

	private bool ConfirmDeleteTargets<T>(List<T> targets, Func<T, string> getLabel, DeleteEntityKind kind)
	{
		if (targets.Count == 0)
		{
			return false;
		}
		return (targets.Count == 1) ? ConfirmDelete(getLabel(targets[0]), kind) : ConfirmDeleteMany(targets.Count, kind);
	}

	private static SchedulePeriodInfo CopyPeriod(SchedulePeriodInfo p)
	{
		return new SchedulePeriodInfo
		{
			Id = p.Id,
			Name = p.Name,
			PeriodType = p.PeriodType,
			StartDate = p.StartDate,
			EndDate = p.EndDate,
			RecurrenceCycle = p.RecurrenceCycle
		};
	}

	private static CalendarEntry CopyCalendar(CalendarEntry c)
	{
		return new CalendarEntry
		{
			Id = c.Id,
			StartDate = c.StartDate,
			EndDate = c.EndDate,
			ExceptionType = c.ExceptionType,
			DonorDayOfWeek = c.DonorDayOfWeek,
			Note = c.Note
		};
	}

	private async Task DeletePeriodRowAsync(SchedulePeriodInfo? item)
	{
		if (item != null && ConfirmDelete(item.Name, DeleteEntityKind.Period))
		{
			await DeletePeriodCoreAsync(new List<SchedulePeriodInfo>(1) { item });
		}
	}

	private async Task DeleteCalendarRowAsync(CalendarEntry? item)
	{
		if (item != null && ConfirmDelete(item.TypeDisplay + " " + item.StartDate, DeleteEntityKind.CalendarEntry))
		{
			await DeleteCalendarCoreAsync(new List<CalendarEntry>(1) { item });
		}
	}

	private void CancelEdit()
	{
		SelectedPeriod = null;
		SelectedCalendarEntry = null;
	}

	private void BeginNewPeriod()
	{
		SelectedPeriod = null;
		OnPropertyChanged("SavePeriodLabel");
	}

	private void BeginNewCalendar()
	{
		SelectedCalendarEntry = null;
		OnPropertyChanged("SaveCalendarLabel");
	}

	private void LoadPeriodForm(SchedulePeriodInfo? item)
	{
		_suppressPeriodNameAutofill = true;
		OnPropertyChanged("SavePeriodLabel");
		if (item == null)
		{
			SelectedPeriodType = "Quarter";
			PeriodName = PeriodTypes.ToDisplay(SelectedPeriodType);
			PeriodStart = new DateTime(2025, 9, 1);
			PeriodEnd = new DateTime(2025, 10, 31);
			SelectedRecurrence = "EveryOtherWeek";
			_suppressPeriodNameAutofill = false;
		}
		else
		{
			PeriodName = item.Name;
			SelectedPeriodType = item.PeriodType;
			PeriodStart = ParseStoredDate(item.StartDate);
			PeriodEnd = ParseStoredDate(item.EndDate);
			SelectedRecurrence = RecurrenceCycles.Normalize(item.RecurrenceCycle);
			_suppressPeriodNameAutofill = false;
		}
	}

	private void LoadCalendarForm(CalendarEntry? item)
	{
		OnPropertyChanged("SaveCalendarLabel");
		if (item == null)
		{
			CalStartDate = DateTime.Today;
			CalEndDate = null;
			SelectedCalType = "Holiday";
			CalDonorDay = 1;
			CalNote = "";
		}
		else
		{
			CalStartDate = ParseStoredDate(item.StartDate);
			CalEndDate = ParseStoredDate(item.EndDate);
			SelectedCalType = item.ExceptionType;
			CalDonorDay = item.DonorDayOfWeek ?? 1;
			CalNote = item.Note ?? "";
		}
	}

	private async Task SavePeriodAsync()
	{
		if (string.IsNullOrWhiteSpace(PeriodName) || !PeriodStart.HasValue || !PeriodEnd.HasValue)
		{
			_dialogs.ShowInfo("Период", "Заполните название и обе даты.");
			return;
		}
		if (PeriodEnd.Value.Date < PeriodStart.Value.Date)
		{
			_dialogs.ShowInfo("Период", "Дата окончания не может быть раньше даты начала.");
			return;
		}
		SchedulePeriodInfo item = new SchedulePeriodInfo
		{
			Name = PeriodName.Trim(),
			PeriodType = SelectedPeriodType,
			StartDate = FormatStoredDate(PeriodStart),
			EndDate = FormatStoredDate(PeriodEnd),
			RecurrenceCycle = RecurrenceCycles.Normalize(SelectedRecurrence)
		};
		bool isNew = SelectedPeriod == null;
		int? savedId = SelectedPeriod?.Id;
		if (SelectedPeriod != null)
		{
			SchedulePeriodInfo before = CopyPeriod(SelectedPeriod);
			item.Id = SelectedPeriod.Id;
			await _periods.UpdateAsync(item);
			_undo.Push(async delegate
			{
				await _periods.UpdateAsync(before);
				_saveState.MarkDirty();
				await ReloadPeriodsAsync();
			});
		}
		else
		{
			int id = await _periods.InsertAsync(item);
			_undo.Push(async delegate
			{
				await _periods.DeleteAsync(id);
				_saveState.MarkDirty();
				await ReloadPeriodsAsync();
			});
		}
		_saveState.MarkDirty();
		await ReloadPeriodsAsync();
		CrudFormHelper.ApplyAfterReload(isNew, savedId, Periods, (SchedulePeriodInfo p) => p.Id, BeginNewPeriod, delegate(SchedulePeriodInfo? p)
		{
			SelectedPeriod = p;
		});
	}

	private async Task DeletePeriodAsync()
	{
		List<SchedulePeriodInfo> targets = GetDeleteTargets(Periods, SelectedPeriod);
		if (ConfirmDeleteTargets(targets, (SchedulePeriodInfo p) => p.Name, DeleteEntityKind.Period))
		{
			await DeletePeriodCoreAsync(targets);
		}
	}

	private async Task DeletePeriodCoreAsync(List<SchedulePeriodInfo> targets)
	{
		List<SchedulePeriodInfo> snapshots = targets.Select(CopyPeriod).ToList();
		foreach (SchedulePeriodInfo t in targets)
		{
			await _periods.DeleteAsync(t.Id);
		}
		List<SchedulePeriodInfo> undoSnaps = snapshots;
		_undo.Push(async delegate
		{
			foreach (SchedulePeriodInfo s in undoSnaps)
			{
				await _periods.InsertAsync(s);
			}
			_saveState.MarkDirty();
			await ReloadPeriodsAsync();
		});
		_saveState.MarkDirty();
		await ReloadPeriodsAsync();
		BeginNewPeriod();
	}

	private async Task SaveCalendarAsync()
	{
		if (!CalStartDate.HasValue)
		{
			_dialogs.ShowInfo("Календарь", "Укажите дату начала.");
			return;
		}
		if (CalEndDate.HasValue && CalEndDate.Value.Date < CalStartDate.Value.Date)
		{
			_dialogs.ShowInfo("Календарь", "Дата окончания не может быть раньше даты начала.");
			return;
		}
		CalendarEntry item = new CalendarEntry
		{
			StartDate = FormatStoredDate(CalStartDate),
			EndDate = ((!CalEndDate.HasValue) ? null : FormatStoredDate(CalEndDate)),
			ExceptionType = SelectedCalType,
			DonorDayOfWeek = ((SelectedCalType == "Compensation") ? new int?(CalDonorDay) : ((int?)null)),
			Note = (string.IsNullOrWhiteSpace(CalNote) ? null : CalNote.Trim())
		};
		bool isNew = SelectedCalendarEntry == null;
		int? savedId = SelectedCalendarEntry?.Id;
		if (SelectedCalendarEntry != null)
		{
			CalendarEntry before = CopyCalendar(SelectedCalendarEntry);
			item.Id = SelectedCalendarEntry.Id;
			await _calendar.UpdateAsync(item);
			_undo.Push(async delegate
			{
				await _calendar.UpdateAsync(before);
				_saveState.MarkDirty();
				await ReloadCalendarAsync();
			});
		}
		else
		{
			int id = await _calendar.InsertAsync(item);
			_undo.Push(async delegate
			{
				await _calendar.DeleteAsync(id);
				_saveState.MarkDirty();
				await ReloadCalendarAsync();
			});
		}
		_saveState.MarkDirty();
		await ReloadCalendarAsync();
		CrudFormHelper.ApplyAfterReload(isNew, savedId, CalendarEntries, (CalendarEntry c) => c.Id, BeginNewCalendar, delegate(CalendarEntry? c)
		{
			SelectedCalendarEntry = c;
		});
	}

	private async Task DeleteCalendarAsync()
	{
		List<CalendarEntry> targets = GetDeleteTargets(CalendarEntries, SelectedCalendarEntry);
		if (ConfirmDeleteTargets(targets, (CalendarEntry c) => c.TypeDisplay + " " + c.StartDate, DeleteEntityKind.CalendarEntry))
		{
			await DeleteCalendarCoreAsync(targets);
		}
	}

	private async Task DeleteCalendarCoreAsync(List<CalendarEntry> targets)
	{
		List<CalendarEntry> snapshots = targets.Select(CopyCalendar).ToList();
		foreach (CalendarEntry t in targets)
		{
			await _calendar.DeleteAsync(t.Id);
		}
		List<CalendarEntry> undoSnaps = snapshots;
		_undo.Push(async delegate
		{
			foreach (CalendarEntry s in undoSnaps)
			{
				await _calendar.InsertAsync(s);
			}
			_saveState.MarkDirty();
			await ReloadCalendarAsync();
		});
		_saveState.MarkDirty();
		await ReloadCalendarAsync();
		BeginNewCalendar();
	}

	private async Task DeleteTemplateAsync()
	{
		if (SelectedTemplate != null)
		{
			if (Templates.Count <= 1)
			{
				_dialogs.ShowInfo("Конструктор", "Нельзя удалить единственный шаблон недели.");
			}
			else if (ConfirmDelete(SelectedTemplate.Name, DeleteEntityKind.Template))
			{
				int deletedId = SelectedTemplate.Id;
				await _templates.DeleteAsync(deletedId);
				_saveState.MarkDirty();
				await LoadAsync();
				SelectedTemplate = Templates.FirstOrDefault();
			}
		}
	}

	private async Task ReloadPeriodsAsync()
	{
		Periods.Clear();
		foreach (SchedulePeriodInfo p in await _periods.GetAllAsync())
		{
			WireSelection(p);
			Periods.Add(p);
		}
		MarkReferenceChanged();
	}

	private async Task ReloadCalendarAsync()
	{
		CalendarEntries.Clear();
		foreach (CalendarEntry c in await _calendar.GetAllAsync())
		{
			WireSelection(c);
			CalendarEntries.Add(c);
		}
		MarkReferenceChanged();
	}

	private bool ConfirmDelete(string label, DeleteEntityKind kind)
	{
		return _dialogs.ConfirmDelete(label, kind);
	}

	private Teacher? ResolveTeacherForDrop(CurriculumDragData payload)
	{
		SchoolClass schoolClass = ClassList.FirstOrDefault((SchoolClass c) => c.Id == payload.ClassId);
		return CurriculumDropResolver.ResolveTeacher(TeacherList, payload.SubjectName, payload.ClassId, schoolClass?.Grade ?? 0, payload.ClassName, payload.SubjectId);
	}

	private Subject? ResolveSubjectFromPayload(CurriculumDragData payload)
	{
		Subject subject = SubjectList.FirstOrDefault((Subject s) => s.Id == payload.SubjectId);
		if (subject != null)
		{
			return subject;
		}
		if (string.IsNullOrWhiteSpace(payload.SubjectName))
		{
			return null;
		}
		return SubjectList.FirstOrDefault((Subject s) => s.Name.Equals(payload.SubjectName, StringComparison.OrdinalIgnoreCase));
	}

	private void ApplyDropEditFields(GridCell target, CurriculumDragData payload, Teacher? teacher, Room? room)
	{
		Subject editSubject = ResolveSubjectFromPayload(payload);
		SubjectSearchText = payload.SubjectName;
		EditSubject = editSubject;
		EditTeacher = teacher ?? ResolveTeacherForDrop(payload);
		EditRoom = (SubjectScheduleRules.IsDynamicPause(payload.SubjectName) ? room : (room ?? ResolveRoomForClass(target.ClassId, EditTeacher, payload.SubjectName)));
		OnPropertyChanged("IsEditRoomRequired");
		OnPropertyChanged("EditRoomHint");
		OnPropertyChanged("EditRoomBuildingColorHex");
	}

	private void PrepareDropEdit(GridCell target, CurriculumDragData payload, Teacher? teacher, Room? room, bool requiresRoom)
	{
		_preparingDropEdit = true;
		EnterEditPanelSyncSuppress();
		try
		{
			SelectedCell = target;
			ApplyDropEditFields(target, payload, teacher, room);
		}
		finally
		{
			_preparingDropEdit = false;
			ExitEditPanelSyncSuppress();
		}
		SyncTeacherPickerSelection();
		if (EditRoom == null && IsEditRoomRequired)
		{
			TryApplyClassDefaultRoom();
		}
		StatusMessage = ((EditTeacher == null) ? "Выберите учителя справа и нажмите «Сохранить»" : ((requiresRoom && EditRoom == null) ? "Выберите кабинет справа и нажмите «Сохранить»" : "Проверьте поля справа и нажмите «Сохранить»"));
	}

	public async Task DropCurriculumAsync(GridCell target, CurriculumDragData payload)
	{
		if (SelectedTemplate == null)
		{
			return;
		}
		if (target.ClassId != payload.ClassId)
		{
			_dialogs.ShowInfo("Конструктор", "Нагрузка для " + payload.ClassName + " — перетащите в строку этого класса.");
		}
		else if (PaletteItems.FirstOrDefault((CurriculumItem p) => p.ClassId == payload.ClassId && p.SubjectId == payload.SubjectId)?.IsFullyScheduled ?? false)
		{
			_dialogs.ShowInfo("Нагрузка", $"«{payload.SubjectName}» для {payload.ClassName} уже полностью разложено в шаблоне.");
		}
		else
		{
			if (!ValidateTimelineCellSubject(target, payload.SubjectName))
			{
				return;
			}
			Teacher teacher = ResolveTeacherForDrop(payload);
			bool requiresRoom = SubjectScheduleRules.RequiresRoom(payload.SubjectName);
			Room room = (requiresRoom ? ResolveRoomForClass(target.ClassId, teacher, payload.SubjectName) : null);
			if (teacher == null || (requiresRoom && room == null))
			{
				PrepareDropEdit(target, payload, teacher, room, requiresRoom);
				if (EditTeacher == null || (requiresRoom && EditRoom == null))
				{
					return;
				}
				teacher = EditTeacher;
				room = EditRoom;
			}
			int subgroupIndex;
			if (payload.HasSubgroups)
			{
				subgroupIndex = ResolveDropSubgroupIndex(target);
				if (subgroupIndex < 0)
				{
					_dialogs.ShowInfo("Конструктор", "В этой ячейке уже заняты обе подгруппы.");
					return;
				}
			}
			else
			{
				subgroupIndex = 0;
			}
			int? saveRoomId = room?.Id;
			ConstructorViewModel constructorViewModel = this;
			int classId = target.ClassId;
			int lessonNumber = target.LessonNumber;
			int subjectId = payload.SubjectId;
			string subjectName = payload.SubjectName;
			int id = teacher.Id;
			string fullName = teacher.FullName;
			int? roomId = saveRoomId;
			object roomNumber;
			if (saveRoomId.HasValue)
			{
				int srid = saveRoomId.GetValueOrDefault();
				roomNumber = RoomList.FirstOrDefault((Room r) => r.Id == srid)?.Number;
			}
			else
			{
				roomNumber = null;
			}
			int subgroupIndex2 = subgroupIndex;
			int? dayOfWeek = ResolveCellDay(target);
			LessonSlot proposed = constructorViewModel.BuildProposedSlot(classId, lessonNumber, subjectId, subjectName, id, fullName, roomId, (string?)roomNumber, subgroupIndex2, null, dayOfWeek);
			if (!WarnAndBlockSave(await GetConflictsForProposedAsync(proposed)) && await ConfirmTransitionWarningsAsync(proposed) && await ConfirmUnavailabilityWarningsAsync(proposed))
			{
				await _templates.UpsertSlotAsync(SelectedTemplate.Id, ResolveCellDay(target), target.LessonNumber, target.ClassId, payload.SubjectId, teacher.Id, saveRoomId, subgroupIndex);
				await SyncTeacherClassPreferencesAsync(target.ClassId, null, teacher.Id);
				await SyncTeacherCurriculumAsync(target.ClassId, payload.SubjectId, null, teacher.Id);
				_saveState.MarkDirty();
				MarkScheduleChanged();
				await RefreshGridAsync();
			}
		}
	}

	public async Task MoveCellAsync(CellDragData source, GridCell target)
	{
		if (SelectedTemplate == null || !source.SubjectId.HasValue || !source.TeacherId.HasValue)
		{
			return;
		}
		bool flag = SubjectScheduleRules.RequiresRoom(SubjectList.FirstOrDefault((Subject s) => s.Id == source.SubjectId)?.Name);
		bool flag2 = flag;
		int? roomId;
		if (flag2)
		{
			roomId = source.RoomId;
			bool flag3 = ((!roomId.HasValue || roomId.GetValueOrDefault() == 0) ? true : false);
			flag2 = flag3;
		}
		if (flag2)
		{
			return;
		}
		int targetSubgroup = ((target.GetPart(source.SubgroupIndex) == null) ? source.SubgroupIndex : ((target.GetPart(0) != null) ? 1 : 0));
		Teacher teacher = TeacherList.FirstOrDefault((Teacher t) => t.Id == source.TeacherId);
		roomId = source.RoomId;
		object obj;
		if (roomId.HasValue)
		{
			int rid = roomId.GetValueOrDefault();
			obj = RoomList.FirstOrDefault((Room r) => r.Id == rid);
		}
		else
		{
			obj = null;
		}
		Room room = (Room)obj;
		Subject subject = SubjectList.FirstOrDefault((Subject s) => s.Id == source.SubjectId);
		if (teacher == null || subject == null || (SubjectScheduleRules.RequiresRoom(subject.Name) && room == null) || !ValidateTimelineCellSubject(target, subject.Name))
		{
			return;
		}
		int targetDay = ResolveCellDay(target);
		int sourceDay = ((source.DayOfWeek > 0) ? source.DayOfWeek : ResolveCellDay(SelectedCell));
		ConstructorViewModel constructorViewModel = this;
		int classId = target.ClassId;
		int lessonNumber = target.LessonNumber;
		int id = subject.Id;
		string name = subject.Name;
		int id2 = teacher.Id;
		string fullName = teacher.FullName;
		int? roomId2 = room?.Id;
		string roomNumber = room?.Number;
		roomId = targetDay;
		LessonSlot proposed = constructorViewModel.BuildProposedSlot(classId, lessonNumber, id, name, id2, fullName, roomId2, roomNumber, targetSubgroup, null, roomId);
		if (!WarnAndBlockSave(await GetConflictsForProposedAsync(proposed)) && await ConfirmTransitionWarningsAsync(proposed) && await ConfirmUnavailabilityWarningsAsync(proposed))
		{
			await _templates.UpsertSlotAsync(SelectedTemplate.Id, targetDay, target.LessonNumber, target.ClassId, source.SubjectId.Value, source.TeacherId.Value, room?.Id, targetSubgroup);
			roomId = source.SlotId;
			int slotId = default(int);
			int num;
			if (roomId.HasValue)
			{
				slotId = roomId.GetValueOrDefault();
				num = 1;
			}
			else
			{
				num = 0;
			}
			if (num == 0)
			{
				await _templates.DeleteSlotAtAsync(SelectedTemplate.Id, sourceDay, source.LessonNumber, source.ClassId, source.SubgroupIndex);
			}
			else
			{
				await _templates.DeleteSlotAsync(slotId);
			}
			await _teacherClassSync.SyncAfterAssignAsync(source.TeacherId.Value, target.ClassId);
			await _teacherClassSync.SyncAfterUnassignAsync(source.TeacherId.Value, source.ClassId);
			await SyncTeacherCurriculumAsync(target.ClassId, source.SubjectId, null, source.TeacherId);
			await SyncTeacherCurriculumAsync(source.ClassId, source.SubjectId, source.TeacherId, null);
			await RefreshTeachersPreferredClassesInMemoryAsync(source.TeacherId);
			_saveState.MarkDirty();
			MarkScheduleChanged();
			await RefreshGridAsync();
		}
	}

	private bool ValidateTimelineCellSubject(GridCell cell, string? subjectName)
	{
		bool flag = SubjectScheduleRules.IsDynamicPause(subjectName);
		if (cell.IsDynamicPauseColumn == flag)
		{
			return true;
		}
		if (flag)
		{
			_dialogs.ShowInfo("Конструктор", "Дин. паузу перетащите в строку «Дин. пауза» между уроками.");
			return false;
		}
		_dialogs.ShowInfo("Конструктор", "Обычный урок нельзя ставить в ячейку дин. паузы — выберите строку урока.");
		return false;
	}

	private LessonSlot BuildProposedSlot(int classId, int lessonNumber, int subjectId, string subjectName, int teacherId, string teacherName, int? roomId, string? roomNumber, int subgroupIndex, int? existingSlotId = null, int? dayOfWeek = null)
	{
		SchoolClass schoolClass = ClassList.FirstOrDefault((SchoolClass c) => c.Id == classId);
		object obj;
		if (roomId.HasValue)
		{
			int rid = roomId.GetValueOrDefault();
			obj = RoomList.FirstOrDefault((Room r) => r.Id == rid);
		}
		else
		{
			obj = null;
		}
		Room room = (Room)obj;
		return new LessonSlot
		{
			SlotId = existingSlotId.GetValueOrDefault(),
			DayOfWeek = (dayOfWeek ?? ResolveCellDay(SelectedCell)),
			LessonNumber = lessonNumber,
			ClassId = classId,
			ClassName = (schoolClass?.DisplayName ?? ""),
			ClassGrade = (schoolClass?.Grade ?? 0),
			ClassShift = (schoolClass?.Shift ?? 1),
			SubjectId = subjectId,
			SubjectName = subjectName,
			TeacherId = teacherId,
			TeacherName = teacherName,
			RoomId = roomId.GetValueOrDefault(),
			RoomNumber = (roomNumber ?? room?.Number ?? ""),
			BuildingName = (room?.BuildingName ?? ""),
			SubgroupIndex = subgroupIndex
		};
	}

	private int ResolveDropSubgroupIndex(GridCell target)
	{
		bool flag = target == SelectedCell;
		bool flag2 = flag;
		if (flag2)
		{
			int selectedSubgroupIndex = SelectedSubgroupIndex;
			bool flag3 = (uint)selectedSubgroupIndex <= 1u;
			flag2 = flag3;
		}
		if (flag2 && target.GetPart(SelectedSubgroupIndex) == null)
		{
			return SelectedSubgroupIndex;
		}
		if (target.GetPart(0) == null)
		{
			return 0;
		}
		if (target.GetPart(1) == null)
		{
			return 1;
		}
		return -1;
	}

	private async Task<List<BuildingTransitionWarning>> GetTransitionWarningsForProposedAsync(LessonSlot proposed)
	{
		if (SelectedTemplate == null)
		{
			return new List<BuildingTransitionWarning>();
		}
		int day = ((proposed.DayOfWeek > 0) ? proposed.DayOfWeek : ResolveCellDay(SelectedCell));
		List<LessonSlot> slots = await _templates.GetSlotsForTemplateDayAsync(SelectedTemplate.Id, day);
		List<BellPeriod> bells = await _bells.GetAllPeriodsAsync();
		IReadOnlyDictionary<(string From, string To), int> routeMap = await _transitionChecker.LoadRouteMapAsync();
		return _transitionChecker.CheckProposedSlot(slots, proposed, bells, routeMap).ToList();
	}

	private async Task<bool> ConfirmTransitionWarningsAsync(LessonSlot proposed)
	{
		List<BuildingTransitionWarning> warnings = await GetTransitionWarningsForProposedAsync(proposed);
		if (warnings.Count == 0)
		{
			return true;
		}
		string message = string.Join("\n\n", warnings.Select((BuildingTransitionWarning w) => w.Message).Distinct());
		return _dialogs.ConfirmProceed("Переход между зданиями", message);
	}

	private async Task<bool> ConfirmUnavailabilityWarningsAsync(LessonSlot proposed)
	{
		IReadOnlyList<string> warnings = await _availability.GetTemplateLessonWarningsAsync(proposed.TeacherId, proposed.DayOfWeek, proposed.LessonNumber, proposed.TeacherName);
		if (warnings.Count == 0)
		{
			return true;
		}
		string message = string.Join("\n\n", warnings) + "\n\nСохранить урок на это время всё равно?";
		return _dialogs.ConfirmProceed("Нерабочее время учителя", message);
	}

	private async Task<List<ScheduleConflict>> GetConflictsForProposedAsync(LessonSlot proposed)
	{
		if (SelectedTemplate == null)
		{
			return new List<ScheduleConflict>();
		}
		int day = ((proposed.DayOfWeek > 0) ? proposed.DayOfWeek : ResolveCellDay(SelectedCell));
		List<LessonSlot> slots = await _templates.GetSlotsForTemplateDayAsync(SelectedTemplate.Id, day);
		List<BellPeriod> bells = await _bells.GetAllPeriodsAsync();
		List<SchoolClass> classes = await _classes.GetAllAsync();
		BellTemplateAssignmentSnapshot assignment = _bellAssignment.CreateSnapshot(classes, Grade1BellSemesterRules.ReferenceDateForGrid(null));
		return _conflictDetector.DetectForProposed(slots, proposed, bells, BuildRoomsById(), assignment);
	}

	private bool WarnAndBlockSave(IReadOnlyList<ScheduleConflict> conflicts)
	{
		List<ScheduleConflict> list = conflicts.Where((ScheduleConflict c) => c.IsBlocking).ToList();
		if (list.Count == 0)
		{
			return false;
		}
		string text = string.Join("\n\n", list.Select((ScheduleConflict c) => c.Message).Distinct());
		_dialogs.ShowWarning("Накладка в расписании", text + "\n\n\ufffd\u0098змените учителя, кабинет или номер урока.");
		return true;
	}

	private static void AnnotateGridConflicts(IEnumerable<GridCell> cells, IReadOnlyList<ScheduleConflict> conflicts)
	{
		Dictionary<int, HashSet<string>> dictionary = new Dictionary<int, HashSet<string>>();
		Dictionary<int, HashSet<string>> dictionary2 = new Dictionary<int, HashSet<string>>();
		foreach (ScheduleConflict conflict in conflicts)
		{
			Dictionary<int, HashSet<string>> dictionary3 = (conflict.IsBlocking ? dictionary : dictionary2);
			foreach (int item in conflict.SlotIds.Where((int id) => id > 0))
			{
				if (!dictionary3.TryGetValue(item, out var value))
				{
					value = (dictionary3[item] = new HashSet<string>());
				}
				value.Add(conflict.Message);
			}
		}
		foreach (GridCell cell in cells)
		{
			cell.HasConflict = false;
			cell.ConflictHint = "";
			cell.HasRoomSharedWarning = false;
			cell.RoomSharedHint = "";
			List<string> list = new List<string>();
			List<string> list2 = new List<string>();
			foreach (SubgroupPart part in cell.Parts)
			{
				int? slotId = part.SlotId;
				if (!slotId.HasValue)
				{
					continue;
				}
				int valueOrDefault = slotId.GetValueOrDefault();
				if (true)
				{
					if (dictionary.TryGetValue(valueOrDefault, out var value2))
					{
						list.AddRange(value2);
					}
					if (dictionary2.TryGetValue(valueOrDefault, out var value3))
					{
						list2.AddRange(value3);
					}
				}
			}
			if (list.Count > 0)
			{
				cell.HasConflict = true;
				cell.ConflictHint = string.Join("\n", list.Distinct());
			}
			if (list2.Count > 0)
			{
				cell.HasRoomSharedWarning = true;
				cell.RoomSharedHint = string.Join("\n", list2.Distinct());
			}
		}
	}

	private async Task RefreshEditConflictHintAsync()
	{
		try
		{
			await RefreshEditConflictHintCoreAsync();
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Debug.WriteLine(ex2);
		}
	}

	private async Task RefreshEditConflictHintCoreAsync()
	{
		EditConflictMessages.Clear();
		EditTransitionMessages.Clear();
		EditUnavailabilityMessages.Clear();
		EditRoomSharedMessages.Clear();
		HasEditConflict = false;
		HasEditTransitionWarning = false;
		HasEditUnavailabilityWarning = false;
		HasEditRoomSharedWarning = false;
		EditConflictCount = 0;
		EditConflictSummary = "";
		EditTransitionSummary = "";
		EditUnavailabilitySummary = "";
		EditRoomSharedSummary = "";
		WeekTemplateInfo template = SelectedTemplate;
		GridCell cell = SelectedCell;
		Teacher teacher = EditTeacher;
		Room room = EditRoom;
		bool roomRequired = IsEditRoomRequired;
		if (template == null || cell == null || teacher == null)
		{
			return;
		}
		int cellDay = ResolveCellDay(cell);
		IReadOnlyList<string> unavailEarly = await _availability.GetTemplateLessonWarningsAsync(teacher.Id, cellDay, cell.LessonNumber, teacher.FullName);
		foreach (string warning in unavailEarly)
		{
			EditUnavailabilityMessages.Add(warning);
		}
		if (unavailEarly.Count > 0)
		{
			HasEditUnavailabilityWarning = true;
			EditUnavailabilitySummary = ((unavailEarly.Count == 1) ? "Совпадает с нерабочим временем" : $"{unavailEarly.Count} совпадения с нерабочим временем");
		}
		if (room == null && roomRequired)
		{
			return;
		}
		cell = SelectedCell;
		teacher = EditTeacher;
		if (cell == null || teacher == null)
		{
			return;
		}
		var (subjectId, subjectName) = ResolveEditSubjectForPanel();
		if (string.IsNullOrWhiteSpace(subjectName))
		{
			return;
		}
		LessonSlot proposed = BuildProposedSlot(existingSlotId: cell.GetPart(SelectedSubgroupIndex)?.SlotId, classId: cell.ClassId, lessonNumber: cell.LessonNumber, subjectId: subjectId, subjectName: subjectName, teacherId: teacher.Id, teacherName: teacher.FullName, roomId: room?.Id, roomNumber: room?.Number, subgroupIndex: SelectedSubgroupIndex, dayOfWeek: cellDay);
		List<ScheduleConflict> conflicts = await GetConflictsForProposedAsync(proposed);
		List<string> blockingMessages = (from c in conflicts
			where c.IsBlocking
			select c.Message).Distinct().ToList();
		string editConflictSummary;
		if (blockingMessages.Count > 0)
		{
			HasEditConflict = true;
			EditConflictCount = blockingMessages.Count;
			int count = blockingMessages.Count;
			if (1 == 0)
			{
			}
			switch (count)
			{
			case 1:
				editConflictSummary = "1 накладка";
				break;
			case 2:
			case 3:
			case 4:
				editConflictSummary = $"{blockingMessages.Count} накладки";
				break;
			default:
				editConflictSummary = $"{blockingMessages.Count} накладок";
				break;
			}
			if (1 == 0)
			{
			}
			EditConflictSummary = editConflictSummary;
			foreach (string message in blockingMessages)
			{
				EditConflictMessages.Add(message);
			}
		}
		List<string> sharedMessages = (from c in conflicts
			where !c.IsBlocking
			select c.Message).Distinct().ToList();
		if (sharedMessages.Count > 0)
		{
			HasEditRoomSharedWarning = true;
			int count2 = sharedMessages.Count;
			if (1 == 0)
			{
			}
			switch (count2)
			{
			case 1:
				editConflictSummary = "Спортзал: одновременно несколько групп";
				break;
			case 2:
			case 3:
			case 4:
				editConflictSummary = $"{sharedMessages.Count} совпадения в спортзале";
				break;
			default:
				editConflictSummary = $"{sharedMessages.Count} совпадений в спортзале";
				break;
			}
			if (1 == 0)
			{
			}
			EditRoomSharedSummary = editConflictSummary;
			foreach (string message2 in sharedMessages)
			{
				EditRoomSharedMessages.Add(message2);
			}
		}
		List<BuildingTransitionWarning> transitions = await GetTransitionWarningsForProposedAsync(proposed);
		if (transitions.Count <= 0)
		{
			return;
		}
		HasEditTransitionWarning = true;
		int count3 = transitions.Count;
		if (1 == 0)
		{
		}
		switch (count3)
		{
		case 1:
			editConflictSummary = "1 переход между зданиями";
			break;
		case 2:
		case 3:
		case 4:
			editConflictSummary = $"{transitions.Count} перехода";
			break;
		default:
			editConflictSummary = $"{transitions.Count} переходов";
			break;
		}
		if (1 == 0)
		{
		}
		EditTransitionSummary = editConflictSummary;
		foreach (BuildingTransitionWarning warning2 in transitions)
		{
			EditTransitionMessages.Add(warning2.Message);
		}
	}

	private async Task RefreshWorkflowPalettesAsync(IReadOnlyList<LessonSlot>? slotsOverride = null)
	{
		await RefreshPlannedHoursCacheAsync();
		if (slotsOverride != null)
		{
			_templateSlotsCache = slotsOverride.ToList();
		}
		else if (SelectedTemplate != null)
		{
			_templateSlotsCache = await _templates.GetAllSlotsForTemplateAsync(SelectedTemplate.Id);
		}
		else
		{
			_templateSlotsCache = new List<LessonSlot>();
		}
		RebuildTeacherPaletteItems(_templateSlotsCache);
		Dictionary<int, SchoolClass> classById = ClassList.ToDictionary((SchoolClass c) => c.Id);
		int? filterClassId = ResolveSubjectPaletteClassId();
		bool showClassInName = !filterClassId.HasValue;
		SubjectPaletteItems.Clear();
		foreach (CurriculumItem item in PaletteItems.Where((CurriculumItem p) => !p.IsFullyScheduled))
		{
			int num;
			if (filterClassId.HasValue)
			{
				int classId = filterClassId.GetValueOrDefault();
				num = ((item.ClassId != classId) ? 1 : 0);
			}
			else
			{
				num = 0;
			}
			if (num == 0)
			{
				SchoolClass cls;
				int grade = (classById.TryGetValue(item.ClassId, out cls) ? cls.Grade : 5);
				double stored = item.SubjectDifficultyScore;
				SubjectPaletteItems.Add(new SubjectPaletteItem
				{
					SubjectId = item.SubjectId,
					SubjectName = item.SubjectName,
					DifficultyScore = OfficialSubjectDifficultyReference.ResolveForClass(item.SubjectName, grade, stored),
					ClassId = item.ClassId,
					ClassName = item.ClassName,
					RemainingHours = item.RemainingHours,
					ShowClassInName = showClassInName
				});
				cls = null;
			}
		}
		List<SubjectPaletteItem> sorted = SubjectPaletteItems.OrderBy<SubjectPaletteItem, string>((SubjectPaletteItem i) => i.ClassName, StringComparer.OrdinalIgnoreCase).ThenBy<SubjectPaletteItem, string>((SubjectPaletteItem i) => i.SubjectName, StringComparer.OrdinalIgnoreCase).ToList();
		SubjectPaletteItems.Clear();
		foreach (SubjectPaletteItem item2 in sorted)
		{
			SubjectPaletteItems.Add(item2);
		}
		RebuildRoomPaletteGroups();
	}

	private void RebuildRoomPaletteGroups()
	{
		Dictionary<string, bool> dictionary = RoomPaletteGroups.ToDictionary((RoomPaletteBuildingGroup g) => g.BuildingName, (RoomPaletteBuildingGroup g) => g.IsExpanded);
		RoomPaletteGroups.Clear();
		IEnumerable<IGrouping<string, Room>> enumerable = RoomList.OrderBy<Room, string>((Room r) => r.BuildingName, StringComparer.OrdinalIgnoreCase).ThenBy<Room, string>((Room r) => r.Number, StringComparer.OrdinalIgnoreCase).GroupBy<Room, string>((Room r) => r.BuildingName, StringComparer.OrdinalIgnoreCase);
		foreach (IGrouping<string, Room> item in enumerable)
		{
			Room room = item.First();
			RoomPaletteBuildingGroup roomPaletteBuildingGroup = new RoomPaletteBuildingGroup
			{
				BuildingName = item.Key,
				BuildingColorHex = room.BuildingColorHex,
				RoomCount = item.Count(),
				IsExpanded = dictionary.GetValueOrDefault(item.Key, defaultValue: true)
			};
			foreach (Room item2 in item)
			{
				roomPaletteBuildingGroup.Rooms.Add(new RoomPaletteItem
				{
					Room = item2
				});
			}
			RoomPaletteGroups.Add(roomPaletteBuildingGroup);
		}
	}

	private async Task RefreshPlannedHoursCacheAsync()
	{
		string parity = SelectedTemplate?.WeekParity ?? "Any";
		_plannedHoursByTeacher = await _teachers.GetPlannedWeeklyHoursByTeacherAsync(parity);
	}

	private void RebuildTeacherPaletteItems(IReadOnlyList<LessonSlot> slots)
	{
		int? num = SelectedPlacementTeacher?.Teacher.Id;
		Dictionary<int, TeacherPaletteItem> dictionary = TeacherPaletteItems.ToDictionary((TeacherPaletteItem i) => i.Teacher.Id);
		HashSet<int> hashSet = new HashSet<int>();
		foreach (Teacher teacher in TeacherList.OrderBy<Teacher, string>((Teacher t) => t.FullName, StringComparer.OrdinalIgnoreCase))
		{
			hashSet.Add(teacher.Id);
			(int Hours, bool FromCurriculum) tuple = TeacherPaletteMetrics.ResolvePlannedHours(teacher, _plannedHoursByTeacher);
			int item = tuple.Hours;
			bool item2 = tuple.FromCurriculum;
			int scheduledCount = ((IsSingleClassWeekMode && FocusClass != null) ? slots.Count((LessonSlot s) => s.TeacherId == teacher.Id && s.ClassId == FocusClass.Id) : TeacherPaletteMetrics.CountScheduledSlots(slots, teacher.Id));
			if (dictionary.TryGetValue(teacher.Id, out var value))
			{
				value.SetScheduleMetrics(scheduledCount, item, item2);
				continue;
			}
			TeacherPaletteItem teacherPaletteItem = new TeacherPaletteItem
			{
				Teacher = teacher,
				GroupName = TeacherPaletteMetrics.ResolveSubjectGroup(teacher)
			};
			teacherPaletteItem.SetScheduleMetrics(scheduledCount, item, item2);
			TeacherPaletteItems.Add(teacherPaletteItem);
		}
		for (int num2 = TeacherPaletteItems.Count - 1; num2 >= 0; num2--)
		{
			if (!hashSet.Contains(TeacherPaletteItems[num2].Teacher.Id))
			{
				TeacherPaletteItems.RemoveAt(num2);
			}
		}
		if (num.HasValue)
		{
			int id = num.GetValueOrDefault();
			if (true)
			{
				SelectedPlacementTeacher = TeacherPaletteItems.FirstOrDefault((TeacherPaletteItem i) => i.Teacher.Id == id);
			}
		}
		ApplyTeacherPaletteView();
		RefreshPlacementSelectionUi();
	}

	private void ApplyTeacherPaletteView()
	{
		TeacherPaletteView.Filter = FilterTeacherPaletteItem;
		TeacherPaletteView.Refresh();
	}

	private bool FilterTeacherPaletteItem(object obj)
	{
		if (!(obj is TeacherPaletteItem teacherPaletteItem))
		{
			return false;
		}
		string text = TeacherPaletteSearchText.Trim();
		if (text.Length > 0 && !teacherPaletteItem.Teacher.FullName.Contains(text, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return TeacherPaletteMetrics.MatchesTypeFilter(teacherPaletteItem.Teacher, TeacherPaletteFilterPrimary, TeacherPaletteFilterSubject, TeacherPaletteFilterAuxiliary, TeacherPaletteFilterUnassigned);
	}

	private async Task RefreshWeeklyLoadChartAsync()
	{
		WeeklyLoadChartPoints.Clear();
		WeeklyLoadChartPointsLarge.Clear();
		WeeklyLoadPolylinePoints = "";
		WeeklyLoadPolylinePointsLarge = "";
		WorkloadChartWeekTotal = 0.0;
		if (SelectedTemplate == null || WorkloadChartClass == null)
		{
			WorkloadChartCaption = "Выберите класс для графика нагрузки";
			return;
		}
		List<LessonSlot> weekSlots = await LoadCurrentTemplateWeekSlotsAsync();
		Dictionary<int, double> difficulty = BuildSubjectDifficultyForClass(WorkloadChartClass.Id, WorkloadChartClass.Grade);
		WeeklyLoadChartResult compact = WeeklyLoadChartBuilder.Build(weekSlots, difficulty, WorkloadChartClass.Id, WorkloadChartClass.DisplayName);
		WeeklyLoadChartResult large = WeeklyLoadChartBuilder.Build(weekSlots, difficulty, WorkloadChartClass.Id, WorkloadChartClass.DisplayName, 620.0, 140.0);
		foreach (WeeklyLoadChartPoint point in compact.Points)
		{
			WeeklyLoadChartPoints.Add(point);
		}
		foreach (WeeklyLoadChartPoint point2 in large.Points)
		{
			WeeklyLoadChartPointsLarge.Add(point2);
		}
		WeeklyLoadPolylinePoints = compact.PolylinePoints;
		WeeklyLoadPolylinePointsLarge = large.PolylinePoints;
		WorkloadChartWeekTotal = compact.WeekTotal;
		WorkloadChartCaption = ((compact.WeekTotal > 0.0) ? $"{compact.ClassName} · за неделю {compact.WeekTotal:0.##} · макс. за день {compact.MaxScore:0.##}" : (compact.ClassName + " · уроков в шаблоне пока нет"));
	}

	private async Task RefreshAllClassWorkloadChartsAsync()
	{
		ClassWorkloadCharts.Clear();
		OnPropertyChanged("HasClassWorkloadCharts");
		if (SelectedTemplate == null)
		{
			SivkovChartsSummary = "Выберите шаблон недели на вкладке «Сетка»";
			return;
		}
		IReadOnlyList<ClassWeeklyLoadChartCard> cards = AllClassWeeklyLoadChartsBuilder.Build(weekSlots: await LoadCurrentTemplateWeekSlotsAsync(), classes: ClassList, difficultyForClass: BuildSubjectDifficultyForClass);
		foreach (ClassWeeklyLoadChartCard card in cards)
		{
			ClassWorkloadCharts.Add(card);
		}
		OnPropertyChanged("HasClassWorkloadCharts");
		int withLessons = cards.Count((ClassWeeklyLoadChartCard c) => c.WeekTotal > 0.0);
		int overloaded = cards.Count((ClassWeeklyLoadChartCard c) => c.HasDailyOverload);
		SivkovChartsSummary = ((withLessons == 0) ? ("Шаблон «" + SelectedTemplate.Name + "» · уроков пока нет") : ((overloaded > 0) ? $"Шаблон «{SelectedTemplate.Name}» · {withLessons} классов · ⚠ превышение в {overloaded} классах" : $"Шаблон «{SelectedTemplate.Name}» · {withLessons} классов с уроками"));
	}

	private async Task<List<LessonSlot>> LoadCurrentTemplateWeekSlotsAsync()
	{
		if (SelectedTemplate == null)
		{
			return new List<LessonSlot>();
		}
		List<LessonSlot> weekSlots = new List<LessonSlot>();
		for (int day = 1; day <= 6; day++)
		{
			List<LessonSlot> list = weekSlots;
			list.AddRange(await _templates.GetSlotsForTemplateDayAsync(SelectedTemplate.Id, day));
		}
		return weekSlots;
	}

	private Dictionary<int, double> BuildSubjectDifficultyForClass(int classId, int grade)
	{
		return (from p in PaletteItems
			where p.ClassId == classId
			group p by p.SubjectId).ToDictionary((IGrouping<int, CurriculumItem> g) => g.Key, (IGrouping<int, CurriculumItem> g) => OfficialSubjectDifficultyReference.ResolveForClass(g.First().SubjectName, grade, g.First().SubjectDifficultyScore));
	}

	private Subject? ResolveDynamicPauseSubjectForClass(int classId)
	{
		return DynamicPauseScheduleHelper.FindSubjectForClass(classId, PaletteItems, SubjectList);
	}

	public async Task DropTeacherAsync(GridCell target, TeacherDragData payload)
	{
		if (SelectedTemplate == null)
		{
			return;
		}
		Teacher teacher = TeacherList.FirstOrDefault((Teacher t) => t.Id == payload.TeacherId);
		if (teacher == null)
		{
			return;
		}
		int day = ResolveCellDay(target);
		Subject pauseSubject;
		WeekTemplateSlotRecord existingPause;
		int num2;
		int? num;
		if (target.IsDynamicPauseColumn)
		{
			pauseSubject = ResolveDynamicPauseSubjectForClass(target.ClassId);
			if (pauseSubject == null)
			{
				_dialogs.ShowInfo("Конструктор", "Для этого класса нет «Динамической паузы» в нагрузке. Добавьте строку в справочниках → Нагрузка.");
				return;
			}
			existingPause = await _templates.FindSlotAtAsync(SelectedTemplate.Id, day, target.LessonNumber, target.ClassId);
			num = existingPause?.TeacherId;
			if (num.HasValue)
			{
				int tid = num.GetValueOrDefault();
				if (tid > 0)
				{
					num2 = ((tid != teacher.Id) ? 1 : 0);
					goto IL_0281;
				}
			}
			num2 = 0;
			goto IL_0281;
		}
		WeekTemplateSlotRecord existing = await _templates.FindSlotAtAsync(SelectedTemplate.Id, day, target.LessonNumber, target.ClassId);
		ConstructorViewModel constructorViewModel = this;
		int classId = target.ClassId;
		int lessonNumber = target.LessonNumber;
		int valueOrDefault = (existing?.SubjectId).GetValueOrDefault();
		int id = teacher.Id;
		string fullName = teacher.FullName;
		int? roomId = existing?.RoomId;
		num = existing?.RoomId;
		object roomNumber;
		if (num.HasValue)
		{
			int rid = num.GetValueOrDefault();
			roomNumber = RoomList.FirstOrDefault((Room r) => r.Id == rid)?.Number;
		}
		else
		{
			roomNumber = null;
		}
		LessonSlot proposed = constructorViewModel.BuildProposedSlot(classId, lessonNumber, valueOrDefault, "", id, fullName, roomId, (string?)roomNumber, 0, existing?.SlotId, day);
		if (!WarnAndBlockSave(await GetConflictsForProposedAsync(proposed)) && await ConfirmTransitionWarningsAsync(proposed) && await ConfirmUnavailabilityWarningsAsync(proposed))
		{
			await _templates.UpsertSlotAsync(SelectedTemplate.Id, day, target.LessonNumber, target.ClassId, existing?.SubjectId, teacher.Id, existing?.RoomId);
			await SyncTeacherClassPreferencesAsync(target.ClassId, existing?.TeacherId, teacher.Id);
			num = existing?.SubjectId;
			int subjectId = default(int);
			int num3;
			if (num.HasValue)
			{
				subjectId = num.GetValueOrDefault();
				num3 = 1;
			}
			else
			{
				num3 = 0;
			}
			if (num3 != 0)
			{
				await SyncTeacherCurriculumAsync(target.ClassId, subjectId, existing.TeacherId, teacher.Id);
			}
			_saveState.MarkDirty();
			MarkScheduleChanged();
			await RefreshGridAsync();
			ReselectGridCell(target.ClassId, target.LessonNumber, day);
			StatusMessage = "Укажите предмет и кабинет справа, затем нажмите «Сохранить»";
		}
		return;
		IL_0281:
		if (num2 != 0)
		{
			_dialogs.ShowInfo("Конструктор", "На дин. паузе уже назначен другой педагог.");
			return;
		}
		LessonSlot proposedPause = BuildProposedSlot(target.ClassId, target.LessonNumber, pauseSubject.Id, pauseSubject.Name, teacher.Id, teacher.FullName, null, null, 0, existingPause?.SlotId, day);
		if (!WarnAndBlockSave(await GetConflictsForProposedAsync(proposedPause)) && await ConfirmUnavailabilityWarningsAsync(proposedPause))
		{
			await _templates.UpsertSlotAsync(SelectedTemplate.Id, day, target.LessonNumber, target.ClassId, pauseSubject.Id, teacher.Id, null);
			await SyncTeacherClassPreferencesAsync(target.ClassId, existingPause?.TeacherId, teacher.Id);
			await SyncTeacherCurriculumAsync(target.ClassId, pauseSubject.Id, existingPause?.TeacherId, teacher.Id);
			_saveState.MarkDirty();
			MarkScheduleChanged();
			await RefreshGridAsync();
			ReselectGridCell(target.ClassId, target.LessonNumber, day);
			StatusMessage = "Дин. пауза · " + target.ClassName + " — педагог назначен (кабинет не нужен)";
		}
	}

	public async Task DropSubjectAsync(GridCell target, SubjectDragData payload)
	{
		if (SelectedTemplate == null)
		{
			return;
		}
		if (target.ClassId != payload.ClassId)
		{
			_dialogs.ShowInfo("Конструктор", "Предмет для " + payload.ClassName + " — перетащите в столбец этого класса.");
			return;
		}
		if (target.IsDynamicPauseColumn)
		{
			_dialogs.ShowInfo("Конструктор", "Дин. паузу перетащите в строку «Дин. пауза».");
			return;
		}
		int day = ResolveCellDay(target);
		WeekTemplateSlotRecord existing = await _templates.FindSlotAtAsync(SelectedTemplate.Id, day, target.LessonNumber, target.ClassId);
		if (existing == null || existing.TeacherId <= 0)
		{
			_dialogs.ShowInfo("Конструктор", "Сначала назначьте педагога на этот урок (этап 1).");
			return;
		}
		Teacher teacher = TeacherList.FirstOrDefault((Teacher t) => t.Id == existing.TeacherId);
		if (teacher == null || !ValidateTimelineCellSubject(target, payload.SubjectName))
		{
			return;
		}
		ConstructorViewModel constructorViewModel = this;
		int classId = target.ClassId;
		int lessonNumber = target.LessonNumber;
		int subjectId = payload.SubjectId;
		string subjectName = payload.SubjectName;
		int id = teacher.Id;
		string fullName = teacher.FullName;
		int? roomId = existing.RoomId;
		int? roomId2 = existing.RoomId;
		object roomNumber;
		if (roomId2.HasValue)
		{
			int rid = roomId2.GetValueOrDefault();
			roomNumber = RoomList.FirstOrDefault((Room r) => r.Id == rid)?.Number;
		}
		else
		{
			roomNumber = null;
		}
		LessonSlot proposed = constructorViewModel.BuildProposedSlot(classId, lessonNumber, subjectId, subjectName, id, fullName, roomId, (string?)roomNumber, 0, existing.SlotId, day);
		if (!WarnAndBlockSave(await GetConflictsForProposedAsync(proposed)))
		{
			await _templates.UpsertSlotAsync(SelectedTemplate.Id, day, target.LessonNumber, target.ClassId, payload.SubjectId, teacher.Id, existing.RoomId);
			_saveState.MarkDirty();
			MarkScheduleChanged();
			await RefreshGridAsync();
		}
	}

	public async Task DropRoomAsync(GridCell target, RoomDragData payload)
	{
		if (SelectedTemplate == null)
		{
			return;
		}
		if (target.IsDynamicPauseColumn)
		{
			_dialogs.ShowInfo("Конструктор", "Для дин. паузы кабинет необязателен.");
			return;
		}
		int day = ResolveCellDay(target);
		WeekTemplateSlotRecord existing = await _templates.FindSlotAtAsync(SelectedTemplate.Id, day, target.LessonNumber, target.ClassId);
		if (existing == null || existing.TeacherId <= 0)
		{
			_dialogs.ShowInfo("Конструктор", "Сначала назначьте педагога (этап 1).");
			return;
		}
		int? subjectId = existing.SubjectId;
		bool flag;
		if (subjectId.HasValue)
		{
			int valueOrDefault = subjectId.GetValueOrDefault();
			if (valueOrDefault > 0)
			{
				flag = false;
				goto IL_0201;
			}
		}
		flag = true;
		goto IL_0201;
		IL_0201:
		if (flag)
		{
			_dialogs.ShowInfo("Конструктор", "Сначала назначьте предмет (этап 2).");
			return;
		}
		Teacher teacher = TeacherList.FirstOrDefault((Teacher t) => t.Id == existing.TeacherId);
		Subject subject = SubjectList.FirstOrDefault((Subject s) => s.Id == existing.SubjectId);
		if (teacher == null || subject == null)
		{
			return;
		}
		Room room = RoomList.FirstOrDefault((Room r) => r.Id == payload.RoomId);
		if (room != null)
		{
			LessonSlot proposed = BuildProposedSlot(target.ClassId, target.LessonNumber, subject.Id, subject.Name, teacher.Id, teacher.FullName, room.Id, room.Number, 0, existing.SlotId, day);
			if (!WarnAndBlockSave(await GetConflictsForProposedAsync(proposed)) && await ConfirmTransitionWarningsAsync(proposed))
			{
				await _templates.UpsertSlotAsync(SelectedTemplate.Id, day, target.LessonNumber, target.ClassId, subject.Id, teacher.Id, room.Id);
				_saveState.MarkDirty();
				MarkScheduleChanged();
				await RefreshGridAsync();
			}
		}
	}

	private static DateTime? ParseStoredDate(string? value)
	{
		DateTime result;
		return DateTime.TryParse(value, out result) ? new DateTime?(result.Date) : ((DateTime?)null);
	}

	private static string FormatStoredDate(DateTime? date)
	{
		return date?.ToString("yyyy-MM-dd") ?? "";
	}

	partial void OnSelectedTemplateChanged(WeekTemplateInfo? value)
	{
		SelectedTemplateParity = value?.WeekParity ?? "Any";
		OnPropertyChanged("TemplateSectionHint");
		if (!_isReloadingReferenceData)
		{
			RefreshTemplateViewAsync();
		}
	}

	partial void OnSelectedDayIndexChanged(int value)
	{
		if (!IsSingleClassWeekMode && !IsAllClassesWeekMode)
		{
			RefreshGridAsync();
			OnPropertyChanged("HeaderHint");
			OnPropertyChanged("CellEditorTitle");
		}
	}

	partial void OnSelectedCellChanged(GridCell? value)
	{
		if (_highlightedGridCell != null && _highlightedGridCell != value)
		{
			_highlightedGridCell.IsSelected = false;
		}
		if (value != null)
		{
			value.IsSelected = true;
			_highlightedGridCell = value;
		}
		else
		{
			_highlightedGridCell = null;
		}
		if (value != null && SelectedSubgroupIndex != 0)
		{
			_resettingSubgroupForCellChange = true;
			try
			{
				SelectedSubgroupIndex = 0;
			}
			finally
			{
				_resettingSubgroupForCellChange = false;
			}
		}
		EnterEditPanelSyncSuppress();
		try
		{
			if (!_preparingDropEdit)
			{
				LoadEditFromPart();
			}
			RebuildTeacherPickerItems();
		}
		finally
		{
			ExitEditPanelSyncSuppress();
		}
		if (!_preparingDropEdit)
		{
			SyncTeacherPickerSelection();
		}
		OnPropertyChanged("CellEditorTitle");
		OnPropertyChanged("HeaderHint");
		OnPropertyChanged("SubgroupEditorHint");
		if (value != null && (IsAllClassesDayMode || IsAllClassesWeekMode))
		{
			SchoolClass schoolClass = ClassList.FirstOrDefault((SchoolClass c) => c.Id == value.ClassId);
			if (schoolClass != null && WorkloadChartClass?.Id != schoolClass.Id)
			{
				WorkloadChartClass = schoolClass;
			}
		}
		if (IsAllClassesDayMode && _bellPeriods.Count > 0)
		{
			RefreshStandardSectionHeaders(ResolveDayGridBellReferenceClass());
		}
		RefreshWeeklyLoadChartAsync();
		RefreshEditConflictHintAsync();
	}

	partial void OnEditSubjectChanged(Subject? value)
	{
		if (value != null && SubjectSearchText != value.Name)
		{
			SubjectSearchText = value.Name;
		}
		if (!IsEditPanelSyncSuppressed)
		{
			ApplyRoomRulesForCurrentSubject();
			RebuildTeacherPickerItems();
			TrySuggestTeacherForCell();
			SyncTeacherPickerSelection();
			RefreshEditConflictHintAsync();
		}
	}

	partial void OnSubjectSearchTextChanged(string value)
	{
		UpdateSubjectSuggestion();
		if (IsEditPanelSyncSuppressed)
		{
			return;
		}
		string trimmed = value.Trim();
		if (EditSubject != null && SubjectNamesMatch(EditSubject.Name, trimmed))
		{
			ApplyRoomRulesForCurrentSubject();
			RebuildTeacherPickerItems();
			TrySuggestTeacherForCell();
			RefreshEditConflictHintAsync();
			return;
		}
		Subject subject = SubjectList.FirstOrDefault((Subject s) => SubjectNamesMatch(s.Name, trimmed));
		if (subject != null)
		{
			EditSubject = subject;
		}
		else if (EditSubject != null)
		{
			EditSubject = null;
		}
		ApplyRoomRulesForCurrentSubject();
		RebuildTeacherPickerItems();
		TrySuggestTeacherForCell();
		RefreshEditConflictHintAsync();
	}

	partial void OnEditTeacherChanged(Teacher? value)
	{
		if (!IsEditPanelSyncSuppressed)
		{
			TryApplyClassDefaultRoom();
			RefreshEditConflictHintAsync();
		}
	}

	partial void OnSelectedTeacherPickerItemChanged(TeacherPickerItem? value)
	{
		if (!IsEditPanelSyncSuppressed && value?.Teacher != EditTeacher)
		{
			EditTeacher = value?.Teacher;
		}
	}

	partial void OnEditRoomChanged(Room? value)
	{
		OnPropertyChanged("EditRoomBuildingColorHex");
		RefreshEditConflictHintAsync();
	}

	partial void OnSelectedPeriodTypeChanged(string value)
	{
		if (!_suppressPeriodNameAutofill && SelectedPeriod == null)
		{
			PeriodName = PeriodTypes.ToDisplay(value);
		}
	}

	partial void OnSelectedRecurrenceChanged(string value)
	{
		OnPropertyChanged("PeriodRecurrenceHint");
	}

	partial void OnSelectedCalTypeChanged(string value)
	{
		OnPropertyChanged("IsCalDonorDayVisible");
	}

	partial void OnCalDonorDayChanged(int value)
	{
		OnPropertyChanged("CalDonorDayIndex");
	}

	partial void OnSelectedSubgroupIndexChanged(int value)
	{
		if (!_resettingSubgroupForCellChange)
		{
			EnterEditPanelSyncSuppress();
			try
			{
				LoadEditFromPart();
				RebuildTeacherPickerItems();
			}
			finally
			{
				ExitEditPanelSyncSuppress();
			}
			SyncTeacherPickerSelection();
			OnPropertyChanged("SubgroupEditorHint");
			RefreshEditConflictHintAsync();
		}
	}

	partial void OnComplianceShowErrorsChanged(bool value)
	{
		ApplyComplianceFilters();
	}

	partial void OnComplianceShowWarningsChanged(bool value)
	{
		ApplyComplianceFilters();
	}

	partial void OnComplianceShowInfoChanged(bool value)
	{
		ApplyComplianceFilters();
	}

	partial void OnComplianceFilterClassChanged(SchoolClass? value)
	{
		ApplyComplianceFilters();
	}

	partial void OnComplianceSearchTextChanged(string value)
	{
		ApplyComplianceFilters();
	}

	partial void OnComplianceSortModeChanged(string value)
	{
		ApplyComplianceFilters();
	}

	partial void OnComplianceShowDismissedChanged(bool value)
	{
		ApplyComplianceFilters();
	}

	partial void OnSelectedPeriodChanged(SchedulePeriodInfo? value)
	{
		LoadPeriodForm(value);
	}

	partial void OnSelectedCalendarEntryChanged(CalendarEntry? value)
	{
		LoadCalendarForm(value);
	}

	partial void OnActiveTabIndexChanged(int value)
	{
		OnPropertyChanged("IsScheduleTab");
		OnPropertyChanged("IsSivkovChartsTab");
		OnPropertyChanged("HeaderHint");
		OnPropertyChanged("ContextHelpTip");
		if (value == 2)
		{
			RefreshComplianceAsync();
		}
		if (value == 3)
		{
			RefreshAllClassWorkloadChartsAsync();
		}
	}

	partial void OnPaletteGroupModeChanged(string value)
	{
		ApplyPaletteView();
	}

	partial void OnPaletteSortModeChanged(string value)
	{
		ApplyPaletteView();
	}

	partial void OnSubjectPaletteClassFilterChanged(SubjectPaletteClassFilter? value)
	{
		RefreshWorkflowPalettesAsync();
	}

	partial void OnGridViewModeChanged(string value)
	{
		OnPropertyChanged("IsAllClassesDayMode");
		OnPropertyChanged("IsSingleClassWeekMode");
		OnPropertyChanged("IsAllClassesWeekMode");
		OnPropertyChanged("GridViewModeHint");
		OnPropertyChanged("ShowSubjectPaletteClassFilter");
		if (IsSingleClassWeekMode && FocusClass == null)
		{
			SchoolClass schoolClass2 = (FocusClass = ((SelectedCell != null) ? ClassList.FirstOrDefault((SchoolClass c) => c.Id == SelectedCell.ClassId) : ClassList.FirstOrDefault()));
		}
		RefreshGridAsync();
	}

	partial void OnFocusClassChanged(SchoolClass? value)
	{
		_suppressFocusClassRoomSync = true;
		_suppressFocusClassPeRoomSync = true;
		try
		{
			int? num = value?.DefaultRoomId;
			object focusClassDefaultRoom;
			if (num.HasValue)
			{
				int rid = num.GetValueOrDefault();
				focusClassDefaultRoom = RoomList.FirstOrDefault((Room r) => r.Id == rid);
			}
			else
			{
				focusClassDefaultRoom = null;
			}
			FocusClassDefaultRoom = (Room?)focusClassDefaultRoom;
			num = value?.DefaultPeRoomId;
			object focusClassDefaultPeRoom;
			if (num.HasValue)
			{
				int peId = num.GetValueOrDefault();
				focusClassDefaultPeRoom = RoomList.FirstOrDefault((Room r) => r.Id == peId);
			}
			else
			{
				focusClassDefaultPeRoom = null;
			}
			FocusClassDefaultPeRoom = (Room?)focusClassDefaultPeRoom;
		}
		finally
		{
			_suppressFocusClassRoomSync = false;
			_suppressFocusClassPeRoomSync = false;
		}
		if (IsSingleClassWeekMode && value != null)
		{
			WorkloadChartClass = value;
		}
		OnPropertyChanged("ShowSubjectPaletteClassFilter");
		RefreshWorkflowPalettesAsync();
		if (IsSingleClassWeekMode)
		{
			RefreshGridAsync();
		}
	}

	partial void OnFocusClassDefaultRoomChanged(Room? value)
	{
		if (!_isReloadingReferenceData && !_suppressFocusClassRoomSync && FocusClass != null)
		{
			int? num = value?.Id;
			if (FocusClass.DefaultRoomId != num)
			{
				FocusClass.DefaultRoomId = num;
				FocusClass.DefaultRoomDisplay = ((value == null) ? "" : (value.Number + " · " + value.BuildingName));
				SaveFocusClassDefaultRoomAsync();
			}
		}
	}

	partial void OnFocusClassDefaultPeRoomChanged(Room? value)
	{
		if (!_isReloadingReferenceData && !_suppressFocusClassPeRoomSync && FocusClass != null)
		{
			int? num = value?.Id;
			if (FocusClass.DefaultPeRoomId != num)
			{
				FocusClass.DefaultPeRoomId = num;
				FocusClass.DefaultPeRoomDisplay = ((value == null) ? "" : (value.Number + " · " + value.BuildingName));
				SaveFocusClassDefaultPeRoomAsync();
			}
		}
	}

	partial void OnShowAllBalanceRowsChanged(bool value)
	{
		ApplyBalanceView();
	}

	partial void OnWorkflowStepChanged(string value)
	{
		OnPropertyChanged("IsTeachersWorkflow");
		OnPropertyChanged("IsSubjectsWorkflow");
		OnPropertyChanged("IsRoomsWorkflow");
		OnPropertyChanged("ShowSubjectPaletteClassFilter");
		OnPropertyChanged("WorkflowStepHint");
		RefreshWorkflowPalettesAsync();
	}

	partial void OnIsGridFullscreenChanged(bool value)
	{
		OnPropertyChanged("FullscreenToggleLabel");
	}

	partial void OnWorkloadChartClassChanged(SchoolClass? value)
	{
		OnPropertyChanged("HasWorkloadChartClass");
		RefreshWeeklyLoadChartAsync();
	}

	partial void OnIsWorkloadChartPopupOpenChanged(bool value)
	{
		OnPropertyChanged("WorkloadChartExpandLabel");
	}

	partial void OnTeacherPaletteSearchTextChanged(string value)
	{
		ApplyTeacherPaletteView();
	}

	partial void OnTeacherPaletteFilterPrimaryChanged(bool value)
	{
		ApplyTeacherPaletteView();
	}

	partial void OnTeacherPaletteFilterSubjectChanged(bool value)
	{
		ApplyTeacherPaletteView();
	}

	partial void OnTeacherPaletteFilterAuxiliaryChanged(bool value)
	{
		ApplyTeacherPaletteView();
	}

	partial void OnTeacherPaletteFilterUnassignedChanged(bool value)
	{
		ApplyTeacherPaletteView();
	}

	partial void OnSelectedPlacementTeacherChanged(TeacherPaletteItem? value)
	{
		RefreshPlacementSelectionUi();
		OnPropertyChanged("SelectedPlacementTeacherCaption");
	}
}




