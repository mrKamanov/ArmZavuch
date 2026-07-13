using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ArmZavuch.Views.Converters;

public sealed class HexToBrushConverter : IValueConverter
{
    public static HexToBrushConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value as string;
        if (string.IsNullOrWhiteSpace(hex))
            return Brushes.LightGray;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex.Trim());
            return new SolidColorBrush(color);
        }
        catch
        {
            return Brushes.LightGray;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
