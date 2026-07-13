using System.Collections.ObjectModel;
using ArmZavuch.Models;
using ArmZavuch.Services.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace ArmZavuch.ViewModels;

/// <summary>Выборочная и полная выгрузка/загрузка данных (вкладка «Настройки»).</summary>
public partial class DirectoriesViewModel
{
    public ObservableCollection<AppDataTransferSectionItem> TransferSections { get; } =
        new(AppDataSectionCatalog.CreateUiItems());

    [ObservableProperty] private AppDataImportMode _transferImportMode = AppDataImportMode.Merge;

    public bool IsTransferMergeMode
    {
        get => TransferImportMode == AppDataImportMode.Merge;
        set
        {
            if (value && TransferImportMode != AppDataImportMode.Merge)
                TransferImportMode = AppDataImportMode.Merge;
        }
    }

    public bool IsTransferReplaceMode
    {
        get => TransferImportMode == AppDataImportMode.Replace;
        set
        {
            if (value && TransferImportMode != AppDataImportMode.Replace)
                TransferImportMode = AppDataImportMode.Replace;
        }
    }

    public IAsyncRelayCommand ExportSelectiveAppDataCommand { get; private set; } = null!;
    public IAsyncRelayCommand ImportSelectiveAppDataCommand { get; private set; } = null!;
    public IRelayCommand SelectAllTransferSectionsCommand { get; private set; } = null!;
    public IRelayCommand SelectDirectoryTransferSectionsCommand { get; private set; } = null!;
    public IRelayCommand ClearTransferSectionsCommand { get; private set; } = null!;

    private void InitDataTransferCommands()
    {
        ExportSelectiveAppDataCommand = new AsyncRelayCommand(ExportSelectiveAppDataAsync);
        ImportSelectiveAppDataCommand = new AsyncRelayCommand(ImportSelectiveAppDataAsync);
        SelectAllTransferSectionsCommand = new RelayCommand(() => SetTransferSectionsSelected(true, availableOnly: false));
        SelectDirectoryTransferSectionsCommand = new RelayCommand(SelectDirectoryTransferSections);
        ClearTransferSectionsCommand = new RelayCommand(() => SetTransferSectionsSelected(false, availableOnly: false));
    }

    private static readonly AppDataTransferSection[] DirectorySections =
    [
        AppDataTransferSection.Buildings,
        AppDataTransferSection.Subjects,
        AppDataTransferSection.Classes,
        AppDataTransferSection.Teachers,
        AppDataTransferSection.Rooms,
        AppDataTransferSection.Curriculum,
        AppDataTransferSection.Bells
    ];

    private void SelectDirectoryTransferSections()
    {
        foreach (var item in TransferSections)
            item.IsSelected = item.IsAvailable && DirectorySections.Contains(item.Section);
    }

    private void SetTransferSectionsSelected(bool selected, bool availableOnly)
    {
        foreach (var item in TransferSections)
        {
            if (!availableOnly || item.IsAvailable)
                item.IsSelected = selected;
        }
    }

    private IReadOnlyList<AppDataTransferSection> GetSelectedTransferSections() =>
        TransferSections.Where(i => i.IsSelected && i.IsAvailable).Select(i => i.Section).ToList();

    private void ApplyArchiveAvailability(AppDataTransferManifest? manifest)
    {
        var available = manifest?.ResolveExportedSections().ToHashSet()
            ?? Enum.GetValues<AppDataTransferSection>().ToHashSet();

        foreach (var item in TransferSections)
        {
            item.IsAvailable = available.Contains(item.Section);
            if (!item.IsAvailable)
                item.IsSelected = false;
        }
    }

    private void ResetTransferSectionAvailability()
    {
        foreach (var item in TransferSections)
            item.IsAvailable = true;
    }

    partial void OnTransferImportModeChanged(AppDataImportMode value)
    {
        OnPropertyChanged(nameof(IsTransferMergeMode));
        OnPropertyChanged(nameof(IsTransferReplaceMode));
    }

    private async Task ExportSelectiveAppDataAsync()
    {
        var sections = GetSelectedTransferSections();
        if (sections.Count == 0)
        {
            _dialogs.ShowWarning("Выборочная выгрузка", "Отметьте хотя бы один раздел в списке ниже.");
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = $"Архив {AppBranding.ProductName} (*{AppDataTransferService.FileExtension})|*{AppDataTransferService.FileExtension}",
            FileName = AppDataSectionCatalog.SuggestPartialFileName(_settings.SchoolName, sections),
            Title = "Выборочная выгрузка данных"
        };
        if (dlg.ShowDialog() != true)
            return;

        var result = await _appDataTransfer.ExportSelectiveAsync(dlg.FileName, sections);
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

    private async Task ImportSelectiveAppDataAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = $"Архив {AppBranding.ProductName} (*{AppDataTransferService.FileExtension})|*{AppDataTransferService.FileExtension}",
            Title = "Выборочная загрузка данных"
        };
        if (dlg.ShowDialog() != true)
            return;

        var manifest = await _appDataTransfer.ReadManifestAsync(dlg.FileName);
        if (manifest is null)
        {
            _dialogs.ShowError("Загрузка данных", $"Не удалось прочитать архив. Проверьте, что это файл выгрузки {AppBranding.ProductName}.");
            return;
        }

        ApplyArchiveAvailability(manifest);

        var sections = GetSelectedTransferSections();
        if (sections.Count == 0)
        {
            SetTransferSectionsSelected(true, availableOnly: true);
            sections = GetSelectedTransferSections();
        }

        if (sections.Count == 0)
        {
            _dialogs.ShowWarning(
                "Выборочная загрузка",
                "Отметьте разделы для загрузки. Доступны только те разделы, которые есть в выбранном архиве.");
            return;
        }

        if (!_dialogs.ConfirmSelectiveImportAppData(manifest, sections, TransferImportMode))
            return;

        var result = await _appDataTransfer.ImportSelectiveAsync(dlg.FileName, sections, TransferImportMode);
        ResetTransferSectionAvailability();

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
}
