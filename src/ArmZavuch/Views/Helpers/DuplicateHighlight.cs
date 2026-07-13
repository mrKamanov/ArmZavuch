using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArmZavuch.Views.Helpers;

/// <summary>Красная обводка поля при дублировании.</summary>
public static class DuplicateHighlight
{
    public static readonly DependencyProperty IsDuplicateProperty = DependencyProperty.RegisterAttached(
        "IsDuplicate",
        typeof(bool),
        typeof(DuplicateHighlight),
        new PropertyMetadata(false, OnIsDuplicateChanged));

    public static bool GetIsDuplicate(DependencyObject obj) => (bool)obj.GetValue(IsDuplicateProperty);

    public static void SetIsDuplicate(DependencyObject obj, bool value) => obj.SetValue(IsDuplicateProperty, value);

    private static void OnIsDuplicateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Control control)
            return;

        if ((bool)e.NewValue)
        {
            control.BorderBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            control.BorderThickness = new Thickness(2);
        }
        else
        {
            control.ClearValue(Control.BorderBrushProperty);
            control.ClearValue(Control.BorderThicknessProperty);
        }
    }
}
