using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ArmZavuch.Models;
using ArmZavuch.ViewModels;
using ArmZavuch.Views.Controls;

namespace ArmZavuch.Views;

public partial class ConstructorView
{
    public ConstructorView(ConstructorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        DragScrollCoordinator.Instance.Register(ScheduleGridHost);
        DragScrollCoordinator.Instance.RegisterGridZoom(ScheduleGridHost, ApplyGridZoomWheelDelta);
        ScheduleGridHost.AddHandler(UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(ScheduleGridHost_PreviewMouseWheel), true);
        Loaded += (_, _) => RegisterPaletteScrollTargets();
    }

    private void RegisterPaletteScrollTargets()
    {
        foreach (var listBox in new[] { TeacherPaletteList, SubjectPaletteList })
        {
            if (FindScrollViewer(listBox) is ScrollViewer paletteScroll)
                DragScrollCoordinator.Instance.Register(paletteScroll);
        }

        DragScrollCoordinator.Instance.Register(RoomPaletteScroll);
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer)
                return viewer;

            var nested = FindScrollViewer(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private ConstructorViewModel Vm => (ConstructorViewModel)DataContext;

    private void ApplyGridZoomWheelDelta(int delta)
    {
        var step = delta > 0 ? 1.12 : 1 / 1.12;
        Vm.GridZoom = Math.Clamp(Vm.GridZoom * step, 0.2, 4.0);
    }

    private void ScheduleGridHost_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        ApplyGridZoomWheelDelta(e.Delta);
        e.Handled = true;
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Vm.IsDragHintActive)
        {
            Vm.ClearDragHints();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && Vm.IsGridFullscreen)
        {
            Vm.IsGridFullscreen = false;
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Delete || Vm.SelectedCell is null)
            return;
        if (Keyboard.FocusedElement is TextBox or ComboBox or DatePicker)
            return;
        if (ScheduleGridHost.IsAncestorOf(Keyboard.FocusedElement as DependencyObject) != true)
            return;

        Vm.ClearCellCommand.Execute(null);
        e.Handled = true;
    }

    private void Cell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not GridCell cell)
            return;

        Vm.SelectCellCommand.Execute(cell);
        ScheduleDragDrop.OnCellMouseDown(e, cell, Vm.SelectedSubgroupIndex);
        fe.CaptureMouse();
    }

    private void Cell_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not GridCell cell)
            return;
        ScheduleDragDrop.OnCellMouseMove(e, cell, fe, Vm.SelectedSubgroupIndex);
    }

    private void Cell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe)
            fe.ReleaseMouseCapture();
    }

    private void Cell_DragOver(object sender, DragEventArgs e)
    {
        var cell = (sender as FrameworkElement)?.DataContext as GridCell;
        ScheduleDragDrop.OnCellDragOver(e, cell);
        DragScrollCoordinator.Instance.ApplyDragOverScrollFromEvent(
            sender as DependencyObject, e);
        if (!Vm.IsDragHintActive && sender is Border border)
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
    }

    private void ScheduleGridHost_DragOver(object sender, DragEventArgs e)
    {
        if (!ScheduleDragDrop.IsConstructorDrag(e.Data))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        DragScrollCoordinator.Instance.ApplyDragOverScroll(
            ScheduleGridHost, e.GetPosition(ScheduleGridHost));
        e.Handled = true;
    }

    private void Cell_DragLeave(object sender, DragEventArgs e)
    {
        if (!Vm.IsDragHintActive && sender is Border border)
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
    }

    private void Cell_Drop(object sender, DragEventArgs e)
    {
        if (!Vm.IsDragHintActive && sender is Border border)
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
        if (sender is FrameworkElement fe && fe.DataContext is GridCell cell)
            ScheduleDragDrop.OnCellDrop(e, cell, Vm);
    }

    private void Palette_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ScheduleDragDrop.OnPaletteMouseDown(e);
        if (sender is FrameworkElement fe)
            fe.CaptureMouse();
    }

    private void Palette_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not CurriculumItem item)
            return;
        ScheduleDragDrop.OnCurriculumMouseMove(e, item, fe);
    }

    private void Palette_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe)
            fe.ReleaseMouseCapture();
    }

    private void TeacherPalette_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ScheduleDragDrop.OnPaletteMouseDown(e);
        if (sender is FrameworkElement fe && fe.DataContext is TeacherPaletteItem item)
            Vm.PrefetchTeacherDragHints(item.Teacher.Id);
        if (sender is FrameworkElement captured)
            captured.CaptureMouse();
    }

    private void TeacherPalette_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not TeacherPaletteItem item)
            return;
        ScheduleDragDrop.OnTeacherMouseMove(e, item, fe, Vm);
    }

    private void TeacherPalette_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe)
            fe.ReleaseMouseCapture();

        if (ScheduleDragDrop.TrySelectPaletteItem(e)
            && sender is FrameworkElement itemHost
            && itemHost.DataContext is TeacherPaletteItem item)
        {
            Vm.SelectedPlacementTeacher = item;
        }
    }

    private void SubjectPalette_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not SubjectPaletteItem item)
            return;
        ScheduleDragDrop.OnSubjectMouseMove(e, item, fe);
    }

    private void RoomPalette_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not RoomPaletteItem item)
            return;
        ScheduleDragDrop.OnRoomMouseMove(e, item, fe);
    }
}
