using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ArmZavuch.Views.Converters;

/// <summary>True → первое число, false → второе (формат: «fullscreen|normal»).</summary>
public sealed class BoolToGridLengthConverter : IValueConverter
{
    public static readonly BoolToGridLengthConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hide = value is true;
        var normal = 280.0;
        var hidden = 0.0;

        if (parameter is string text)
        {
            var parts = text.Split('|');
            if (parts.Length >= 2)
            {
                hidden = Parse(parts[0], hidden);
                normal = Parse(parts[1], normal);
            }
        }

        return new GridLength(hide ? hidden : normal);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static double Parse(string text, double fallback) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
}
