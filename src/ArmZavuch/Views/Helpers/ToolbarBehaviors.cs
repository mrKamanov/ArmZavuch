using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ArmZavuch.Views.Helpers;

/// <summary>Открывает ContextMenu по левому клику (панель инструментов).</summary>
public static class ToolbarBehaviors
{
    public static readonly DependencyProperty OpenContextMenuOnClickProperty =
        DependencyProperty.RegisterAttached(
            "OpenContextMenuOnClick",
            typeof(bool),
            typeof(ToolbarBehaviors),
            new PropertyMetadata(false, OnOpenContextMenuOnClickChanged));

    public static void SetOpenContextMenuOnClick(DependencyObject obj, bool value) =>
        obj.SetValue(OpenContextMenuOnClickProperty, value);

    public static bool GetOpenContextMenuOnClick(DependencyObject obj) =>
        (bool)obj.GetValue(OpenContextMenuOnClickProperty);

    private static void OnOpenContextMenuOnClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Button button)
            return;

        if (e.NewValue is true)
            button.Click += OnToolbarButtonClick;
        else
            button.Click -= OnToolbarButtonClick;
    }

    private static void OnToolbarButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is null)
            return;

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
        e.Handled = true;
    }
}
