using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArmZavuch.Views.Helpers;

/// <summary>
/// Команды CRUD для DataGrid, снятие выделения повторным кликом и безопасная привязка SelectedItem
/// для нескольких сеток с общим выбором (нагрузка по классам).
/// </summary>
public static class DataGridCrudBehaviors
{
    private static readonly HashSet<DataGrid> SyncingGrids = [];

    public static readonly DependencyProperty EditRowCommandProperty =
        DependencyProperty.RegisterAttached(
            "EditRowCommand",
            typeof(ICommand),
            typeof(DataGridCrudBehaviors));

    public static void SetEditRowCommand(DependencyObject obj, ICommand? value) =>
        obj.SetValue(EditRowCommandProperty, value);

    public static ICommand? GetEditRowCommand(DependencyObject obj) =>
        (ICommand?)obj.GetValue(EditRowCommandProperty);

    public static readonly DependencyProperty DeleteRowCommandProperty =
        DependencyProperty.RegisterAttached(
            "DeleteRowCommand",
            typeof(ICommand),
            typeof(DataGridCrudBehaviors));

    public static void SetDeleteRowCommand(DependencyObject obj, ICommand? value) =>
        obj.SetValue(DeleteRowCommandProperty, value);

    public static ICommand? GetDeleteRowCommand(DependencyObject obj) =>
        (ICommand?)obj.GetValue(DeleteRowCommandProperty);

    public static readonly DependencyProperty ClearSelectionCommandProperty =
        DependencyProperty.RegisterAttached(
            "ClearSelectionCommand",
            typeof(ICommand),
            typeof(DataGridCrudBehaviors),
            new PropertyMetadata(null, OnClearSelectionCommandChanged));

    public static void SetClearSelectionCommand(DependencyObject obj, ICommand? value) =>
        obj.SetValue(ClearSelectionCommandProperty, value);

    public static ICommand? GetClearSelectionCommand(DependencyObject obj) =>
        (ICommand?)obj.GetValue(ClearSelectionCommandProperty);

    public static readonly DependencyProperty BoundSelectedItemProperty =
        DependencyProperty.RegisterAttached(
            "BoundSelectedItem",
            typeof(object),
            typeof(DataGridCrudBehaviors),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundSelectedItemChanged));

    public static void SetBoundSelectedItem(DependencyObject obj, object? value) =>
        obj.SetValue(BoundSelectedItemProperty, value);

    public static object? GetBoundSelectedItem(DependencyObject obj) =>
        obj.GetValue(BoundSelectedItemProperty);

    private static void OnClearSelectionCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
            return;

        grid.PreviewMouseLeftButtonDown -= Grid_PreviewMouseLeftButtonDown;
        if (e.NewValue is not null)
            grid.PreviewMouseLeftButtonDown += Grid_PreviewMouseLeftButtonDown;
    }

    private static void OnBoundSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
            return;

        EnsureSelectionHook(grid);

        if (SyncingGrids.Contains(grid))
            return;

        SyncingGrids.Add(grid);
        try
        {
            ApplyBoundSelection(grid, e.NewValue);
        }
        finally
        {
            SyncingGrids.Remove(grid);
        }
    }

    private static void EnsureSelectionHook(DataGrid grid)
    {
        grid.SelectionChanged -= Grid_SelectionChanged;
        grid.SelectionChanged += Grid_SelectionChanged;
    }

    private static void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DataGrid grid || SyncingGrids.Contains(grid))
            return;

        SyncingGrids.Add(grid);
        try
        {
            SetBoundSelectedItem(grid, grid.SelectedItem);
        }
        finally
        {
            SyncingGrids.Remove(grid);
        }
    }

    private static void ApplyBoundSelection(DataGrid grid, object? incoming)
    {
        if (incoming is null)
        {
            if (grid.SelectedItem is not null)
                grid.SelectedItem = null;
            return;
        }

        if (!grid.Items.Contains(incoming))
            return;

        if (!ReferenceEquals(grid.SelectedItem, incoming))
            grid.SelectedItem = incoming;
    }

    private static void Grid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid)
            return;

        var command = GetClearSelectionCommand(grid);
        if (command is null)
            return;

        if (FindParent<DataGridRow>(e.OriginalSource as DependencyObject) is not { IsSelected: true } row)
            return;

        if (IsInteractiveControl(e.OriginalSource as DependencyObject))
            return;

        if (!ReferenceEquals(row.Item, grid.SelectedItem))
            return;

        grid.SelectedItem = null;
        if (command.CanExecute(null))
            command.Execute(null);
        e.Handled = true;
    }

    private static T? FindParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool IsInteractiveControl(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is Button or CheckBox or ComboBox or TextBox)
                return true;

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
