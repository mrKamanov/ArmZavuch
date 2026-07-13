using ArmZavuch.Data;
using ArmZavuch.Models;
using ArmZavuch.Services.Catalog;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ArmZavuch.ViewModels;

/// <summary>Редактор пользовательских шаблонов нагрузки на вкладке «Нагрузка».</summary>
public partial class DirectoriesViewModel
{
    public ObservableCollection<CurriculumTemplateItem> EditCurriculumTemplateItems { get; } = [];

    private string _editCurriculumTemplateName = "";
    private string _editCurriculumTemplateGrade = "1";
    private CurriculumTemplateItem? _selectedEditTemplateItem;
    private Subject? _newTemplateItemSubject;
    private string _newTemplateItemHours = "1";
    private string _newTemplateItemDifficulty =
        OfficialSubjectDifficultyReference.FormatScore(OfficialSubjectDifficultyReference.DefaultFallback);
    private bool _newTemplateItemHasSubgroups;
    private string _newTemplateItemWeekParity = CurriculumWeekParity.EveryWeek;

    public string EditCurriculumTemplateName
    {
        get => _editCurriculumTemplateName;
        set => SetProperty(ref _editCurriculumTemplateName, value);
    }

    public string EditCurriculumTemplateGrade
    {
        get => _editCurriculumTemplateGrade;
        set => SetProperty(ref _editCurriculumTemplateGrade, value);
    }

    public CurriculumTemplateItem? SelectedEditTemplateItem
    {
        get => _selectedEditTemplateItem;
        set
        {
            if (SetProperty(ref _selectedEditTemplateItem, value))
                RemoveCurriculumTemplateItemCommand.NotifyCanExecuteChanged();
        }
    }

    public Subject? NewTemplateItemSubject
    {
        get => _newTemplateItemSubject;
        set
        {
            if (SetProperty(ref _newTemplateItemSubject, value) && value is not null)
                NewTemplateItemDifficulty = OfficialSubjectDifficultyReference.FormatScore(value.DifficultyScore);
        }
    }

    public string NewTemplateItemHours
    {
        get => _newTemplateItemHours;
        set => SetProperty(ref _newTemplateItemHours, value);
    }

    public string NewTemplateItemDifficulty
    {
        get => _newTemplateItemDifficulty;
        set => SetProperty(ref _newTemplateItemDifficulty, value);
    }

    public bool NewTemplateItemHasSubgroups
    {
        get => _newTemplateItemHasSubgroups;
        set => SetProperty(ref _newTemplateItemHasSubgroups, value);
    }

    public string NewTemplateItemWeekParity
    {
        get => _newTemplateItemWeekParity;
        set => SetProperty(ref _newTemplateItemWeekParity, value);
    }

    public bool IsCurriculumTemplateBuiltIn => SelectedCurriculumTemplate?.IsBuiltIn == true;
    public bool CanEditCurriculumTemplate => SelectedCurriculumTemplate is not null && !IsCurriculumTemplateBuiltIn;
    public bool CanDeleteCurriculumTemplate => CanEditCurriculumTemplate;
    public bool CanCopyCurriculumTemplate => SelectedCurriculumTemplate is not null;

    public string CurriculumTemplateManageHint => IsCurriculumTemplateBuiltIn
        ? "Встроенный шаблон — только просмотр и применение. Нажмите «Копия», чтобы создать редактируемую версию."
        : CanEditCurriculumTemplate
            ? "Свой шаблон: измените название и строки, нажмите «Сохранить», затем отметьте классы справа и «Применить»."
            : "Выберите шаблон в списке или нажмите «Создать» для своей параллели.";

    public IAsyncRelayCommand CreateCurriculumTemplateCommand { get; private set; } = null!;
    public IAsyncRelayCommand CopyCurriculumTemplateCommand { get; private set; } = null!;
    public IAsyncRelayCommand DeleteCurriculumTemplateCommand { get; private set; } = null!;
    public IAsyncRelayCommand SaveCurriculumTemplateCommand { get; private set; } = null!;
    public IRelayCommand AddCurriculumTemplateItemCommand { get; private set; } = null!;
    public IRelayCommand RemoveCurriculumTemplateItemCommand { get; private set; } = null!;

    private void InitializeCurriculumTemplateCommands()
    {
        CreateCurriculumTemplateCommand = new AsyncRelayCommand(CreateCurriculumTemplateAsync);
        CopyCurriculumTemplateCommand = new AsyncRelayCommand(CopyCurriculumTemplateAsync);
        DeleteCurriculumTemplateCommand = new AsyncRelayCommand(DeleteCurriculumTemplateAsync);
        SaveCurriculumTemplateCommand = new AsyncRelayCommand(SaveCurriculumTemplateAsync);
        AddCurriculumTemplateItemCommand = new RelayCommand(AddCurriculumTemplateItem, () => CanEditCurriculumTemplate);
        RemoveCurriculumTemplateItemCommand = new RelayCommand(RemoveCurriculumTemplateItem, () =>
            CanEditCurriculumTemplate && SelectedEditTemplateItem is not null);
    }

    partial void OnSelectedCurriculumTemplateChanged(CurriculumTemplate? value)
    {
        RebuildCurriculumTemplateTargets(value);
        LoadCurriculumTemplateEditor(value);
        NotifyCurriculumTemplateEditorState();
    }

