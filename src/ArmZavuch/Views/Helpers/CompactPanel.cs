using System.Windows;

namespace ArmZavuch.Views.Helpers;

/// <summary>
/// Компактный режим боковой панели: при сужении скрывает подписи кнопок (остаются иконки + ToolTip).
/// </summary>
public static class CompactPanel
{
    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.RegisterAttached(
            "IsCompact",
            typeof(bool),
            typeof(CompactPanel),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty CompactAtProperty =
        DependencyProperty.RegisterAttached(
            "CompactAt",
            typeof(double),
            typeof(CompactPanel),
            new PropertyMetadata(300.0, OnCompactAtChanged));

    public static bool GetIsCompact(DependencyObject obj) => (bool)obj.GetValue(IsCompactProperty);
    public static void SetIsCompact(DependencyObject obj, bool value) => obj.SetValue(IsCompactProperty, value);

    public static double GetCompactAt(DependencyObject obj) => (double)obj.GetValue(CompactAtProperty);
    public static void SetCompactAt(DependencyObject obj, double value) => obj.SetValue(CompactAtProperty, value);

    private static void OnCompactAtChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        element.SizeChanged -= OnSizeChanged;
        if (e.NewValue is double)
            element.SizeChanged += OnSizeChanged;

        UpdateCompact(element);
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateCompact((FrameworkElement)sender);

    private static void UpdateCompact(FrameworkElement element)
    {
        var threshold = GetCompactAt(element);
        var isCompact = element.ActualWidth > 0 && element.ActualWidth < threshold;
        element.SetCurrentValue(IsCompactProperty, isCompact);
    }
}
