using System.Windows;
using System.Windows.Input;
using ArmZavuch.Models;

namespace ArmZavuch.Views.Controls;

/// <summary>Drag-and-drop уроков в сетке конструктора дня.</summary>
public static class DayEditorDragDrop
{
    private static Point _startPoint;
    private static bool _isDragging;

    public static void OnCellMouseDown(MouseButtonEventArgs e, DispatcherMonitorCell cell)
    {
        if (!CanDrag(cell))
            return;

        _startPoint = e.GetPosition(null);
        _isDragging = false;
    }

    public static void OnCellMouseMove(MouseEventArgs e, DispatcherMonitorCell cell, FrameworkElement source)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !CanDrag(cell) || cell.Lesson is null)
            return;

        var pos = e.GetPosition(null);
        if (_isDragging || (Math.Abs(pos.X - _startPoint.X) <= 4 && Math.Abs(pos.Y - _startPoint.Y) <= 4))
            return;

        _isDragging = true;
        var data = new DataObject();
        data.SetData(DragFormats.DayEditorCell, new DayEditorCellDragData
        {
            ClassId = cell.Lesson.ClassId,
            LessonNumber = cell.Lesson.LessonNumber,
            SlotId = cell.Lesson.SlotId
        });
        Mouse.Capture(null);
        DragScrollCoordinator.Instance.Begin(source);
        try
        {
            DragDrop.DoDragDrop(source, data, DragDropEffects.Move);
        }
        finally
        {
            DragScrollCoordinator.Instance.End();
            _isDragging = false;
        }
    }

    public static void OnDragOver(DragEventArgs e, DispatcherMonitorCell? target)
    {
        if (!e.Data.GetDataPresent(DragFormats.DayEditorCell))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = IsDropTarget(target) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    public static bool IsDayEditorDrag(IDataObject data) =>
        data.GetDataPresent(DragFormats.DayEditorCell);

    private static bool CanDrag(DispatcherMonitorCell cell) =>
        cell.HasLesson
        && cell.Lesson is not null
        && cell.ColumnKind == DispatcherMonitorCell.KindLesson
        && !cell.Lesson.IsCancelled;

    private static bool IsDropTarget(DispatcherMonitorCell? target)
    {
        if (target is null)
            return false;

        return target.ColumnKind is DispatcherMonitorCell.KindLesson or DispatcherMonitorCell.KindEmpty;
    }
}
