using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ArmZavuch.Models;
using ArmZavuch.ViewModels;

namespace ArmZavuch.Views.Controls;

/// <summary>Интерактивная ячейка сетки конструктора дня: выбор, drag-and-drop.</summary>
public partial class DayEditorGridCellView
{
    private static readonly SolidColorBrush DropHighlightBrush = new(Color.FromRgb(37, 99, 235));

    public DayEditorGridCellView() => InitializeComponent();

    private DispatcherMonitorCell? Cell => DataContext as DispatcherMonitorCell;

    private DispatcherViewModel? Vm => FindHostView(this)?.DataContext as DispatcherViewModel;

    private static DispatcherView? FindHostView(DependencyObject start)
    {
        for (var node = start; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is DispatcherView view)
                return view;
        }

        return null;
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Cell is null || Vm is null)
            return;

        Vm.SelectDayEditorCellCommand.Execute(Cell);
        DayEditorDragDrop.OnCellMouseDown(e, Cell);
        Root.CaptureMouse();
        e.Handled = true;
    }

    private void Root_MouseMove(object sender, MouseEventArgs e)
    {
        if (Cell is null)
            return;

        DayEditorDragDrop.OnCellMouseMove(e, Cell, Root);
    }

    private void Root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        Root.ReleaseMouseCapture();

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        DayEditorDragDrop.OnDragOver(e, Cell);
        DragScrollCoordinator.Instance.ApplyDragOverScrollFromEvent(this, e);
        if (e.Effects == DragDropEffects.Move)
            Root.BorderBrush = DropHighlightBrush;
    }

    private void Root_DragLeave(object sender, DragEventArgs e) =>
        Root.ClearValue(Border.BorderBrushProperty);

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        Root.ClearValue(Border.BorderBrushProperty);
        if (Cell is null || Vm is null || !e.Data.GetDataPresent(DragFormats.DayEditorCell))
            return;

        var payload = (DayEditorCellDragData)e.Data.GetData(DragFormats.DayEditorCell)!;
        await Vm.ApplyDayEditorDropAsync(payload, Cell);
        e.Handled = true;
    }
}
