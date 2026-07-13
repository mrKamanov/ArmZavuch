using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ArmZavuch.Views.Converters;

/// <summary>Скрывает элемент при пустой строке.</summary>
public sealed class EmptyToCollapsedConverter : IValueConverter
{
    public static readonly EmptyToCollapsedConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && !string.IsNullOrWhiteSpace(s) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
