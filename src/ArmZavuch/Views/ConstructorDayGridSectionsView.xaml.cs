using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ArmZavuch.Models;
using ArmZavuch.ViewModels;
using ArmZavuch.Views.Controls;

namespace ArmZavuch.Views;

/// <summary>Транспонированная сетка «классы × уроки» для одного дня или блока недели.</summary>
public partial class ConstructorDayGridSectionsView : UserControl
{
    public static readonly DependencyProperty SectionsProperty = DependencyProperty.Register(
        nameof(Sections),
        typeof(IEnumerable),
        typeof(ConstructorDayGridSectionsView),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(ConstructorViewModel),
        typeof(ConstructorDayGridSectionsView),
        new PropertyMetadata(null));

    public IEnumerable? Sections
    {
        get => (IEnumerable?)GetValue(SectionsProperty);
        set => SetValue(SectionsProperty, value);
    }

    public ConstructorViewModel? ViewModel
    {
        get => (ConstructorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public ConstructorDayGridSectionsView()
    {
        InitializeComponent();
    }

    private ConstructorViewModel? Vm => ViewModel ?? DataContext as ConstructorViewModel;

    private void Cell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null || sender is not FrameworkElement fe || fe.DataContext is not GridCell cell)
            return;

        Vm.SelectCellCommand.Execute(cell);
        ScheduleDragDrop.OnCellMouseDown(e, cell, Vm.SelectedSubgroupIndex);
        fe.CaptureMouse();
    }

    private void Cell_MouseMove(object sender, MouseEventArgs e)
    {
        if (Vm is null || sender is not FrameworkElement fe || fe.DataContext is not GridCell cell)
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
        if (Vm?.IsDragHintActive != true && sender is Border border)
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
    }

    private void Cell_DragLeave(object sender, DragEventArgs e)
    {
        if (Vm?.IsDragHintActive != true && sender is Border border)
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
    }

    private void Cell_Drop(object sender, DragEventArgs e)
    {
        if (Vm is null)
            return;
        if (Vm.IsDragHintActive != true && sender is Border border)
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
        if (sender is FrameworkElement fe && fe.DataContext is GridCell cell)
            ScheduleDragDrop.OnCellDrop(e, cell, Vm);
    }
}
