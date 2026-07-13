using System.Windows;
using System.Windows.Input;
using ArmZavuch.Models;
using ArmZavuch.ViewModels;

namespace ArmZavuch.Views.Controls;

/// <summary>Обработка drag-and-drop для ячеек сетки и палитры конструктора.</summary>
public static class ScheduleDragDrop
{
    private static Point _startPoint;
    private static bool _isDragging;

    public static void OnCellMouseDown(MouseButtonEventArgs e, GridCell cell, int subgroupIndex = 0)
    {
        var part = cell.GetPart(subgroupIndex);
        if (part?.TeacherId is null && part?.SubjectId is null)
            return;
        _startPoint = e.GetPosition(null);
        _isDragging = false;
    }

    public static void OnCellMouseMove(MouseEventArgs e, GridCell cell, FrameworkElement source, int subgroupIndex = 0)
    {
        var part = cell.GetPart(subgroupIndex);
        if (e.LeftButton != MouseButtonState.Pressed || part?.SubjectId is null)
            return;
        var pos = e.GetPosition(null);
        if (!_isDragging && (Math.Abs(pos.X - _startPoint.X) > 4 || Math.Abs(pos.Y - _startPoint.Y) > 4))
        {
            _isDragging = true;
            var data = new DataObject();
            data.SetData(DragFormats.Cell, new CellDragData
            {
                ClassId = cell.ClassId,
                LessonNumber = cell.LessonNumber,
                DayOfWeek = cell.DayOfWeek > 0 ? cell.DayOfWeek : 0,
                SlotId = part!.SlotId,
                SubjectId = part.SubjectId,
                TeacherId = part.TeacherId,
                RoomId = part.RoomId,
                SubgroupIndex = subgroupIndex
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
    }

    public static void OnPaletteMouseDown(MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(null);
        _isDragging = false;
    }

    public static bool TrySelectPaletteItem(MouseButtonEventArgs e) =>
        !_isDragging
        && (Math.Abs(e.GetPosition(null).X - _startPoint.X) <= 4
            && Math.Abs(e.GetPosition(null).Y - _startPoint.Y) <= 4);

    public static void OnCurriculumMouseMove(MouseEventArgs e, CurriculumItem item, FrameworkElement source) =>
        StartDragIfNeeded(e, source, () =>
        {
            var data = new DataObject();
            data.SetData(DragFormats.Curriculum, new CurriculumDragData
            {
                ClassId = item.ClassId,
                ClassName = item.ClassName,
                SubjectId = item.SubjectId,
                SubjectName = item.SubjectName,
                HasSubgroups = item.HasSubgroups
            });
            return data;
        });

    public static void OnTeacherMouseMove(
        MouseEventArgs e,
        TeacherPaletteItem item,
        FrameworkElement source,
        ConstructorViewModel vm)
    {
        StartDragIfNeeded(e, source, () =>
        {
            vm.BeginTeacherDragHints(item.Teacher);
            var data = new DataObject();
            data.SetData(DragFormats.Teacher, new TeacherDragData
            {
                TeacherId = item.Teacher.Id,
                TeacherName = item.Teacher.FullName
            });
            return data;
        }, () => vm.ClearDragHints());
    }

    public static void OnSubjectMouseMove(MouseEventArgs e, SubjectPaletteItem item, FrameworkElement source) =>
        StartDragIfNeeded(e, source, () =>
        {
            var data = new DataObject();
            data.SetData(DragFormats.Subject, new SubjectDragData
            {
                ClassId = item.ClassId,
                ClassName = item.ClassName,
                SubjectId = item.SubjectId,
                SubjectName = item.SubjectName,
                DifficultyScore = item.DifficultyScore,
                HasSubgroups = false
            });
            return data;
        });

    public static void OnRoomMouseMove(MouseEventArgs e, RoomPaletteItem item, FrameworkElement source) =>
        StartDragIfNeeded(e, source, () =>
        {
            var data = new DataObject();
            data.SetData(DragFormats.Room, new RoomDragData
            {
                RoomId = item.Room.Id,
                RoomNumber = item.Room.Number,
                BuildingName = item.Room.BuildingName
            });
            return data;
        });

    private static void StartDragIfNeeded(
        MouseEventArgs e,
        FrameworkElement source,
        Func<DataObject> buildData,
        Action? onDragFinished = null)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;
        var pos = e.GetPosition(null);
        if (!_isDragging && (Math.Abs(pos.X - _startPoint.X) > 4 || Math.Abs(pos.Y - _startPoint.Y) > 4))
        {
            _isDragging = true;
            Mouse.Capture(null);
            DragScrollCoordinator.Instance.Begin(source);
            try
            {
                DragDrop.DoDragDrop(source, buildData(), DragDropEffects.Copy);
            }
            finally
            {
                DragScrollCoordinator.Instance.End();
                _isDragging = false;
                onDragFinished?.Invoke();
            }
        }
    }

    public static bool IsConstructorDrag(IDataObject data) =>
        data.GetDataPresent(DragFormats.Cell)
        || data.GetDataPresent(DragFormats.Curriculum)
        || data.GetDataPresent(DragFormats.Teacher)
        || data.GetDataPresent(DragFormats.Subject)
        || data.GetDataPresent(DragFormats.Room);

    public static void OnCellDragOver(DragEventArgs e, GridCell? cell = null)
    {
        var allowed = IsConstructorDrag(e.Data);

        if (!allowed)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (cell?.DragHintLevel == DragHintLevel.Blocked)
            e.Effects = DragDropEffects.None;
        else
            e.Effects = DragDropEffects.Copy;

        e.Handled = true;
    }

    public static async void OnCellDrop(DragEventArgs e, GridCell target, ConstructorViewModel vm)
    {
        vm.ClearDragHints();

        if (vm.SelectedTemplate is null)
            return;

        if (e.Data.GetDataPresent(DragFormats.Teacher))
        {
            await vm.DropTeacherAsync(target, (TeacherDragData)e.Data.GetData(DragFormats.Teacher)!);
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DragFormats.Subject))
        {
            await vm.DropSubjectAsync(target, (SubjectDragData)e.Data.GetData(DragFormats.Subject)!);
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DragFormats.Room))
        {
            await vm.DropRoomAsync(target, (RoomDragData)e.Data.GetData(DragFormats.Room)!);
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DragFormats.Curriculum))
        {
            var payload = (CurriculumDragData)e.Data.GetData(DragFormats.Curriculum)!;
            await vm.DropCurriculumAsync(target, payload);
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DragFormats.Cell))
        {
            var payload = (CellDragData)e.Data.GetData(DragFormats.Cell)!;
            var sameDay = payload.DayOfWeek <= 0
                || target.DayOfWeek <= 0
                || payload.DayOfWeek == target.DayOfWeek;
            if (payload.ClassId == target.ClassId && payload.LessonNumber == target.LessonNumber
                && sameDay && target.GetPart(payload.SubgroupIndex)?.SlotId == payload.SlotId)
                return;
            await vm.MoveCellAsync(payload, target);
            e.Handled = true;
        }
    }
}
