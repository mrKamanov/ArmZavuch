using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArmZavuch.Models;

/// <summary>Строка таблицы нагрузки: предмет, часы, Сивков, п/г и педагоги.</summary>
public sealed partial class CurriculumGridRow : ObservableObject
{
    public CurriculumGridRow(CurriculumItem item, Teacher? primaryTeacher, Teacher? secondaryTeacher)
    {
        Item = item;
        _primaryTeacher = primaryTeacher;
        _secondaryTeacher = secondaryTeacher;
        _hoursText = FormatHours(item.HoursPerWeek);
        _difficultyText = FormatDifficulty(item.SubjectDifficultyScore);
        _hasSubgroups = item.HasSubgroups;
    }

    public CurriculumItem Item { get; }

    public bool IsSelected
    {
        get => Item.IsSelected;
        set
        {
            if (Item.IsSelected == value)
                return;
            Item.IsSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    [ObservableProperty] private Teacher? _primaryTeacher;
    [ObservableProperty] private Teacher? _secondaryTeacher;
    [ObservableProperty] private string _hoursText;
    [ObservableProperty] private string _difficultyText;
    [ObservableProperty] private bool _hasSubgroups;

    internal bool SuppressTeacherChange { get; set; }
    internal bool SuppressFieldChange { get; set; }

    partial void OnHasSubgroupsChanged(bool value) => Item.HasSubgroups = value;

    internal void SyncFieldsFromItem()
    {
        SuppressFieldChange = true;
        try
        {
            HoursText = FormatHours(Item.HoursPerWeek);
            DifficultyText = FormatDifficulty(Item.SubjectDifficultyScore);
            HasSubgroups = Item.HasSubgroups;
        }
        finally
        {
            SuppressFieldChange = false;
        }
    }

    private static string FormatHours(double hours) =>
        Math.Abs(hours - Math.Round(hours)) < 0.01
            ? hours.ToString("0", CultureInfo.InvariantCulture)
            : hours.ToString("0.#", CultureInfo.InvariantCulture);

    private static string FormatDifficulty(double score) =>
        Math.Abs(score - Math.Round(score)) < 0.01
            ? score.ToString("0", CultureInfo.InvariantCulture)
            : score.ToString("0.##", CultureInfo.InvariantCulture);
}
