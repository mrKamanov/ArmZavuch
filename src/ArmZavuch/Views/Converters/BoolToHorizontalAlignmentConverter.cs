using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ArmZavuch.Views.Converters;

public sealed class BoolToHorizontalAlignmentConverter : IValueConverter
{
    public static readonly BoolToHorizontalAlignmentConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? HorizontalAlignment.Left : HorizontalAlignment.Center;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
