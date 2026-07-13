using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using ArmZavuch.Models;
using ArmZavuch.Services.Save;
using ArmZavuch.Services.Settings;
using ArmZavuch.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace ArmZavuch.ViewModels;

/// <summary>
/// Главная ViewModel: навигация между модулями, статус сохранения, команда Save.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ISaveStateService _saveState;
    private readonly IServiceProvider _services;
    private readonly AppSettingsService _settings;
    private readonly OverviewViewModel _overview;
    private readonly RoomsViewModel _rooms;
    private readonly DispatcherViewModel _dispatcher;
    private readonly ConstructorViewModel _constructor;
    private readonly DirectoriesViewModel _directories;
    private bool _suppressNavChange;
    private string _currentModuleKey = "Dispatcher";

    [ObservableProperty]
    private object _currentView;

    [ObservableProperty]
    private NavEntry? _selectedNav;

    public ObservableCollection<NavEntry> NavItems { get; } =
    [
        new() { Key = "Dispatcher", Title = "Диспетчерская", Icon = "📋", Hint = "Замены на день" },
        new() { Key = "Overview", Title = "Сводка", Icon = "📊", Hint = "Картина на неделю" },
        new() { Key = "Constructor", Title = "Конструктор", Icon = "🗓", Hint = "Сетка и календарь" },
        new() { Key = "Rooms", Title = "Кабинеты", Icon = "🏫", Hint = "Схема зданий" },
        new() { Key = "Directories", Title = "Справочники", Icon = "📁", Hint = "Классы, люди, нагрузка" },
        new() { Key = "Instructions", Title = "Инструкция", Icon = "📖", Hint = "Порядок работы" }
    ];

    public string WindowTitle => $"{AppBranding.ProductName} — {_settings.SchoolName}";

    public string SaveStatusText => _saveState.IsDirty ? "Есть несохранённые изменения" : "Все изменения сохранены";

    public string SaveStatusShort => _saveState.IsDirty ? "●" : "✓";

    public Brush SaveStatusBrush => _saveState.IsDirty
        ? new SolidColorBrush(Color.FromRgb(245, 158, 11))
        : new SolidColorBrush(Color.FromRgb(34, 197, 94));

    public ICommand SaveCommand { get; }

    public MainViewModel(
        ISaveStateService saveState,
        IServiceProvider services,
        AppSettingsService settings,
        OverviewViewModel overview,
        RoomsViewModel rooms,
        DispatcherViewModel dispatcher,
        ConstructorViewModel constructor,
        DirectoriesViewModel directories)
    {
        _saveState = saveState;
        _services = services;
        _settings = settings;
        _overview = overview;
        _rooms = rooms;
        _dispatcher = dispatcher;
        _constructor = constructor;
        _directories = directories;
        _saveState.DirtyStateChanged += OnDirtyStateChanged;
        _settings.SchoolNameChanged += OnSchoolNameChanged;

        _currentView = _services.GetRequiredService<DispatcherView>();
        _selectedNav = NavItems[0];

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        _ = _dispatcher.ActivateAsync();
    }

    partial void OnSelectedNavChanged(NavEntry? value)
    {
        if (_suppressNavChange || value is null)
            return;
        _ = NavigateAsync(value.Key);
    }

    public void NavigateToModule(string module) => _ = NavigateAsync(module);

    private async Task NavigateAsync(string module)
    {
        if (_currentModuleKey == "Directories" && module != "Directories")
            await _directories.PrepareForDeactivateAsync();

        switch (module)
        {
            case "Overview":
                _ = _overview.ActivateAsync();
                CurrentView = _services.GetRequiredService<OverviewView>();
                break;
            case "Rooms":
                _ = _rooms.ActivateAsync();
                CurrentView = _services.GetRequiredService<RoomsView>();
                break;
            case "Constructor":
                _ = _constructor.ActivateAsync();
                CurrentView = _services.GetRequiredService<ConstructorView>();
                break;
            case "Directories":
                _ = _directories.ActivateAsync();
                CurrentView = _services.GetRequiredService<DirectoriesView>();
                break;
            case "Instructions":
                CurrentView = _services.GetRequiredService<InstructionsView>();
                break;
            default:
                _ = _dispatcher.ActivateAsync();
                CurrentView = _services.GetRequiredService<DispatcherView>();
                break;
        }

        var nav = NavItems.FirstOrDefault(n => n.Key == module);
        if (nav is not null && !ReferenceEquals(SelectedNav, nav))
        {
            _suppressNavChange = true;
            SelectedNav = nav;
            _suppressNavChange = false;
        }

        _currentModuleKey = module;
    }

    private async Task SaveAsync()
    {
        await _directories.WaitPendingCurriculumGridSavesAsync();
        await _saveState.SaveAsync();
        OnPropertyChanged(nameof(SaveStatusText));
        OnPropertyChanged(nameof(SaveStatusBrush));
        OnPropertyChanged(nameof(SaveStatusShort));
    }

    private void OnDirtyStateChanged()
    {
        OnPropertyChanged(nameof(SaveStatusText));
        OnPropertyChanged(nameof(SaveStatusBrush));
        OnPropertyChanged(nameof(SaveStatusShort));
    }

    private void OnSchoolNameChanged() => OnPropertyChanged(nameof(WindowTitle));
}
