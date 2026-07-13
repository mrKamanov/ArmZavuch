using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ArmZavuch.Models;

/// <summary>Ячейка сетки Конструктора: класс × урок (до 2 подгрупп).</summary>
public sealed class GridCell : INotifyPropertyChanged
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public int ClassGrade { get; set; }
    public int LessonNumber { get; set; }
    /// <summary>Колонка дин. паузы (не учебный урок).</summary>
    public bool IsDynamicPauseColumn { get; set; }
    /// <summary>Колонка перемены между уроками.</summary>
    public bool IsBreakColumn { get; set; }
    /// <summary>День недели 1–6 (Пн–Сб) в режиме «класс · неделя».</summary>
    public int DayOfWeek { get; set; }
    public List<SubgroupPart> Parts { get; } = [];
    public bool IsSplit => Parts.Count > 1;
    public bool HasContent => Parts.Count > 0;
    public bool HasConflict { get; set; }
    public string ConflictHint { get; set; } = "";
    public bool HasRoomSharedWarning { get; set; }
    public string RoomSharedHint { get; set; } = "";
    public bool IsAnchored => Parts.Any(p => p.IsAnchored);

    private bool _isSelected;

    /// <summary>Ячейка выбрана для редактирования в правой панели.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private DragHintLevel _dragHintLevel = DragHintLevel.None;
    private string _dragHintMessage = "";

    /// <summary>Подсказка при перетаскивании карточки (временно, не в БД).</summary>
    public DragHintLevel DragHintLevel
    {
        get => _dragHintLevel;
        set => SetProperty(ref _dragHintLevel, value);
    }

    public string DragHintMessage
    {
        get => _dragHintMessage;
        set => SetProperty(ref _dragHintMessage, value ?? "");
    }

    public string DisplayText => Parts.Count == 0
        ? ""
        : string.Join("\n—\n", Parts.Select(p =>
            (IsSplit || p.SubgroupIndex > 0) ? $"[{p.SubgroupIndex + 1}] {p.Line}" : p.Line));

    public SubgroupPart? GetPart(int subgroupIndex) =>
        Parts.FirstOrDefault(p => p.SubgroupIndex == subgroupIndex);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
