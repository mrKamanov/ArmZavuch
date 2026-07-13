using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ArmZavuch.Views.Converters;

/// <summary>Светлый фон из hex-цвета (для подсветки полей).</summary>
public sealed class HexToLightBrushConverter : IValueConverter
{
    public static HexToLightBrushConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var mix = parameter is string raw && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var m)
            ? m
            : 0.12;

        var hex = value as string;
        if (string.IsNullOrWhiteSpace(hex))
            return new SolidColorBrush(Color.FromRgb(248, 250, 252));

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex.Trim())!;
            return new SolidColorBrush(BlendWithWhite(color, mix));
        }
        catch
        {
            return new SolidColorBrush(Color.FromRgb(248, 250, 252));
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Color BlendWithWhite(Color color, double mix)
    {
        mix = Math.Clamp(mix, 0, 1);
        var inv = 1 - mix;
        return Color.FromRgb(
            (byte)(color.R * mix + 255 * inv),
            (byte)(color.G * mix + 255 * inv),
            (byte)(color.B * mix + 255 * inv));
    }
}
