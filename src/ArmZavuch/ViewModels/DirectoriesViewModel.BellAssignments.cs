using ArmZavuch.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArmZavuch.ViewModels;

/// <summary>Назначение шаблонов звонков по умолчанию и override на класс (Справочники).</summary>
public sealed partial class DirectoriesViewModel
{
    [ObservableProperty] private string _defaultBellGrade1 = BellTemplateNaming.Grade1;
    [ObservableProperty] private string _defaultBellGrade1SecondHalf = BellTemplateNaming.Grade1SecondHalf;
    [ObservableProperty] private string _defaultBellShift1 = BellTemplateNaming.Standard;
    [ObservableProperty] private string _defaultBellShift2 = BellTemplateNaming.SecondShift;
    [ObservableProperty] private string _newClassBellTemplate = "";

    private bool _bellDefaultsDirty;
    private bool _bellDefaultsLoaded;

    public IAsyncRelayCommand SaveBellDefaultsCommand { get; }

    partial void OnDefaultBellGrade1Changed(string value) => _bellDefaultsDirty = true;
    partial void OnDefaultBellGrade1SecondHalfChanged(string value) => _bellDefaultsDirty = true;
    partial void OnDefaultBellShift1Changed(string value) => _bellDefaultsDirty = true;
    partial void OnDefaultBellShift2Changed(string value) => _bellDefaultsDirty = true;

    private async Task LoadBellDefaultsAsync()
    {
        if (_bellDefaultsDirty && _bellDefaultsLoaded)
            return;

        await _bellAssignment.LoadAsync();
        DefaultBellGrade1 = _bellAssignment.DefaultGrade1;
        DefaultBellGrade1SecondHalf = _bellAssignment.DefaultGrade1SecondHalf;
        DefaultBellShift1 = _bellAssignment.DefaultShift1;
        DefaultBellShift2 = _bellAssignment.DefaultShift2;
        _bellDefaultsLoaded = true;
        _bellDefaultsDirty = false;
    }

    private async Task SaveBellDefaultsAsync()
    {
        await _bellAssignment.SaveDefaultsAsync(
            DefaultBellGrade1,
            DefaultBellGrade1SecondHalf,
            DefaultBellShift1,
            DefaultBellShift2);
        _bellDefaultsDirty = false;
        RefreshBellTemplateCards();
        StatusMessage = "Назначения звонков по умолчанию сохранены";
    }

    private async Task<int?> ResolveClassBellTemplateIdAsync()
    {
        if (string.IsNullOrWhiteSpace(NewClassBellTemplate))
            return null;

        return await _bellAssignment.ResolveTemplateIdAsync(NewClassBellTemplate.Trim());
    }
}
