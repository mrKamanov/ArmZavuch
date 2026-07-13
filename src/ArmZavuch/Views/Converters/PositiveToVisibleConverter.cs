using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ArmZavuch.Views.Converters;

/// <summary>Показывает элемент, если число больше нуля.</summary>
public sealed class PositiveToVisibleConverter : IValueConverter
{
    public static readonly PositiveToVisibleConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int n && n > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
