using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ArmZavuch.Views.Converters;

public sealed class StringToPointCollectionConverter : IValueConverter
{
    public static readonly StringToPointCollectionConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
            return new PointCollection();

        var points = new PointCollection();
        foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split(',');
            if (parts.Length != 2)
                continue;
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                continue;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                continue;
            points.Add(new Point(x, y));
        }

        return points;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