    private void LoadCurriculumTemplateEditor(CurriculumTemplate? template)
    {
        EditCurriculumTemplateItems.Clear();
        if (template is null)
        {
            EditCurriculumTemplateName = "";
            EditCurriculumTemplateGrade = "1";
            return;
        }

        EditCurriculumTemplateName = template.Name;
        EditCurriculumTemplateGrade = template.GradeFrom.ToString(CultureInfo.InvariantCulture);
        foreach (var item in template.ResolveItemsForGrade())
        {
            EditCurriculumTemplateItems.Add(new CurriculumTemplateItem
            {
                SubjectName = item.SubjectName,
                HoursPerWeek = item.HoursPerWeek,
                DifficultyScore = item.DifficultyScore,
                HasSubgroups = item.HasSubgroups,
                WeekParity = item.WeekParity
            });
        }
    }

    private void NotifyCurriculumTemplateEditorState()
    {
        OnPropertyChanged(nameof(IsCurriculumTemplateBuiltIn));
        OnPropertyChanged(nameof(CanEditCurriculumTemplate));
        OnPropertyChanged(nameof(CanDeleteCurriculumTemplate));
        OnPropertyChanged(nameof(CanCopyCurriculumTemplate));
        OnPropertyChanged(nameof(CurriculumTemplateManageHint));
        AddCurriculumTemplateItemCommand.NotifyCanExecuteChanged();
        RemoveCurriculumTemplateItemCommand.NotifyCanExecuteChanged();
    }

    private async Task CreateCurriculumTemplateAsync()
    {
        try
        {
            var grade = CurriculumTemplateManageService.ParseGrade(EditCurriculumTemplateGrade);
            var id = await _curriculumTemplateManage.CreateAsync(grade, CurriculumTemplateList);
            _saveState.MarkDirty();
            await ReloadCurriculumTemplatesAsync(id);
            StatusMessage = $"Создан шаблон «{SelectedCurriculumTemplate?.Name}»";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task CopyCurriculumTemplateAsync()
    {
        if (SelectedCurriculumTemplate is null)
        {
            StatusMessage = "Выберите шаблон для копирования";
            return;
        }

        try
        {
            var id = await _curriculumTemplateManage.CopyAsync(
                SelectedCurriculumTemplate.Id,
                CurriculumTemplateList);
            _saveState.MarkDirty();
            await ReloadCurriculumTemplatesAsync(id);
            StatusMessage = $"Создана копия «{SelectedCurriculumTemplate?.Name}»";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task DeleteCurriculumTemplateAsync()
    {
        if (SelectedCurriculumTemplate is null || IsCurriculumTemplateBuiltIn)
            return;

        if (!_dialogs.ConfirmDelete(SelectedCurriculumTemplate.Name))
            return;

        try
        {
            await _curriculumTemplateManage.DeleteAsync(SelectedCurriculumTemplate.Id);
            _saveState.MarkDirty();
            await ReloadCurriculumTemplatesAsync(null);
            StatusMessage = "Шаблон удалён";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task SaveCurriculumTemplateAsync()
    {
        if (SelectedCurriculumTemplate is null || IsCurriculumTemplateBuiltIn)
            return;

        try
        {
            var templateId = SelectedCurriculumTemplate.Id;
            var grade = CurriculumTemplateManageService.ParseGrade(EditCurriculumTemplateGrade);
            await _curriculumTemplateManage.SaveAsync(
                templateId,
                EditCurriculumTemplateName,
                grade,
                EditCurriculumTemplateItems.ToList(),
                CurriculumTemplateList);
            _saveState.MarkDirty();
            await ReloadCurriculumTemplatesAsync(templateId);
            StatusMessage = $"Шаблон «{EditCurriculumTemplateName}» сохранён";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void AddCurriculumTemplateItem()
    {
        if (!CanEditCurriculumTemplate || NewTemplateItemSubject is null)
        {
            StatusMessage = "Выберите предмет для строки шаблона";
            return;
        }

        if (!double.TryParse(NewTemplateItemHours.Replace(',', '.'), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var hours) || hours <= 0)
        {
            StatusMessage = "Укажите корректное число часов";
            return;
        }

        if (!double.TryParse(NewTemplateItemDifficulty.Replace(',', '.'), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var difficulty) || difficulty < 0)
        {
            StatusMessage = "Укажите корректный балл Сивкова";
            return;
        }

        var name = NewTemplateItemSubject.Name.Trim();
        if (EditCurriculumTemplateItems.Any(i =>
                i.SubjectName.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = $"Предмет «{name}» уже есть в шаблоне";
            return;
        }

        EditCurriculumTemplateItems.Add(new CurriculumTemplateItem
        {
            SubjectName = name,
            HoursPerWeek = hours,
            DifficultyScore = difficulty,
            HasSubgroups = NewTemplateItemHasSubgroups,
            WeekParity = NewTemplateItemWeekParity
        });

        NewTemplateItemSubject = null;
        NewTemplateItemHours = "1";
        NewTemplateItemHasSubgroups = false;
        NewTemplateItemWeekParity = CurriculumWeekParity.EveryWeek;
        StatusMessage = $"В шаблон добавлен {name}";
    }

    private void RemoveCurriculumTemplateItem()
    {
        if (SelectedEditTemplateItem is null || !CanEditCurriculumTemplate)
            return;

        EditCurriculumTemplateItems.Remove(SelectedEditTemplateItem);
        SelectedEditTemplateItem = null;
    }

    private async Task ReloadCurriculumTemplatesAsync(int? selectId)
    {
        await LoadCurriculumTemplatesAsync();
        SelectedCurriculumTemplate = selectId is int id
            ? CurriculumTemplateList.FirstOrDefault(t => t.Id == id)
            : CurriculumTemplateList.FirstOrDefault();
        if (SelectedCurriculumTemplate is not null)
            RebuildCurriculumTemplateTargets(SelectedCurriculumTemplate);
    }
}
