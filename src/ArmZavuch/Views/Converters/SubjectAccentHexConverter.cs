using System.Globalization;
using System.Windows.Data;
using ArmZavuch.Services.Schedule;

namespace ArmZavuch.Views.Converters;

/// <summary>Hex-цвет или метка предмета для заголовков групп палитры.</summary>
public sealed class SubjectAccentHexConverter : IValueConverter
{
    public static SubjectAccentHexConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var part = parameter as string ?? "Border";
        var accent = SubjectAccentColors.Resolve(value as string);
        return part switch
        {
            "Background" => accent.BackgroundHex,
            "BadgeText" => accent.BadgeText,
            _ => accent.BorderHex
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
